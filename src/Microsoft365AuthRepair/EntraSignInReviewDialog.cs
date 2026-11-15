using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Optional delegated, device-code based admin investigation. Never collects
// passwords, silently signs in, caches tokens or uploads a diagnostic report.
// Requires an explicitly configured Entra public-client application with
// delegated AuditLog.Read.All consent and an authorized reader account.
internal sealed class EntraSignInReviewDialog : Form
{
    private readonly TextBox tenant = new() { Width = 390, AccessibleName = "Tenant GUID or organizations" };
    private readonly TextBox clientId = new() { Width = 390, AccessibleName = "Public-client application ID" };
    private readonly TextBox correlation = new() { Width = 390, AccessibleName = "Sign-in correlation ID" };
    private readonly Label instructions = UiTheme.Caption(string.Empty);
    private readonly Label result = UiTheme.Caption(string.Empty);
    private readonly Label userCode = UiTheme.Caption(string.Empty, heading: true);
    private readonly Button start = UiTheme.CreateActionButton(
        UpdateUiText.T("Review Entra sign-in", "Проверить вход в Entra", "בדיקת כניסה ל-Entra"), primary: true);
    private readonly Button openLogin = UiTheme.CreateActionButton(
        UpdateUiText.T("Open Microsoft sign-in", "Открыть вход Microsoft", "פתיחת כניסה ל-Microsoft"));
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private CancellationTokenSource? inFlight;
    private bool working;

    internal EntraSignInReviewDialog()
    {
        Text = "Microsoft 365 Auth Repair — " +
            UpdateUiText.T("Entra sign-in review", "Проверка входа Entra", "בדיקת כניסת Entra");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 490);
        Size = new Size(910, 655);
        ResponsiveLayout.FitInitialWindow(this, new Size(910, 655),
            new Size(1200, 870), widthFraction: 0.80, heightFraction: 0.76);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, ColumnCount = 1, RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(UiTheme.Caption(UpdateUiText.T(
            "Entra sign-in logs (optional)", "Журналы входа Entra (необязательно)",
            "יומני כניסה ל-Entra (אופציונלי)"), heading: true), 0, 0);
        instructions.Text = UpdateUiText.T(
            "Requires an organization-approved PUBLIC client app with delegated AuditLog.Read.All, admin consent and an authorized sign-in-log reader. Enter GUIDs only. Device code is explicitly approved in a browser. No password is entered here; access tokens stay in memory.",
            "Нужно одобренное публичное приложение Entra с AuditLog.Read.All, согласием администратора и ролью чтения журналов. Укажите GUID. Авторизация — по Device Code в браузере; пароли сюда не вводятся, токены не сохраняются.",
            "נדרש יישום Entra מאושר והרשאת AuditLog.Read.All. ההתחברות באמצעות קוד מכשיר בדפדפן; לא נשמרים סיסמאות או טוקנים.");
        instructions.Margin = new Padding(0, 7, 0, 11);
        layout.Controls.Add(instructions, 0, 1);
        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 3
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) inputs.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        string[] captions = [
            UpdateUiText.T("Tenant ID", "Tenant ID", "מזהה Tenant"),
            UpdateUiText.T("Client application ID", "ID приложения", "מזהה אפליקציה"),
            UpdateUiText.T("Correlation ID", "Correlation ID", "מזהה התאמה")];
        TextBox[] fields = [tenant, clientId, correlation];
        for (int i = 0; i < 3; i++)
        {
            var caption = UiTheme.Caption(captions[i]);
            caption.Margin = new Padding(0, 9, 14, 8);
            fields[i].Dock = DockStyle.Fill;
            fields[i].Margin = new Padding(0, 5, 0, 5);
            fields[i].MinimumSize = new Size(175, 29);
            inputs.Controls.Add(caption, 0, i);
            inputs.Controls.Add(fields[i], 1, i);
        }
        tenant.Text = "organizations";
        layout.Controls.Add(inputs, 0, 2);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = true, Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 6)
        };
        actions.Controls.Add(start);
        actions.Controls.Add(openLogin);
        actions.Controls.Add(close);
        layout.Controls.Add(actions, 0, 3);
        openLogin.Enabled = false;
        userCode.Visible = false;
        userCode.Margin = new Padding(0, 8, 0, 8);
        layout.Controls.Add(userCode, 0, 4);
        result.Text = UpdateUiText.T(
            "No query sent. A Correlation ID locates a sign-in event, not a verified local WAM repair.",
            "Запрос не отправлен. Correlation ID находит событие входа, но не подтверждает восстановление WAM.",
            "טרם נשלחה שאילתה. מזהה התאמה אינו אישור לתיקון WAM.");
        result.Margin = new Padding(0, 10, 0, 4);
        layout.Controls.Add(result, 0, 5);
        Controls.Add(ResponsiveLayout.Center(this, layout, 1100));
        layout.SizeChanged += (_, _) =>
        {
            int width = Math.Max(210, layout.ClientSize.Width - layout.Padding.Horizontal - 22);
            instructions.MaximumSize = new Size(width, 0);
            result.MaximumSize = new Size(width, 0);
            userCode.MaximumSize = new Size(width, 0);
        };
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(start, UiGlyph.Search, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        start.Click += async (_, _) => await BeginAsync();
        openLogin.Click += (_, _) =>
        {
            // Fixed Microsoft domain, never trust a remote supplied URL.
            Process.Start(new ProcessStartInfo("https://microsoft.com/devicelogin")
            { UseShellExecute = true });
        };
        close.Click += (_, _) => Close();
        FormClosing += (_, _) => inFlight?.Cancel();
        FormClosed += (_, _) => http.Dispose();
        CancelButton = close;
    }

    internal static bool ValidTenant(string value) =>
        value == "organizations" || (Guid.TryParse(value, out var id) && id != Guid.Empty);
    internal static bool ValidId(string value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty;

    private async Task BeginAsync()
    {
        if (working) return;
        var tenantId = tenant.Text.Trim();
        var appId = clientId.Text.Trim();
        var corrId = correlation.Text.Trim();
        if (!ValidTenant(tenantId) || !ValidId(appId) || !ValidId(corrId))
        {
            result.Text = UpdateUiText.T("Enter a valid tenant, client and correlation ID.",
                "Введите корректные Tenant, Client и Correlation ID.",
                "הזן מזהים תקינים.");
            return;
        }
        working = true;
        start.Enabled = tenant.Enabled = clientId.Enabled = correlation.Enabled = false;
        openLogin.Enabled = false;
        userCode.Visible = false;
        inFlight = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try
        {
            await QueryAsync(tenantId, appId, corrId, inFlight.Token);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) result.Text = UpdateUiText.T(
                "Entra sign-in request cancelled or expired.", "Запрос Entra отменён или истёк.",
                "הבקשה בוטלה או פג תוקפה.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
                                    or JsonException or InvalidOperationException or TaskCanceledException)
        {
            if (!IsDisposed) result.Text = UpdateUiText.T(
                "Entra lookup unavailable: ", "Проверка Entra недоступна: ",
                "בדיקת Entra אינה זמינה: ") + ex.GetType().Name;
        }
        finally
        {
            inFlight?.Dispose();
            inFlight = null;
            working = false;
            if (!IsDisposed && !Disposing)
            {
                start.Enabled = tenant.Enabled = clientId.Enabled = correlation.Enabled = true;
                openLogin.Enabled = false;
                userCode.Visible = false;
            }
        }
    }

    private static string? SafeError(JsonElement node)
    {
        if (!node.TryGetProperty("error", out var e)) return null;
        var value = e.GetString() ?? "";
        return value is "authorization_pending" or "slow_down" or "authorization_declined"
            or "expired_token" or "bad_verification_code" or "invalid_client"
            or "unauthorized_client" or "invalid_scope" or "access_denied"
            ? value : "authorization_error";
    }

    private static string StringProp(JsonElement item, string key, int maxLength = 40)
    {
        if (!item.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String)
            return "Unknown";
        var value = p.GetString() ?? "";
        return value.Length <= maxLength &&
            value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or ' ')
            ? value : "Unavailable";
    }

    private async Task QueryAsync(string tenantId, string appId,
        string correlationId, CancellationToken token)
    {
        var endpoint = "https://login.microsoftonline.com/" + tenantId + "/oauth2/v2.0/";
        using var deviceResponse = await http.PostAsync(endpoint + "devicecode",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = appId,
                ["scope"] = "https://graph.microsoft.com/AuditLog.Read.All"
            }), token);
        using var deviceDoc = JsonDocument.Parse(await deviceResponse.Content.ReadAsStringAsync(token));
        if (!deviceResponse.IsSuccessStatusCode)
        {
            result.Text = "Microsoft device code: " + (SafeError(deviceDoc.RootElement) ?? 
                ((int)deviceResponse.StatusCode).ToString());
            return;
        }
        var body = deviceDoc.RootElement;
        var code = body.GetProperty("device_code").GetString() ?? "";
        var user = body.GetProperty("user_code").GetString() ?? "";
        var expiration = Math.Clamp(body.GetProperty("expires_in").GetInt32(), 60, 900);
        var interval = body.TryGetProperty("interval", out var intervalJson)
            ? Math.Clamp(intervalJson.GetInt32(), 5, 30) : 5;
        if (code.Length < 10 || user.Length < 4) throw new InvalidOperationException("Invalid device code response.");
        userCode.Text = UpdateUiText.T("Enter code in Microsoft browser sign-in: ",
            "Введите код на странице входа Microsoft: ", "הזן קוד באתר Microsoft: ") + user;
        userCode.Visible = true;
        openLogin.Enabled = true;
        result.Text = UpdateUiText.T(
            "Approve sign-in using the account authorized to read Entra sign-in logs. The tool never sees your password.",
            "Подтвердите вход учётной записью, имеющей право читать журналы Entra. Пароль программа не получает.",
            "אשר התחברות בחשבון המורשה לצפות ביומני Entra.");
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiration);
        string? accessToken = null;
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), token);
            using var response = await http.PostAsync(endpoint + "token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = appId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = code
                }), token);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (response.IsSuccessStatusCode)
            {
                accessToken = doc.RootElement.GetProperty("access_token").GetString();
                break;
            }
            var error = SafeError(doc.RootElement);
            if (error == "authorization_pending") continue;
            if (error == "slow_down") { interval = Math.Min(30, interval + 5); continue; }
            result.Text = "Microsoft sign-in: " + (error ?? ((int)response.StatusCode).ToString());
            return;
        }
        userCode.Visible = false;
        openLogin.Enabled = false;
        if (string.IsNullOrEmpty(accessToken))
        {
            result.Text = "Device code authorization expired or did not complete.";
            return;
        }
        var query = "https://graph.microsoft.com/v1.0/auditLogs/signIns" +
            "?$filter=" + Uri.EscapeDataString("correlationId eq '" + correlationId + "'") +
            "&$select=createdDateTime,correlationId,status,conditionalAccessStatus&$top=5";
        using var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var graph = await http.SendAsync(request, token);
        if (!graph.IsSuccessStatusCode)
        {
            result.Text = graph.StatusCode switch
            {
                HttpStatusCode.Forbidden => UpdateUiText.T(
                    "Access denied. Verify AuditLog.Read.All consent, account role and licensing.",
                    "Доступ запрещён. Проверьте AuditLog.Read.All, роль учётной записи и лицензию.",
                    "אין הרשאה. בדוק הסכמה ותפקיד."),
                HttpStatusCode.BadRequest => UpdateUiText.T(
                    "Graph rejected the Correlation ID filter; check tenant and API support.",
                    "Graph отклонил фильтр Correlation ID; проверьте tenant и API.",
                    "Graph דחה את השאילתה."),
                _ => "Graph lookup: HTTP " + (int)graph.StatusCode
            };
            return;
        }
        using var responseDoc = JsonDocument.Parse(await graph.Content.ReadAsStringAsync(token));
        if (!responseDoc.RootElement.TryGetProperty("value", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Unexpected Graph response.");
        var summaries = new List<string>();
        foreach (var entry in entries.EnumerateArray().Take(5))
        {
            int err = -1;
            if (entry.TryGetProperty("status", out var state) &&
                state.TryGetProperty("errorCode", out var errorJson) &&
                errorJson.ValueKind == JsonValueKind.Number)
                err = errorJson.GetInt32();
            var condition = StringProp(entry, "conditionalAccessStatus");
            var date = StringProp(entry, "createdDateTime");
            summaries.Add(date + " | AADSTS code " + err + " | CA " + condition);
        }
        result.Text = summaries.Count == 0
            ? UpdateUiText.T("No matching sign-in events returned. Check retention, tenant and Correlation ID.",
                "Совпадающих событий нет. Проверьте срок хранения, tenant и Correlation ID.",
                "לא נמצאו אירועים תואמים.")
            : UpdateUiText.T("Entra returned sign-in metadata (not an authentication test):",
                "Entra вернул метаданные входа (это не проверка авторизации):",
                "נמצאו נתוני כניסה:") + Environment.NewLine +
              string.Join(Environment.NewLine, summaries);
        // No token, user/tenant identifiers, raw response, or credentials are persisted.
    }

    internal bool SelfTest() => ValidTenant("organizations") &&
        ValidTenant("11111111-1111-1111-1111-111111111111") &&
        !ValidTenant("common/test") && !ValidId("") &&
        ValidId("11111111-1111-1111-1111-111111111111") &&
        !ValidId("00000000-0000-0000-0000-000000000000") &&
        UiTheme.VerifyButtons(this, 3);
}
