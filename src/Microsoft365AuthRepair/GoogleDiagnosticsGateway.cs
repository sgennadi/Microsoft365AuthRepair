using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Shared UniversalDiagnostics v1 payload adapted to the common Google gateway.
// No username, UPN, SID, domain, hostname, log, URL, raw error text or tokens
// are ever included in the outgoing payload.
internal sealed record PreparedDiagnostic(string Id, string Json);

internal static class M365DiagnosticPayload
{
    internal const string Project = "sgennadi/Microsoft365AuthRepair";
    private const string Schema = "universal-diagnostics/v1";
    private static string Field(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing event field: " + name);
        return v.GetString()!;
    }

    private static string Match(string value, string pattern, string label)
    {
        if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
            throw new InvalidDataException("Unexpected diagnostic value: " + label);
        return value;
    }

    internal static string SafeSummary(string readOnlyLocalReport)
    {
        var keys = new[] { "AzureAdJoined", "DomainJoined", "DeviceAuthStatus",
            "AzureAdPrt", "WamDefaultSet", "BrokerPlugin" };
        var values = new List<string>();
        foreach (var key in keys)
        {
            var match = Regex.Match(readOnlyLocalReport,
                @"(?im)^\s*" + Regex.Escape(key) + @"\s*:\s*([A-Za-z]+)");
            var value = match.Success ? match.Groups[1].Value.ToUpperInvariant() : "UNKNOWN";
            value = key switch
            {
                "DeviceAuthStatus" when value is not ("SUCCESS" or "FAILED") => "UNKNOWN",
                "BrokerPlugin" when value is not ("PRESENT" or "MISSING") => "UNKNOWN",
                "DeviceAuthStatus" or "BrokerPlugin" => value,
                _ when value is "YES" or "NO" => value,
                _ => "UNKNOWN"
            };
            values.Add(key + ": " + value);
        }
        return string.Join("; ", values);
    }

    private static Dictionary<string, string> Signals(string summary)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in summary.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int index = part.IndexOf(':');
            if (index < 1) continue;
            var key = part[..index].Trim();
            var token = part[(index + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (token is not null) values[key] = token.ToUpperInvariant();
        }

        string Allowed(string key, string[] options) =>
            values.TryGetValue(key, out var v) && options.Contains(v, StringComparer.Ordinal)
                ? v : "UNKNOWN";
        string[] yn = ["YES", "NO"];
        return new Dictionary<string, string>
        {
            ["azure_ad_joined"] = Allowed("AzureAdJoined", yn),
            ["domain_joined"] = Allowed("DomainJoined", yn),
            ["device_auth_status"] = Allowed("DeviceAuthStatus", ["SUCCESS", "FAILED"]),
            ["azure_ad_prt"] = Allowed("AzureAdPrt", yn),
            ["wam_default_set"] = Allowed("WamDefaultSet", yn),
            ["broker_plugin"] = Allowed("BrokerPlugin", ["PRESENT", "MISSING"]),
            // Office state is deliberately restricted to enumerated, coarse
            // machine-wide signals. No raw Office WMI rows, host, key, PID,
            // tenant, SID or per-user subscription contents are transmitted.
            ["office_installed"] = Allowed("OfficeInstalled", ["YES", "NO", "UNKNOWN"]),
            ["office_edition"] = Allowed("OfficeEdition",
                ["PROPLUS_2024", "STANDARD_2024", "VISIO_2024", "PROJECT_2024",
                 "MULTIPLE_VOLUME", "VOLUME_OTHER", "MICROSOFT_365", "RETAIL", "UNKNOWN"]),
            ["office_activation"] = Allowed("OfficeActivation",
                ["LICENSED", "UNLICENSED", "PREVIEW_KEY_PRESENT",
                 "NOT_VERIFIED", "UNKNOWN"]),
            ["office_wmi_records"] = Allowed("OfficeWmiRecords",
                ["NONE", "ONE", "TWO", "THREE_PLUS", "NOT_APPLICABLE", "UNKNOWN"]),
            ["office_licensed_records"] = Allowed("OfficeLicensedRecords",
                ["NONE", "ONE", "TWO", "THREE_PLUS", "NOT_APPLICABLE", "UNKNOWN"]),
            ["office_preview_records"] = Allowed("OfficePreviewRecords",
                ["NONE", "ONE", "TWO", "THREE_PLUS", "NOT_APPLICABLE", "UNKNOWN"]),
            ["office_preview_key_records"] = Allowed("OfficePreviewKeyRecords",
                ["NONE", "ONE", "TWO", "THREE_PLUS", "NOT_APPLICABLE", "UNKNOWN"]),
            // TPM/BitLocker never send recovery keys, protector GUIDs, TPM
            // serials, raw WMI objects, OS drive labels or personal identities.
            ["tpm_present"] = Allowed("TpmPresent", ["YES", "NO", "UNKNOWN"]),
            ["tpm_version"] = Allowed("TpmVersion", ["1.2", "2.0", "UNKNOWN"]),
            ["tpm_ready"] = Allowed("TpmReady", ["YES", "NO", "UNKNOWN"]),
            ["bitlocker_os_protection"] = Allowed("BitLockerProtection", ["ON", "OFF", "UNKNOWN"]),
            ["bitlocker_os_conversion"] = Allowed("BitLockerConversion",
                ["FULLY_DECRYPTED", "FULLY_ENCRYPTED", "ENCRYPTING", "DECRYPTING",
                 "ENCRYPTION_PAUSED", "DECRYPTION_PAUSED", "UNKNOWN"]),
            ["memory_integrity_runtime"] = Allowed("MemoryIntegrityRuntime",
                ["RUNNING", "NOT_RUNNING", "UNKNOWN"]),
            ["dpapi_protection_policy"] = Allowed("DpapiProtectionPolicy",
                ["NOT_SET", "ZERO", "ONE", "OTHER", "UNKNOWN"]),
            ["dpapi_domain_joined"] = Allowed("DpapiDomainJoined", ["YES", "NO", "UNKNOWN"]),
            ["dpapi_writable_dc"] = Allowed("DpapiWritableDc", [
                "AVAILABLE", "NOT_FOUND", "UNKNOWN", "NOT_APPLICABLE"]),
            ["tpm_aik_enrollment"] = Allowed("TpmAikEnrollment", [
                "NOT_FOUND", "TIMEOUT", "INVALID_PUBLIC_KEY", "SERVER_ERROR",
                "OTHER_ERROR", "NONE_RECORDED", "UNKNOWN"]),
            ["tpm_ek_cert_store"] = Allowed("TpmEkCertStore", [
                "IMPORT_INCOMPLETE", "UNKNOWN"]),
            ["tpm_manufacturer_class"] = Allowed("TpmManufacturerClass", [
                "STM", "MSFT", "OTHER", "UNKNOWN"]),
            ["windows_tpm_release"] = Allowed("WindowsTpmRelease", [
                "WIN11_24H2", "WIN11_25H2", "WIN11_NEWER", "OTHER", "UNKNOWN"]),
            ["secure_boot_state"] = Allowed("SecureBootState",
                ["ON", "OFF", "UNSUPPORTED", "UNKNOWN"]),
            ["memory_integrity_configured"] = Allowed("MemoryIntegrityConfigured",
                ["ON", "OFF", "NOT_SET", "UNKNOWN"]),
            // Old saved reports lack this field. Do not misrepresent them as
            // complete: explicit NOT_VERIFIED is the safe compatibility value.
            ["device_probe_coverage"] = Allowed("DeviceProbeCoverage",
                ["COMPLETE", "PARTIAL", "NOT_VERIFIED"]) is var coverage &&
                coverage != "UNKNOWN" ? coverage : "NOT_VERIFIED"
        };
    }

    internal static PreparedDiagnostic Prepare(string localJson)
    {
        if (Encoding.UTF8.GetByteCount(localJson) > 65536)
            throw new InvalidDataException("Local diagnostic report exceeds size limit.");
        using var doc = JsonDocument.Parse(localJson);
        var e = doc.RootElement.GetProperty("event");
        var schema = Field(e, "schema");
        var id = Field(e, "id");
        var project = Field(e, "project");
        var originalComponent = Field(e, "component");
        // The first preview wrote the longer display label into local JSON.
        // Verify the ORIGINAL fingerprint, then map to an allowlisted
        // transport component for the shared gateway.
        var component = Match(
            originalComponent == "Entra WAM / AAD BrokerPlugin"
                ? "EntraWAMBroker" : originalComponent,
            @"^[A-Za-z][A-Za-z0-9 _-]{0,55}$", "component");
        var type = Match(Field(e, "error_type"), @"^[A-Za-z][A-Za-z0-9_]{0,63}$", "type");
        var code = Match(Field(e, "code"), @"^(?:0x[A-Fa-f0-9]{1,8}|[A-Za-z0-9_-]{1,32})$", "code");
        var version = Match(Field(e, "version"), @"^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$", "version");
        var level = Field(e, "level");
        var platform = Field(e, "platform");
        var arch = Field(e, "architecture");
        var time = Field(e, "timestamp_utc");
        var fp = Match(Field(e, "fingerprint"), @"^[a-f0-9]{64}$", "fingerprint");
        var summary = Field(e, "summary");

        if (schema != "universal-diagnostics/v1" || project != Project ||
            !Guid.TryParseExact(id, "D", out _) ||
            platform != "Windows" || arch is not ("AMD64" or "ARM64") ||
            level is not ("info" or "warning" or "error") ||
            !DateTime.TryParseExact(time, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when) ||
            when > DateTime.UtcNow.AddMinutes(5) || when < DateTime.UtcNow.AddDays(-90))
            throw new InvalidDataException("Invalid diagnostic event metadata.");

        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", new[] { project, originalComponent, type, code }.Select(v => v.ToLowerInvariant())))));
        if (fp != expected) throw new InvalidDataException("Invalid diagnostic fingerprint.");
        var outgoingFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", new[] { project, component, type, code }.Select(v => v.ToLowerInvariant())))));

        // This is an exact allowlist. Free-text summary is NOT sent.
        var @event = new Dictionary<string, object>
        {
            ["schema"] = schema, ["id"] = id, ["timestamp_utc"] = time,
            ["project"] = project, ["component"] = component,
            ["error_type"] = type, ["code"] = code, ["level"] = level,
            ["version"] = version, ["platform"] = platform, ["architecture"] = arch,
            ["fingerprint"] = outgoingFingerprint, ["signals"] = Signals(summary)
        };
        var prepared = JsonSerializer.Serialize(new { @event },
            new JsonSerializerOptions { WriteIndented = true });
        if (Encoding.UTF8.GetByteCount(prepared) > 8192)
            throw new InvalidDataException("Outgoing report exceeds the 8 KiB limit.");
        return new PreparedDiagnostic(id, prepared);
    }

    internal static bool SelfTest()
    {
        const string component = "EntraWAMBroker";
        const string type = "USER_REPORT", code = "WAM_REVIEW";
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", new[] { Project, component, type, code }.Select(x => x.ToLowerInvariant())))));
        var source = JsonSerializer.Serialize(new { @event = new
        {
            schema = Schema, id = Guid.NewGuid().ToString("D"),
            timestamp_utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            project = Project, component, error_type = type, code, level = "info",
            version = "1.0.1-preview.1", platform = "Windows", architecture = "ARM64", fingerprint,
            summary = "AzureAdJoined : YES; DomainJoined : YES; DeviceAuthStatus : SUCCESS; WamDefaultSet : YES; AzureAdPrt : YES; PRIVATE_USER_NAME;" +
                " OfficeInstalled: YES; OfficeEdition: PROPLUS_2024; OfficeActivation: LICENSED;" +
                " OfficeWmiRecords: THREE_PLUS; OfficeLicensedRecords: ONE;" +
                " OfficePreviewRecords: TWO; OfficePreviewKeyRecords: NONE;" +
                " TpmPresent: YES; TpmVersion: 2.0; TpmReady: YES;" +
                " BitLockerProtection: ON; BitLockerConversion: FULLY_ENCRYPTED;" +
                " MemoryIntegrityRuntime: RUNNING; DpapiProtectionPolicy: NOT_SET;" +
                " DpapiDomainJoined: YES; DpapiWritableDc: AVAILABLE;" +
                " TpmAikEnrollment: NOT_FOUND; TpmEkCertStore: IMPORT_INCOMPLETE;" +
                " TpmManufacturerClass: STM; WindowsTpmRelease: WIN11_24H2;" +
                " SecureBootState: OFF; MemoryIntegrityConfigured: ON;" +
                " DeviceProbeCoverage: PARTIAL;" +
                " TpmLockedOut: YES; KeySignTest: FAILED; AadRecoveryEnabled: YES;" +
                " NgcKeyId: PRIVATE_NGC_KEY_ID; KeyContainerId: PRIVATE_CONTAINER_ID;" +
                " TPMFirmwareVersion: secret.device.build;" +
                " BitLockerRecoveryKey: 111111-222222-333333-444444-555555-666666;" +
                " TpmOwnerAuth: secret-password; OfficeKmsHost: internal.example;" +
                " OfficeKey: XJ2XN-FW8RK-P4HMP-DKDBV-GCVGB"
        } });
        var outgoing = Prepare(source).Json;
        using var parsed = JsonDocument.Parse(outgoing);
        var signals = parsed.RootElement.GetProperty("event").GetProperty("signals");
        return signals.GetProperty("azure_ad_prt").GetString() == "YES" &&
            signals.GetProperty("broker_plugin").GetString() == "UNKNOWN" &&
            signals.GetProperty("office_installed").GetString() == "YES" &&
            signals.GetProperty("office_edition").GetString() == "PROPLUS_2024" &&
            signals.GetProperty("office_activation").GetString() == "LICENSED" &&
            signals.GetProperty("office_wmi_records").GetString() == "THREE_PLUS" &&
            signals.GetProperty("office_preview_records").GetString() == "TWO" &&
            signals.GetProperty("office_preview_key_records").GetString() == "NONE" &&
            signals.GetProperty("tpm_present").GetString() == "YES" &&
            signals.GetProperty("tpm_version").GetString() == "2.0" &&
            signals.GetProperty("tpm_ready").GetString() == "YES" &&
            signals.GetProperty("bitlocker_os_protection").GetString() == "ON" &&
            signals.GetProperty("bitlocker_os_conversion").GetString() == "FULLY_ENCRYPTED" &&
            signals.GetProperty("memory_integrity_runtime").GetString() == "RUNNING" &&
            signals.GetProperty("dpapi_protection_policy").GetString() == "NOT_SET" &&
            signals.GetProperty("dpapi_domain_joined").GetString() == "YES" &&
            signals.GetProperty("dpapi_writable_dc").GetString() == "AVAILABLE" &&
            signals.GetProperty("tpm_aik_enrollment").GetString() == "NOT_FOUND" &&
            signals.GetProperty("tpm_ek_cert_store").GetString() == "IMPORT_INCOMPLETE" &&
            signals.GetProperty("tpm_manufacturer_class").GetString() == "STM" &&
            signals.GetProperty("windows_tpm_release").GetString() == "WIN11_24H2" &&
            signals.GetProperty("secure_boot_state").GetString() == "OFF" &&
            signals.GetProperty("memory_integrity_configured").GetString() == "ON" &&
            signals.GetProperty("device_probe_coverage").GetString() == "PARTIAL" &&
            signals.EnumerateObject().Count() == 29 &&
            !outgoing.Contains("secret.device.build", StringComparison.Ordinal) &&
            !outgoing.Contains("TPMFirmwareVersion", StringComparison.Ordinal) &&
            !outgoing.Contains("TpmLockedOut", StringComparison.Ordinal) &&
            !outgoing.Contains("KeySignTest", StringComparison.Ordinal) &&
            !outgoing.Contains("AadRecoveryEnabled", StringComparison.Ordinal) &&
            !outgoing.Contains("PRIVATE_NGC_KEY_ID", StringComparison.Ordinal) &&
            !outgoing.Contains("PRIVATE_CONTAINER_ID", StringComparison.Ordinal) &&
            !outgoing.Contains("111111-222222-", StringComparison.Ordinal) &&
            !outgoing.Contains("secret-password", StringComparison.Ordinal) &&
            !outgoing.Contains("BitLockerRecoveryKey", StringComparison.Ordinal) &&
            !outgoing.Contains("TpmOwnerAuth", StringComparison.Ordinal) &&
            !outgoing.Contains("internal.example", StringComparison.Ordinal) &&
            !outgoing.Contains("XJ2XN-", StringComparison.Ordinal) &&
            !outgoing.Contains("OfficeKmsHost", StringComparison.Ordinal) &&
            !outgoing.Contains("OfficeKey", StringComparison.Ordinal) &&
            !outgoing.Contains("PRIVATE_USER_NAME", StringComparison.Ordinal) &&
            !outgoing.Contains("summary", StringComparison.OrdinalIgnoreCase) &&
            GoogleDiagnosticsGateway.ApprovedIssueUrl(
                "https://github.com/sgennadi/Microsoft365AuthRepair/issues/12") &&
            !GoogleDiagnosticsGateway.ApprovedIssueUrl(
                "https://github.com/sgennadi/TermsrvPatcherUnified/issues/12");
    }

    internal static void MarkDelivered(string file, string id, Uri issue)
    {
        var queue = Path.GetFullPath(
            Microsoft365AuthRepair.DiagnosticsEmbedded.UniversalDiagnosticsEmbedded.QueueDirectory(Project))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(file);
        if (!target.StartsWith(queue, StringComparison.OrdinalIgnoreCase))
            return; // External JSON is read-only; do not overwrite arbitrary files.
        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        if (Field(doc.RootElement.GetProperty("event"), "id") != id)
            throw new InvalidDataException("Report changed since review.");
        var node = JsonNode.Parse(File.ReadAllText(target)) ??
            throw new InvalidDataException("Invalid local report.");
        node["delivery"] = new JsonObject { ["status"] = "submitted", ["url"] = issue.AbsoluteUri };
        var temporary = Path.Combine(queue, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal sealed class GoogleDiagnosticsGateway : IDisposable
{
    // Public known deployment URL; private GitHub PAT remains ONLY in Apps Script.
    internal const string Endpoint =
        "https://script.google.com/macros/s/AKfycbyV-tq5tMa08XaEmgo6-vpHD6ISQGOci32oPjy5ELm2pHUAud_Wn5j5YjVV0kcc6o62/exec";

    private readonly HttpClient http = new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false
    }) { Timeout = TimeSpan.FromSeconds(30) };

    internal static bool ApprovedEndpoint(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
            uri.Host.Equals("script.google.com", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(uri.AbsolutePath, @"^/macros/s/[A-Za-z0-9_-]{25,180}/exec$",
                RegexOptions.CultureInvariant) &&
            uri.Query.Length == 0 && uri.Fragment.Length == 0;
    }

    internal static bool ApprovedIssueUrl(string url) =>
        Regex.IsMatch(url,
            @"\Ahttps://github\.com/sgennadi/Microsoft365AuthRepair/issues/[1-9][0-9]*\z",
            RegexOptions.CultureInvariant);

    private static bool ValidGoogleReceipt(Uri? url) =>
        url is not null && url.Scheme == "https" && url.IsDefaultPort &&
        url.UserInfo.Length == 0 && url.Host == "script.googleusercontent.com" &&
        url.AbsolutePath == "/macros/echo" && url.Fragment.Length == 0 &&
        url.AbsoluteUri.Length < 4096;

    // Read-only live receiver check; sends no diagnostic data or credentials.
    // A ready GET proves the deployed Code.gs schema, NOT that its token works.
    internal async Task<(bool Ready, string Code)> CheckReceiverAsync(CancellationToken ct)
    {
        if (!ApprovedEndpoint(Endpoint))
            return (false, "UNTRUSTED_ENDPOINT");
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        using var first = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        HttpResponseMessage? redirected = null;
        try
        {
            HttpResponseMessage actual = first;
            if (first.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
            {
                var location = first.Headers.Location;
                if (!ValidGoogleReceipt(location))
                    return (false, "UNTRUSTED_REDIRECT");
                using var next = new HttpRequestMessage(HttpMethod.Get, location);
                redirected = await http.SendAsync(next, HttpCompletionOption.ResponseHeadersRead, ct);
                if (redirected.RequestMessage?.RequestUri != location)
                    return (false, "UNEXPECTED_ENDPOINT");
                actual = redirected;
            }
            if (actual.StatusCode != HttpStatusCode.OK)
                return (false, "HTTP_" + (int)actual.StatusCode);
            if (actual.Content.Headers.ContentLength is > 8192)
                return (false, "INVALID_RESPONSE");
            await using var stream = await actual.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            byte[] bytes = new byte[2048];
            int count;
            while ((count = await stream.ReadAsync(bytes, ct)) > 0)
            {
                if (buffer.Length + count > 8192)
                    return (false, "INVALID_RESPONSE");
                buffer.Write(bytes, 0, count);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray());
            var root = doc.RootElement;
            if (!root.TryGetProperty("gateway_schema_version", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out var version))
                return (false, "GATEWAY_SCHEMA_UNAVAILABLE");
            if (version < 4 ||
                !root.TryGetProperty("capabilities", out var capabilities) ||
                capabilities.ValueKind != JsonValueKind.Array ||
                !capabilities.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                    x.GetString() == "microsoft365-auth-repair/v2"))
                return (false, "DEPLOY_GATEWAY_V4");
            return (true, "GATEWAY_V3_ONLINE");
        }
        catch (JsonException)
        {
            return (false, "INVALID_RESPONSE");
        }
        finally { redirected?.Dispose(); }
    }

    internal async Task<Uri> SendAsync(PreparedDiagnostic payload, CancellationToken ct)
    {
        if (!ApprovedEndpoint(Endpoint))
            throw new InvalidOperationException("Approved shared diagnostics endpoint is unavailable.");
        // v3 accepts only six M365 fields and rejects an expanded report.
        // Never upload an operator-approved payload to an incompatible
        // receiver or silently discard evidence to force it through.
        var readiness = await CheckReceiverAsync(ct).ConfigureAwait(false);
        if (!readiness.Ready)
            throw new InvalidOperationException(readiness.Code.StartsWith("GATEWAY_",
                StringComparison.Ordinal) ? readiness.Code : "GATEWAY_" + readiness.Code);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Microsoft365AuthRepair", "1.0"));
        request.Content = new StringContent(payload.Json, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.RequestMessage?.RequestUri?.AbsoluteUri != Endpoint)
            throw new InvalidDataException("Unexpected reporting destination.");
        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
        {
            var uri = response.Headers.Location;
            if (!ValidGoogleReceipt(uri))
                throw new InvalidDataException("Untrusted reporting receipt redirect.");
            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            get.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var answer = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
            if (answer.RequestMessage?.RequestUri != uri)
                throw new InvalidDataException("Receipt destination changed.");
            return await ParseAsync(answer, ct);
        }
        return await ParseAsync(response, ct);
    }

    private static async Task<Uri> ParseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("Reporting service returned HTTP " + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength is > 8192)
            throw new InvalidDataException("Reporting response exceeds size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        byte[] bytes = new byte[2048];
        int n;
        while ((n = await stream.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + n > 8192) throw new InvalidDataException("Reporting receipt exceeds size limit.");
            buffer.Write(bytes, 0, n);
        }
        using var doc = JsonDocument.Parse(buffer.ToArray());
        var result = doc.RootElement;
        if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            var reason = result.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString() : "not_confirmed";
            reason = reason switch
            {
                "not_configured" or "invalid_evidence" or "busy" or "github_not_confirmed" or
                "github_receipt_invalid" or "rate_limited" or "daily_limit" or
                "temporarily_unavailable" or "not_confirmed" => reason,
                _ => "server_rejected"
            };
            throw new InvalidOperationException("GATEWAY_" + reason);
        }
        if (!result.TryGetProperty("issue_url", out var issue) ||
            issue.ValueKind != JsonValueKind.String || !ApprovedIssueUrl(issue.GetString() ?? ""))
            throw new InvalidDataException("Invalid GitHub issue confirmation.");
        return new Uri(issue.GetString()!);
    }

    public void Dispose() => http.Dispose();
}
