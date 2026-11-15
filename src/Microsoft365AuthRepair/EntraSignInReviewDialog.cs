using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Optional administrator review. Auth implementation is shared for the full
// process session, adapted from BitKeyBridge Graph auth. No ROPC/password.
// Neither the account, GUIDs, tokens nor raw Graph replies are persisted.
internal sealed class EntraSignInReviewDialog : Form
{
    private readonly TextBox tenant = new() { AccessibleName = "Entra tenant", Dock = DockStyle.Fill };
    private readonly TextBox clientId = new() { AccessibleName = "Entra application ID", Dock = DockStyle.Fill };
    private readonly TextBox correlation = new() { AccessibleName = "Correlation ID", Dock = DockStyle.Fill };
    private readonly TextBox thumbprint = new() { AccessibleName = "Certificate thumbprint", Dock = DockStyle.Fill };
    private readonly ComboBox authMode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 175,
        AccessibleName = "Graph sign-in method"
    };
    private readonly Label instructions = UiTheme.Caption(string.Empty);
    private readonly Label result = UiTheme.Caption(string.Empty);
    private readonly Label userCode = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label certificateCaption = UiTheme.Caption(string.Empty);
    private readonly Button start = UiTheme.CreateActionButton(
        UpdateUiText.T("Find sign-in event", "Найти событие входа", "חיפוש אירוע כניסה"),
        primary: true);
    private readonly Button openLogin = UiTheme.CreateActionButton(
        UpdateUiText.T("Open Microsoft sign-in", "Открыть вход Microsoft", "פתיחת כניסה"));
    private readonly Button disconnect = UiTheme.CreateActionButton(
        UpdateUiText.T("Disconnect", "Отключить Graph", "ניתוק Graph"));
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private CancellationTokenSource? inFlight;
    private bool working;

    internal EntraSignInReviewDialog()
    {
        Text = "Microsoft 365 Auth Repair — " +
            UpdateUiText.T("Entra sign-in review", "Проверка входа Entra", "בדיקת כניסה ל-Entra");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 490);
        Size = new Size(940, 715);
        ResponsiveLayout.FitInitialWindow(this, new Size(940, 715),
            new Size(1200, 960), widthFraction: 0.85, heightFraction: 0.78);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, ColumnCount = 1, RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(UiTheme.Caption(UpdateUiText.T("Entra sign-in logs",
            "Журналы входа Entra", "יומני כניסה ל-Entra"), heading: true), 0, 0);
        instructions.Text = UpdateUiText.T(
            "Optional Microsoft Graph read-only investigation. Device Code needs an approved public client app with delegated AuditLog.Read.All and an authorized log reader. Certificate needs an app-only AuditLog.Read.All grant, admin consent and its private key. Neither mode modifies accounts or devices. Token is held in memory only until expiry or Disconnect.",
            "Необязательное чтение журналов через Microsoft Graph. Device Code требует одобренного публичного приложения, AuditLog.Read.All и прав чтения. Certificate требует разрешения AuditLog.Read.All для приложения, согласия администратора и закрытого ключа. Никаких изменений учетных записей; токен только в памяти до истечения или отключения.",
            "בדיקת Graph לקריאה בלבד. יש צורך בהרשאת AuditLog.Read.All והסכמת מנהל. טוקנים נשמרים בזיכרון בלבד.");
        root.Controls.Add(instructions, 0, 1);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 5
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        void Add(int row, string caption, Control control)
        {
            var label = UiTheme.Caption(caption);
            label.Margin = new Padding(0, 8, 12, 7);
            control.Margin = new Padding(0, 4, 0, 4);
            fields.Controls.Add(label, 0, row);
            fields.Controls.Add(control, 1, row);
        }
        Add(0, UpdateUiText.T("Tenant", "Tenant", "Tenant"), tenant);
        Add(1, UpdateUiText.T("Client ID", "Client ID", "Client ID"), clientId);
        Add(2, UpdateUiText.T("Correlation ID", "Correlation ID", "Correlation ID"), correlation);
        Add(3, UpdateUiText.T("Authentication", "Авторизация", "אימות"), authMode);
        certificateCaption.Text = UpdateUiText.T("Certificate SHA-1 thumbprint",
            "Отпечаток сертификата SHA-1", "טביעת אצבע של האישור");
        certificateCaption.Margin = new Padding(0, 8, 12, 7);
        fields.Controls.Add(certificateCaption, 0, 4);
        fields.Controls.Add(thumbprint, 1, 4);
        authMode.Items.AddRange([
            UpdateUiText.T("Device Code", "Device Code", "קוד מכשיר"),
            UpdateUiText.T("Certificate", "Сертификат", "אישור")
        ]);
        authMode.SelectedIndex = 0;
        tenant.Text = string.IsNullOrWhiteSpace(GraphSignInSession.Shared.Tenant)
            ? "organizations" : GraphSignInSession.Shared.Tenant;
        clientId.Text = GraphSignInSession.Shared.Client;
        void ModeChanged(object? sender, EventArgs args)
        {
            bool certificate = authMode.SelectedIndex == 1;
            certificateCaption.Visible = certificate;
            thumbprint.Visible = certificate;
            if (certificate && tenant.Text.Equals("organizations", StringComparison.OrdinalIgnoreCase))
                tenant.Text = "";
            if (!certificate && string.IsNullOrWhiteSpace(tenant.Text))
                tenant.Text = "organizations";
        }
        authMode.SelectedIndexChanged += ModeChanged;
        ModeChanged(this, EventArgs.Empty);
        root.Controls.Add(fields, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 6, 0, 6)
        };
        buttons.Controls.Add(start);
        buttons.Controls.Add(openLogin);
        buttons.Controls.Add(disconnect);
        buttons.Controls.Add(close);
        openLogin.Enabled = false;
        disconnect.Enabled = GraphSignInSession.Shared.IsConnected;
        root.Controls.Add(buttons, 0, 3);
        userCode.Visible = false;
        root.Controls.Add(userCode, 0, 4);
        result.Text = UpdateUiText.T(
            "No query sent. A Graph event is evidence of a cloud sign-in attempt, not proof of a local WAM repair.",
            "Запрос не отправлен. Событие Graph описывает попытку входа, но не подтверждает исправление WAM.",
            "טרם נשלחה שאילתה. אירוע Graph אינו אישור לתיקון WAM.");
        root.Controls.Add(result, 0, 5);
        root.SizeChanged += (_, _) =>
        {
            int width = Math.Max(210, root.ClientSize.Width - root.Padding.Horizontal - 24);
            instructions.MaximumSize = new Size(width, 0);
            result.MaximumSize = new Size(width, 0);
            userCode.MaximumSize = new Size(width, 0);
        };
        Controls.Add(ResponsiveLayout.Center(this, root, 1100));
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(start, UiGlyph.Search, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        start.Click += async (_, _) => await BeginAsync();
        openLogin.Click += (_, _) =>
        {
            Process.Start(new ProcessStartInfo("https://microsoft.com/devicelogin")
            { UseShellExecute = true });
        };
        disconnect.Click += (_, _) =>
        {
            if (working) return;
            GraphSignInSession.Shared.Disconnect();
            disconnect.Enabled = false;
            result.Text = UpdateUiText.T("Graph disconnected (memory-only token cleared).",
                "Graph отключён, токен удалён из памяти сеанса.",
                "החיבור נותק והטוקן נמחק מהזיכרון.");
        };
        close.Click += (_, _) => Close();
        FormClosing += (_, _) => inFlight?.Cancel();
        CancelButton = close;
    }

    private async Task BeginAsync()
    {
        if (working) return;
        var tenantValue = tenant.Text.Trim();
        var clientValue = clientId.Text.Trim();
        var correlationValue = correlation.Text.Trim();
        var isCert = authMode.SelectedIndex == 1;
        var certValue = thumbprint.Text.Trim();
        var mode = isCert ? GraphSignInAuthMode.Certificate : GraphSignInAuthMode.DeviceCode;
        if (!GraphSignInSession.ValidTenant(tenantValue, !isCert) ||
            !GraphSignInSession.ValidClient(clientValue) ||
            !GraphSignInSession.ValidCorrelation(correlationValue) ||
            (isCert && GraphSignInSession.NormalizeThumbprint(certValue).Length != 40))
        {
            result.Text = UpdateUiText.T("Enter valid tenant/client/correlation IDs and, for Certificate, its thumbprint.",
                "Укажите корректные Tenant/Client/Correlation ID и отпечаток сертификата для Certificate.",
                "יש להזין מזהים תקינים ואישור מתאים.");
            return;
        }
        working = true;
        start.Enabled = tenant.Enabled = clientId.Enabled = correlation.Enabled = false;
        authMode.Enabled = thumbprint.Enabled = false;
        disconnect.Enabled = openLogin.Enabled = false;
        userCode.Visible = false;
        inFlight = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try
        {
            var session = GraphSignInSession.Shared;
            if (!session.IsReusable(tenantValue, clientValue, mode, certValue))
            {
                if (isCert)
                    await session.ConnectCertificateAsync(tenantValue, clientValue, certValue, inFlight.Token);
                else
                    await session.ConnectDeviceCodeAsync(tenantValue, clientValue, info =>
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            try
                            {
                                BeginInvoke(new Action(() =>
                                {
                                    if (IsDisposed || Disposing) return;
                                    userCode.Text = UpdateUiText.T("Enter at microsoft.com/devicelogin: ",
                                        "Введите на microsoft.com/devicelogin: ", "הזן באתר Microsoft: ") + info.UserCode;
                                    userCode.Visible = true;
                                    openLogin.Enabled = true;
                                    result.Text = UpdateUiText.T(
                                        "Complete Microsoft sign-in in the browser. No password is entered here.",
                                        "Завершите вход Microsoft в браузере. Пароль сюда вводить не нужно.",
                                        "השלם כניסה בדפדפן ללא הזנת סיסמה כאן.");
                                }));
                            }
                            catch (InvalidOperationException) { }
                        }
                        return Task.CompletedTask;
                    }, inFlight.Token);
            }
            if (IsDisposed || Disposing) return;
            userCode.Visible = false;
            openLogin.Enabled = false;
            var events = await session.FindByCorrelationIdAsync(
                Guid.Parse(correlationValue), inFlight.Token);
            if (IsDisposed || Disposing) return;
            result.Text = events.Count == 0
                ? UpdateUiText.T("No matching events. Check tenant, retention and Correlation ID.",
                    "Нет совпадений. Проверьте tenant, срок хранения и Correlation ID.",
                    "לא נמצאו אירועים תואמים.")
                : UpdateUiText.T("Cloud sign-in metadata (not a local repair test):",
                    "Сведения облачных входов (не проверка локального ремонта):",
                    "נתוני כניסה בענן:") + Environment.NewLine +
                  string.Join(Environment.NewLine, events.Select(e =>
                    e.Time + " · AADSTS " + e.ErrorCode + " · CA " + e.ConditionalAccess));
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) result.Text = UpdateUiText.T("Graph request cancelled or expired.",
                "Запрос Graph отменён или истёк.", "הבקשה בוטלה או פג תוקפה.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or HttpRequestException or UnauthorizedAccessException or IOException
            or System.Security.Cryptography.CryptographicException or TimeoutException
            or System.Text.Json.JsonException or System.Security.SecurityException)
        {
            if (!IsDisposed) result.Text = UpdateUiText.T("Graph query could not complete: ",
                "Не удалось выполнить запрос Graph: ", "שאילתת Graph לא הושלמה: ") +
                ex.GetType().Name;
        }
        finally
        {
            inFlight?.Dispose();
            inFlight = null;
            working = false;
            if (!IsDisposed && !Disposing)
            {
                start.Enabled = tenant.Enabled = clientId.Enabled = correlation.Enabled = true;
                authMode.Enabled = thumbprint.Enabled = true;
                openLogin.Enabled = false;
                disconnect.Enabled = GraphSignInSession.Shared.IsConnected;
                userCode.Visible = false;
            }
        }
    }

    internal bool SelfTest() =>
        authMode.Items.Count == 2 && !openLogin.Enabled &&
        GraphSignInSession.SelfTest() && UiTheme.VerifyButtons(this, 4);
}
