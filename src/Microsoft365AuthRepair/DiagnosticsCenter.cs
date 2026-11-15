using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft365AuthRepair.DiagnosticsEmbedded;

namespace Microsoft365AuthRepair;

internal sealed class DiagnosticsCenter : Form
{
    private const string Project = M365DiagnosticPayload.Project;
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly Button generate = UiTheme.CreateActionButton(
        UpdateUiText.T("Generate report", "Создать отчёт", "צור דוח"), true);
    private readonly Button load = UiTheme.CreateActionButton(
        UpdateUiText.T("Send saved report", "Отправить сохранённый отчёт", "שלח דוח שמור"));
    private readonly Button open = UiTheme.CreateActionButton(
        UpdateUiText.T("Open local queue", "Открыть очередь", "פתח תור מקומי"));
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private bool collecting;

    internal DiagnosticsCenter(bool uiSelfTest = false)
    {
        Text = $"Microsoft 365 Auth Repair v{Repair.Version} - " +
            UpdateUiText.T("Diagnostics & reports", "Диагностика и отчёты", "אבחון ודוחות");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(820, 380);
        MinimumSize = new Size(560, 310);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(820, 380),
            new Size(1050, 480), widthFraction: 0.76, heightFraction: 0.42);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, ColumnCount = 1, RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(UiTheme.Caption(UpdateUiText.T(
            "Diagnostics & reports", "Диагностика и отчёты", "אבחון ודוחות"), heading: true), 0, 0);
        var description = UiTheme.Caption(UpdateUiText.T(
            "Generate a local report and immediately review it in the Send anonymously dialog. No data is uploaded until you press Send.",
            "Создайте локальный отчёт — затем сразу откроется окно просмотра и отправки. Без нажатия «Отправить» данные не передаются.",
            "יצירת דוח מקומי תפתח חלון לבדיקת הדוח ושליחתו. אין שליחה ללא לחיצה על שלח."));
        description.MaximumSize = new Size(780, 0);
        root.Controls.Add(description, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 10, 0, 10)
        };
        actions.Controls.Add(generate);
        actions.Controls.Add(load);
        actions.Controls.Add(open);
        actions.Controls.Add(close);
        root.Controls.Add(actions, 0, 2);
        status.Text = UpdateUiText.T(
            "Nothing has been submitted. Reports stay in the local queue until GitHub confirms delivery.",
            "Ничего не отправлено. Отчёты остаются в локальной очереди до подтверждения GitHub.",
            "לא נשלח דבר. דוחות נשמרים מקומית עד לאישור GitHub.");
        status.MaximumSize = new Size(780, 0);
        root.Controls.Add(status, 0, 3);
        root.SizeChanged += (_, _) =>
        {
            int width = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 15);
            description.MaximumSize = new Size(width, 0);
            status.MaximumSize = new Size(width, 0);
        };
        Controls.Add(ResponsiveLayout.Center(this, root, 1000));

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(generate, UiGlyph.Reports, primary: true);
        UiIcons.Assign(load, UiGlyph.Folder);
        UiIcons.Assign(open, UiGlyph.Logs);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);

        generate.Click += async (_, _) => await GenerateAsync();
        load.Click += (_, _) => LoadPending();
        open.Click += (_, _) =>
        {
            try
            {
                var folder = UniversalDiagnosticsEmbedded.QueueDirectory(Project);
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
            }
            catch (Exception ex) { status.Text = "Open queue: " + ex.GetType().Name; }
        };
        close.Click += (_, _) => Close();

        if (uiSelfTest)
            status.Text = "UI-only diagnostics self-test; no data collected or sent.";
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    private void Review(PreparedDiagnostic payload, string? path)
    {
        if (IsDisposed || Disposing) return;
        status.Text = UpdateUiText.T("Review report in the confirmation dialog.",
            "Проверьте отчёт в окне подтверждения.", "בדוק דוח בחלון האישור.");
        using var confirmation = new ReportSendDialog(payload, path);
        if (confirmation.ShowDialog(this) == DialogResult.OK && confirmation.ConfirmedIssue is not null)
        {
            status.Text = "GitHub confirmed: " + confirmation.ConfirmedIssue.AbsoluteUri;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            status.Text = UpdateUiText.T(
                "Report kept locally. Delivery is not confirmed. Check GitHub Issues before retrying.",
                "Отчёт сохранён локально. Доставка не подтверждена. Перед повтором проверьте GitHub Issues.",
                "הדוח נשמר מקומית. השליחה לא אומתה. בדוק את GitHub לפני ניסיון נוסף.");
        }
    }

    private async Task GenerateAsync()
    {
        if (collecting || IsDisposed) return;
        collecting = true;
        generate.Enabled = load.Enabled = open.Enabled = false;
        status.Text = UpdateUiText.T(
            "Collecting local Entra/WAM diagnostics... No data is being uploaded.",
            "Собираю локальную диагностику Entra/WAM... Данные не отправляются.",
            "אוסף נתוני אבחון מקומיים. אין שליחה ברשת.");
        try
        {
            // AppX and dsregcmd can be slow. Keep the UI responsive.
            // Only the sanitized allowlist can be offered for publication.
            var summary = await Task.Run(
                () => M365DiagnosticPayload.SafeSummary(Repair.DiagnoseReport()));
            if (IsDisposed || Disposing) return;
            var capture = JsonSerializer.Serialize(new
            {
                project = Project,
                component = "EntraWAMBroker",
                error_type = "USER_REPORT", code = "WAM_REVIEW",
                level = "info", version = Repair.Version, summary
            });
            var receipt = UniversalDiagnosticsEmbedded.Capture(capture);
            Review(M365DiagnosticPayload.Prepare(File.ReadAllText(receipt.Path)), receipt.Path);
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                status.Text = "Diagnostic capture failed: " + ex.GetType().Name;
        }
        finally
        {
            collecting = false;
            if (!IsDisposed && !Disposing)
                generate.Enabled = load.Enabled = open.Enabled = true;
        }
    }

    private void LoadPending()
    {
        var folder = UniversalDiagnosticsEmbedded.QueueDirectory(Project);
        Directory.CreateDirectory(folder);
        using var picker = new OpenFileDialog
        {
            InitialDirectory = folder,
            Title = UpdateUiText.T("Open pending diagnostic JSON",
                "Открыть ожидающий отчёт JSON", "פתח דוח JSON ממתין"),
            Filter = "Diagnostic JSON (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var json = File.ReadAllText(picker.FileName);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("delivery", out var delivery) &&
                delivery.ValueKind == JsonValueKind.Object &&
                delivery.TryGetProperty("status", out var result) &&
                result.ValueKind == JsonValueKind.String &&
                result.GetString() == "submitted")
            {
                status.Text = UpdateUiText.T(
                    "Already marked submitted. Choose another pending report.",
                    "Уже отмечен как отправленный. Выберите другой отчёт.",
                    "הדוח כבר סומן כנשלח.");
                return;
            }
            Review(M365DiagnosticPayload.Prepare(json), picker.FileName);
        }
        catch (Exception ex)
        {
            status.Text = "Cannot open report: " + ex.GetType().Name;
        }
    }
}
