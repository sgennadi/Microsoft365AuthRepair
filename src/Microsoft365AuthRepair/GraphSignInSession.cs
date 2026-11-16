using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Adapted to read-only sign-in diagnostics from the BitKeyBridge
// CloudGraphService device-code / certificate-client-assertion approach.
// Only AuditLog.Read.All is requested; no BitLocker, LAPS, Intune or
// application-management permissions. No stored passwords or ROPC.
internal enum GraphSignInAuthMode { DeviceCode, Certificate }
internal sealed record GraphDeviceCode(string UserCode, int ExpiresInSeconds);
internal sealed record GraphSignInEvent(string Time, int ErrorCode, string ConditionalAccess);

internal sealed class GraphSignInSession
{
    internal static GraphSignInSession Shared { get; } = new();
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 1024 * 1024
    };
    private readonly SemaphoreSlim authGate = new(1, 1);
    private sealed record Token(string Value, DateTimeOffset ExpiresAt,
        string Tenant, string Client, GraphSignInAuthMode Mode, string CertificateThumbprint);
    private Token? active;

    internal string Tenant => active?.Tenant ?? "";
    internal string Client => active?.Client ?? "";
    internal GraphSignInAuthMode? Mode => active?.Mode;
    internal bool IsConnected => active is { } token &&
        token.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(20);

    internal void Disconnect() => active = null;

    private static readonly Regex DomainPart =
        new("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$",
            RegexOptions.CultureInvariant);

    // BitKeyBridge uses the same exact-tenant rule for app administration.
    // This app never creates or modifies app registrations.
    internal static bool ValidTenant(string? value, bool allowOrganizations)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (allowOrganizations && value.Equals("organizations",
            StringComparison.OrdinalIgnoreCase)) return true;
        if (Guid.TryParse(value, out var guid)) return guid != Guid.Empty;
        if (value.Length > 253 || value.IndexOfAny(
            ['/', '\\', ':', '?', '#', '@', ' ']) >= 0) return false;
        var parts = value.Split('.');
        return parts.Length >= 2 && parts[^1].Length >= 2 &&
            !parts[^1].All(char.IsDigit) && parts.All(p => DomainPart.IsMatch(p));
    }

    internal static bool ValidClient(string? client) =>
        Guid.TryParse(client, out var value) && value != Guid.Empty;
    internal static bool ValidCorrelation(string? correlation) =>
        Guid.TryParse(correlation, out var value) && value != Guid.Empty;

    internal static string NormalizeThumbprint(string value)
    {
        var normalized = new string(value.Where(c => !char.IsWhiteSpace(c))
            .Select(char.ToUpperInvariant).ToArray());
        return Regex.IsMatch(normalized, "^[A-F0-9]{40}$") ? normalized : "";
    }

    internal bool IsReusable(string tenant, string client, GraphSignInAuthMode mode,
        string thumbprint = "") =>
        IsConnected && active is { } token &&
        token.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase) &&
        token.Client.Equals(client, StringComparison.OrdinalIgnoreCase) &&
        token.Mode == mode &&
        (mode != GraphSignInAuthMode.Certificate ||
            token.CertificateThumbprint.Equals(
                NormalizeThumbprint(thumbprint), StringComparison.Ordinal));

    private static string SafeOauthError(JsonElement element)
    {
        if (!element.TryGetProperty("error", out var error) ||
            error.ValueKind != JsonValueKind.String)
            return "http_error";
        var code = error.GetString() ?? "";
        return code is "authorization_pending" or "slow_down" or
            "authorization_declined" or "expired_token" or "bad_verification_code" or
            "invalid_client" or "unauthorized_client" or "invalid_scope" or
            "access_denied" or "interaction_required" or "invalid_grant"
            ? code : "authorization_error";
    }

    private static string Endpoint(string tenant) =>
        "https://login.microsoftonline.com/" + Uri.EscapeDataString(tenant) +
        "/oauth2/v2.0/";

    private static string TokenFrom(JsonElement response)
    {
        if (!response.TryGetProperty("access_token", out var token) ||
            token.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Microsoft returned no Graph token.");
        return token.GetString() is { Length: > 40 } t ? t :
            throw new InvalidOperationException("Microsoft returned an invalid Graph token.");
    }

    // Certificate /.default inherits every Graph application role assigned to
    // the registered app. Never reuse a BitKeyBridge certificate with BitLocker,
    // LAPS or Intune roles for an audit-only diagnostic session. Require a
    // dedicated AuditLog.Read.All app even when tenant/client/thumbprint exist.
    private static void RequireAuditOnlyAccess(string accessToken,
        GraphSignInAuthMode mode)
    {
        var segments = accessToken.Split('.');
        if (segments.Length < 3)
            throw new InvalidOperationException("Unsupported Graph token format.");
        string base64 = segments[1].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(base64));
        var payload = doc.RootElement;
        if (!payload.TryGetProperty("aud", out var audElement))
            throw new InvalidOperationException("Graph token audience is missing.");
        var audience = audElement.GetString() ?? "";
        if (audience is not ("https://graph.microsoft.com" or
            "00000003-0000-0000-c000-000000000000"))
            throw new InvalidOperationException("Graph token has an unexpected audience.");
        if (mode == GraphSignInAuthMode.Certificate)
        {
            if (!payload.TryGetProperty("roles", out var roles) ||
                roles.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Graph app-only roles are missing.");
            var list = roles.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            if (list.Length != 1 ||
                !list[0].Equals("AuditLog.Read.All", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Use a dedicated audit-only certificate app; Graph token contains other roles.");
        }
        else
        {
            if (!payload.TryGetProperty("scp", out var scopes) ||
                scopes.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Delegated Graph scopes missing.");
            var list = (scopes.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (list.Length != 1 ||
                !list[0].Equals("AuditLog.Read.All", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Delegated token must contain only AuditLog.Read.All.");
        }
    }

    internal static bool AuditAccessPolicySelfTest()
    {
        string Token(string permissions, bool appOnly)
        {
            var claim = appOnly
                ? new Dictionary<string, object>
                {
                    ["aud"] = "https://graph.microsoft.com",
                    ["roles"] = permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                }
                : new Dictionary<string, object>
                {
                    ["aud"] = "https://graph.microsoft.com", ["scp"] = permissions
                };
            return "header." + Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claim))) +
                ".signature";
        }
        bool Accepts(string value, GraphSignInAuthMode mode)
        {
            try { RequireAuditOnlyAccess(value, mode); return true; }
            catch (Exception ex) when (ex is InvalidOperationException or
                ArgumentException or FormatException or JsonException) { return false; }
        }
        return Accepts(Token("AuditLog.Read.All", false), GraphSignInAuthMode.DeviceCode) &&
            Accepts(Token("AuditLog.Read.All", true), GraphSignInAuthMode.Certificate) &&
            !Accepts(Token("AuditLog.Read.All BitlockerKey.Read.All", true),
                GraphSignInAuthMode.Certificate) &&
            !Accepts(Token("AuditLog.Read.All Device.Read.All", false),
                GraphSignInAuthMode.DeviceCode) &&
            !Accepts(Token("BitlockerKey.Read.All", true),
                GraphSignInAuthMode.Certificate);
    }

    private static DateTimeOffset Expiry(JsonElement response)
    {
        int seconds = response.TryGetProperty("expires_in", out var t) &&
            t.TryGetInt32(out int n) ? n : 3600;
        return DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, seconds - 120));
    }

    internal async Task ConnectDeviceCodeAsync(string tenant, string client,
        Func<GraphDeviceCode, Task> showCode, CancellationToken cancellationToken)
    {
        if (!ValidTenant(tenant, true) || !ValidClient(client))
            throw new ArgumentException("Tenant or Client ID is invalid.");
        tenant = tenant.Trim(); client = client.Trim();
        if (IsReusable(tenant, client, GraphSignInAuthMode.DeviceCode)) return;
        await authGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsReusable(tenant, client, GraphSignInAuthMode.DeviceCode)) return;
            using var deviceResponse = await http.PostAsync(Endpoint(tenant) + "devicecode",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = client,
                    // Read-only, least-privilege delegated scope; no offline_access.
                    ["scope"] = "https://graph.microsoft.com/AuditLog.Read.All"
                }), cancellationToken).ConfigureAwait(false);
            using var deviceDoc = JsonDocument.Parse(
                await deviceResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!deviceResponse.IsSuccessStatusCode)
                throw new InvalidOperationException("Device-code request: " +
                    SafeOauthError(deviceDoc.RootElement));
            var root = deviceDoc.RootElement;
            var deviceCode = root.GetProperty("device_code").GetString() ?? "";
            var userCode = root.GetProperty("user_code").GetString() ?? "";
            var duration = root.TryGetProperty("expires_in", out var time) &&
                time.TryGetInt32(out var seconds) ? Math.Clamp(seconds, 60, 900) : 900;
            var interval = root.TryGetProperty("interval", out var intervalJson) &&
                intervalJson.TryGetInt32(out var intervalValue)
                ? Math.Clamp(intervalValue, 5, 30) : 5;
            if (deviceCode.Length < 10 || userCode.Length < 4)
                throw new InvalidOperationException("Microsoft returned an incomplete device code.");
            await showCode(new GraphDeviceCode(userCode, duration)).ConfigureAwait(false);

            var until = DateTimeOffset.UtcNow.AddSeconds(duration);
            while (DateTimeOffset.UtcNow < until)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken)
                    .ConfigureAwait(false);
                using var tokenResponse = await http.PostAsync(Endpoint(tenant) + "token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = client,
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                        ["device_code"] = deviceCode
                    }), cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(
                    await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var accessToken = TokenFrom(doc.RootElement);
                    RequireAuditOnlyAccess(accessToken, GraphSignInAuthMode.DeviceCode);
                    active = new Token(accessToken, Expiry(doc.RootElement),
                        tenant, client, GraphSignInAuthMode.DeviceCode, "");
                    return;
                }
                var error = SafeOauthError(doc.RootElement);
                if (error == "authorization_pending") continue;
                if (error == "slow_down") { interval = Math.Min(30, interval + 5); continue; }
                throw new InvalidOperationException("Device-code sign-in: " + error);
            }
            throw new TimeoutException("Microsoft device-code sign-in expired.");
        }
        finally { authGate.Release(); }
    }

    // BitKeyBridge CertificateService: find a valid certificate in CurrentUser
    // or LocalMachine My, require an RSA private key, and sign a client assertion.
    private static string ClientAssertion(string url, string client, string thumbprint)
    {
        thumbprint = NormalizeThumbprint(thumbprint);
        if (thumbprint.Length != 40)
            throw new ArgumentException("Certificate thumbprint must be SHA-1 hex.");
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            foreach (var certificate in store.Certificates.OfType<X509Certificate2>())
            {
                if (!string.Equals(certificate.Thumbprint, thumbprint,
                    StringComparison.OrdinalIgnoreCase)) continue;
                if (!certificate.HasPrivateKey ||
                    certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                    certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                    throw new InvalidOperationException("Selected certificate is unavailable, expired or has no private key.");
                var now = DateTimeOffset.UtcNow;
                var header = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["alg"] = "RS256", ["typ"] = "JWT",
                    ["x5t"] = Base64Url(SHA1.HashData(certificate.RawData))
                });
                var payload = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["aud"] = url, ["iss"] = client, ["sub"] = client,
                    ["jti"] = Guid.NewGuid().ToString("N"),
                    ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
                    ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds()
                });
                var unsigned = Base64Url(Encoding.UTF8.GetBytes(header)) + "." +
                    Base64Url(Encoding.UTF8.GetBytes(payload));
                using var rsa = certificate.GetRSAPrivateKey() ??
                    throw new InvalidOperationException("RSA private key is unavailable.");
                var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsigned),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return unsigned + "." + Base64Url(signature);
            }
        }
        throw new InvalidOperationException("Certificate not found in CurrentUser/My or LocalMachine/My.");
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal async Task ConnectCertificateAsync(string tenant, string client,
        string thumbprint, CancellationToken ct)
    {
        if (!ValidTenant(tenant, false) || !ValidClient(client))
            throw new ArgumentException("A specific tenant and Client ID are required.");
        thumbprint = NormalizeThumbprint(thumbprint);
        if (thumbprint.Length != 40) throw new ArgumentException("Invalid certificate thumbprint.");
        tenant = tenant.Trim(); client = client.Trim();
        if (IsReusable(tenant, client, GraphSignInAuthMode.Certificate, thumbprint)) return;
        await authGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsReusable(tenant, client, GraphSignInAuthMode.Certificate, thumbprint)) return;
            var tokenUri = Endpoint(tenant) + "token";
            var assertion = ClientAssertion(tokenUri, client, thumbprint);
            using var response = await http.PostAsync(tokenUri,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = client,
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["client_assertion_type"] =
                        "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                    ["client_assertion"] = assertion
                }), ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Certificate Graph login: " +
                    SafeOauthError(doc.RootElement));
            var accessToken = TokenFrom(doc.RootElement);
            RequireAuditOnlyAccess(accessToken, GraphSignInAuthMode.Certificate);
            active = new Token(accessToken, Expiry(doc.RootElement),
                tenant, client, GraphSignInAuthMode.Certificate, thumbprint);
        }
        finally { authGate.Release(); }
    }

    internal async Task<IReadOnlyList<GraphSignInEvent>> FindByCorrelationIdAsync(
        Guid correlationId, CancellationToken ct)
    {
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID required.");
        var token = active;
        if (token is null || token.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(20))
            throw new InvalidOperationException("Connect to Entra first; session token is missing/expired.");

        // Exactly one read-only Graph endpoint. No Microsoft 365 account
        // profile, UPN, IP, tenant GUID or raw response is returned to UI.
        var filter = "correlationId eq '" + correlationId.ToString("D") + "'";
        var uri = "https://graph.microsoft.com/v1.0/auditLogs/signIns" +
            "?$filter=" + Uri.EscapeDataString(filter) +
            "&$select=createdDateTime,status,conditionalAccessStatus&$top=5";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Graph denied AuditLog.Read.All, admin consent or the sign-in log reader role.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Graph sign-in lookup returned HTTP " +
                (int)response.StatusCode, null, response.StatusCode);
        using var data = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (!data.RootElement.TryGetProperty("value", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Graph returned an unexpected sign-in response.");
        var records = new List<GraphSignInEvent>();
        foreach (var entry in results.EnumerateArray().Take(5))
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            var date = SafeText(entry, "createdDateTime", 40);
            var conditional = SafeText(entry, "conditionalAccessStatus", 35);
            int code = -1;
            if (entry.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.Object &&
                status.TryGetProperty("errorCode", out var error) &&
                error.ValueKind == JsonValueKind.Number && error.TryGetInt32(out var n))
                code = n;
            records.Add(new GraphSignInEvent(date, code, conditional));
        }
        return records;
    }

    private static string SafeText(JsonElement row, string key, int limit)
    {
        if (!row.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.String) return "Unknown";
        string text = value.GetString() ?? "";
        return text.Length <= limit && text.All(c => char.IsLetterOrDigit(c) ||
            c is '-' or '_' or ':' or '.' or ' ')
            ? text : "Unavailable";
    }

    internal static bool SelfTest()
    {
        var client = "11111111-1111-1111-1111-111111111111";
        return AuditAccessPolicySelfTest() && ValidTenant("organizations", true) &&
            !ValidTenant("organizations", false) &&
            ValidTenant("contoso.onmicrosoft.com", false) &&
            ValidTenant(client, false) &&
            !ValidTenant("https://login.microsoftonline.com", false) &&
            !ValidTenant("common", true) && !ValidTenant("tenant/path", false) &&
            ValidClient(client) && ValidCorrelation(client) &&
            !ValidClient(Guid.Empty.ToString()) && !ValidCorrelation("not-a-guid") &&
            NormalizeThumbprint("00112233445566778899aabbccddeeff00112233") ==
                "00112233445566778899AABBCCDDEEFF00112233" &&
            NormalizeThumbprint("bad") == "" &&
            !Shared.IsReusable("none", "none", GraphSignInAuthMode.DeviceCode);
    }
}
