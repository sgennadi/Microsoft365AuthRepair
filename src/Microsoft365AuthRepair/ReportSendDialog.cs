using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// The only place where a prepared report may be submitted. The exact
// allowlisted JSON is displayed before an explicit Send button click.
// A checked review checkbox never initiates a network request by itself.
internal sealed class ReportSendDialog : Form
{
    private readonly PreparedDiagnostic prepared;
    private readonly string? sourceFile;
    private readonly TextBox preview = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Multiline = true,
        WordWrap = false,
        TabStop = false, // Do not take initial focus or highlight the JSON.
        HideSelection = true,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 9f),
        BackColor = SystemColors.Window
    };
    private readonly CheckBox consent = new()
    {
        AutoSize = true,
        Checked = true, // User-requested default; actual upload still needs Send.
        Margin = new Padding(0, 8, 0, 8)
    };
    private readonly TextBox status = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Multiline = true,
        WordWrap = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        Height = 62,
        MinimumSize = new Size(0, 56),
        BackColor = SystemColors.Control,
        ForeColor = SystemColors.ControlText
    };
    private readonly Button send = UiTheme.CreateActionButton(
        UpdateUiText.T("Send anonymously", "Отправить анонимно", "שלח בעילום שם"), primary: true);
    private readonly Button keepLocal = UiTheme.CreateActionButton(
        UpdateUiText.T("Keep local", "Оставить локально", "השאר מקומי"));
    private CancellationTokenSource? sending;
    private bool busy;

    internal Uri? ConfirmedIssue { get; private set; }
    internal bool ConsentInitiallyChecked => consent.Checked;
    internal bool IsSendExplicit => send.Enabled;
    internal bool PreviewStartsUnselected => preview.ReadOnly &&
        !preview.TabStop && preview.SelectionLength == 0 &&
        preview.Text == prepared.Json && keepLocal.Enabled;

    internal ReportSendDialog(PreparedDiagnostic payload, string? queuedFile)
    {
        prepared = payload;
        sourceFile = queuedFile;
        Text = UpdateUiText.T("Review & send diagnostic report",
            "Проверка и отправка отчёта", "בדיקה ושליחת דוח אבחון");
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiTheme.BodyFont();
        Size = new Size(920, 650);
        MinimumSize = new Size(650, 490);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(920, 650),
            new Size(1180, 900), widthFraction: 0.84, heightFraction: 0.79);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(UiTheme.Caption(UpdateUiText.T(
            "Review before sending", "Проверьте перед отправкой", "בדוק לפני השליחה"), heading: true), 0, 0);

        var warning = UiTheme.Caption(UpdateUiText.T(
            "Only the JSON shown below will be sent. A PUBLIC GitHub Issue will be created and can be seen by anyone. Account names, hostnames, tokens and logs are excluded.",
            "Отправляется только JSON ниже. Создаётся ПУБЛИЧНАЯ GitHub Issue, доступная всем. Имена пользователей, компьютеров, токены и журналы исключены.",
            "רק ה-JSON המוצג יישלח. תיווצר בעיה ציבורית ב-GitHub, ללא שמות משתמשים, מחשבים, אסימונים או יומנים."));
        warning.MaximumSize = new Size(720, 0);
        root.Controls.Add(warning, 0, 1);
        root.SizeChanged += (_, _) =>
        {
            int width = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 20);
            warning.MaximumSize = new Size(width, 0);
            consent.MaximumSize = new Size(width, 0);
        };
        preview.MinimumSize = new Size(0, 180);
        preview.Text = payload.Json;
        preview.SelectionStart = 0;
        preview.SelectionLength = 0;
        preview.AccessibleName = UpdateUiText.T("JSON report (read-only, manually selectable)",
            "JSON-отчёт (только чтение; можно выделять вручную)",
            "דוח JSON לקריאה בלבד, ניתן לסמן ידנית");
        root.Controls.Add(preview, 0, 2);
        consent.Text = UpdateUiText.T(
            "I approve sending these reviewed fields to a PUBLIC GitHub Issue.",
            "Я разрешаю отправить эти данные в ПУБЛИЧНУЮ GitHub Issue.",
            "אני מאשר/ת שליחת השדות שנבדקו לבעיית GitHub ציבורית.");
        consent.AccessibleDescription = UpdateUiText.T(
            "Unchecking this option disables Send. No report is sent automatically.",
            "Снятие галочки отключает отправку. Автоматической отправки нет.",
            "ביטול הסימון מונע שליחה. אין שליחה אוטומטית.");
        root.Controls.Add(consent, 0, 3);
        status.Text = UpdateUiText.T(
            "Report is saved locally. Nothing has been sent.",
            "Отчёт сохранён локально. Ничего не отправлено.",
            "הדוח נשמר מקומית. דבר לא נשלח.");
        root.Controls.Add(status, 0, 4);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        actions.Controls.Add(send);
        actions.Controls.Add(keepLocal);
        root.Controls.Add(actions, 0, 5);
        Controls.Add(ResponsiveLayout.Center(this, root, 1130));
        CancelButton = keepLocal;
        // WinForms can select every character of the first focused TextBox
        // when a modal dialog is shown. Put keyboard focus on the safe
        // Keep local button and clear selection AFTER the dialog is visible.
        // Mouse selection, Ctrl+A and Ctrl+C still work in the read-only JSON.
        Shown += (_, _) => BeginInvoke((MethodInvoker)(() =>
        {
            if (IsDisposed || Disposing) return;
            ActiveControl = keepLocal;
            preview.SelectionStart = 0;
            preview.SelectionLength = 0;
            preview.ScrollToCaret();
        }));

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(send, UiGlyph.Link, primary: true);
        UiIcons.Assign(keepLocal, UiGlyph.Folder);
        UiIcons.TrackDpi(this);

        consent.CheckedChanged += (_, _) => UpdateButtons();
        send.Click += async (_, _) => await SendAsync();
        keepLocal.Click += (_, _) =>
        {
            if (busy)
            {
                sending?.Cancel();
                status.Text = UpdateUiText.T(
                    "Cancelling; check GitHub Issues before another attempt.",
                    "Отмена; перед повтором проверьте GitHub Issues.",
                    "מבטל; בדוק את GitHub לפני ניסיון חוזר.");
                return;
            }
            DialogResult = DialogResult.Cancel;
            Close();
        };
        FormClosing += (_, e) =>
        {
            if (!busy) return;
            sending?.Cancel();
            e.Cancel = true;
        };
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        send.Enabled = !busy && consent.Checked;
        keepLocal.Enabled = true;
    }

    private async Task SendAsync()
    {
        if (busy || !consent.Checked) return;
        if (!string.Equals(preview.Text, prepared.Json, StringComparison.Ordinal))
        {
            status.Text = UpdateUiText.T(
                "Report changed after review. Not sent.",
                "Отчёт изменился после просмотра. Не отправлен.",
                "הדוח השתנה ולא נשלח.");
            return;
        }
        busy = true;
        UpdateButtons();
        status.Text = UpdateUiText.T("Submitting to the shared Google gateway...",
            "Отправляю через общий Google-приёмник...",
            "שולח דרך שרת Google המשותף...");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        sending = cts;
        try
        {
            using var gateway = new GoogleDiagnosticsGateway();
            var issue = await gateway.SendAsync(prepared, cts.Token);
            // The remote receipt is authoritative. Never turn a confirmed
            // GitHub submission into a "not delivered" message just because
            // writing the local marker failed.
            ConfirmedIssue = issue;
            if (sourceFile is not null)
            {
                try { M365DiagnosticPayload.MarkDelivered(sourceFile, prepared.Id, issue); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    InvalidDataException or System.Text.Json.JsonException)
                {
                    // Best-effort local marker; no re-submission on this path.
                }
            }
        }
        catch (OperationCanceledException)
        {
            status.Text = UpdateUiText.T(
                "Delivery unconfirmed (cancelled or timed out). Review GitHub Issues before retrying; the report stays local.",
                "Доставка не подтверждена (отмена или тайм-аут). Перед повтором проверьте GitHub Issues. Отчёт сохранён.",
                "השליחה לא אומתה. בדוק את GitHub לפני ניסיון חוזר.");
        }
        catch (InvalidOperationException ex)
        {
            status.Text = ExplainGatewayFailure(ex.Message);
        }
        catch (Exception ex)
        {
            status.Text = UpdateUiText.T(
                "Delivery not confirmed (" + ex.GetType().Name + "). Report stays in the local queue. Check GitHub Issues before retrying.",
                "Доставка не подтверждена (" + ex.GetType().Name + "). Отчёт в очереди. Проверьте GitHub Issues перед повтором.",
                "השליחה לא אומתה (" + ex.GetType().Name + "). הדוח נשמר בתור.");
        }
        finally
        {
            sending = null;
            cts.Dispose();
            busy = false;
            if (!IsDisposed) UpdateButtons();
        }
        if (ConfirmedIssue is not null && !IsDisposed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private static string ExplainGatewayFailure(string message)
    {
        string code = message.StartsWith("GATEWAY_", StringComparison.Ordinal) &&
            message.Length <= 80 && message.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? message["GATEWAY_".Length..] : "unknown_error";

        string hint = code switch
        {
            "not_configured" => UpdateUiText.T(
                "Add GITHUB_M365_ISSUES_TOKEN to the deployed Apps Script project.",
                "Добавьте GITHUB_M365_ISSUES_TOKEN в свойства действующего Apps Script.",
                "יש להגדיר GITHUB_M365_ISSUES_TOKEN בפרויקט Google Apps Script."),
            "github_not_confirmed" => UpdateUiText.T(
                "GitHub did not accept the new Issue. Verify PAT repository access and Issues: Read and write in Apps Script.",
                "GitHub не принял создание Issue. Проверьте разрешения PAT для репозитория и Issues: Read and write.",
                "GitHub לא אישר יצירת Issue. בדוק הרשאות אסימון."),
            "DEPLOY_GATEWAY_V4" => UpdateUiText.T(
                "The receiver is older than the expanded M365 diagnostic contract. Deploy the latest Code.gs as a new version of the existing Apps Script deployment, then retry.",
                "Приёмник устарел. Опубликуйте обновлённый Code.gs как новую версию существующего развёртывания Apps Script и повторите.",
                "יש לפרוס גרסה חדשה של Code.gs באותה פריסת Apps Script לפני שליחה."),
            "invalid_evidence" => UpdateUiText.T(
                "The deployed gateway rejected the event schema. Check the installed Preview and gateway Code.gs version.",
                "Приёмник отклонил формат отчёта. Проверьте версии Preview и Code.gs.",
                "מבנה הדוח נדחה. בדוק גרסת היישום ושרת Google."),
            "daily_limit" => UpdateUiText.T(
                "The receiver has reached today's issue limit.",
                "Достигнут суточный лимит новых Issues.",
                "הושגה מכסת הדוחות היומית."),
            "busy" => UpdateUiText.T(
                "Receiver is busy. Check the queue and GitHub Issues before a retry.",
                "Приёмник занят. Перед повтором проверьте очередь и Issues.",
                "השרת עסוק. בדוק דוחות לפני ניסיון נוסף."),
            "temporarily_unavailable" => UpdateUiText.T(
                "The receiver failed internally. See Google Apps Script Executions.",
                "Внутренняя ошибка приёмника. Проверьте Google Apps Script → Executions.",
                "אירעה שגיאה בשרת. בדוק יומן הפעלות."),
            _ => UpdateUiText.T(
                "Review the Google Apps Script deployment and GitHub Issues.",
                "Проверьте Google Apps Script и GitHub Issues.",
                "בדוק את Google Apps Script ואת GitHub Issues.")
        };
        return "NOT DELIVERED: GATEWAY_" + code + ". " + hint +
            UpdateUiText.T(" Report stays in the local queue.",
                " Отчёт остаётся в локальной очереди.",
                " הדוח נשמר בתור המקומי.");
    }
}
