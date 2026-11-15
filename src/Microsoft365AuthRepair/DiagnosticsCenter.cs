using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft365AuthRepair.DiagnosticsEmbedded;

namespace Microsoft365AuthRepair;

internal sealed class DiagnosticsCenter : Form
{
    private const string Project = M365DiagnosticPayload.Project;
    private readonly TextBox preview = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Both, WordWrap = false,
        Font = new Font("Consolas", 9f), BackColor = SystemColors.Window
    };
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly Button generate = UiTheme.CreateActionButton(
        UpdateUiText.T("Generate report", "Создать отчёт", "צור דוח"), true);
    private readonly Button load = UiTheme.CreateActionButton(
        UpdateUiText.T("Load pending JSON", "Открыть ожидающий JSON", "טען דוח ממתין"));
    private readonly Button open = UiTheme.CreateActionButton(
        UpdateUiText.T("Open local queue", "Открыть очередь", "פתח תור מקומי"));
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));

    internal DiagnosticsCenter(bool uiSelfTest = false)
    {
        Text = $"Microsoft 365 Auth Repair v{Repair.Version} - " +
            UpdateUiText.T("Diagnostics & reports", "Диагностика и отчёты", "אבחון ודוחות");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(860, 610);
        MinimumSize = new Size(660, 460);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, ColumnCount = 1, RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
        root.Controls.Add(preview, 0, 4);
        Controls.Add(root);

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(generate, UiGlyph.Reports, primary: true);
        UiIcons.Assign(load, UiGlyph.Folder);
        UiIcons.Assign(open, UiGlyph.Logs);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);

        generate.Click += (_, _) => Generate();
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

        preview.Text = uiSelfTest
            ? "UI-only diagnostics self-test; no data collected or sent."
            : UpdateUiText.T(
                "Click Generate report or Load pending JSON. The outgoing allowlisted JSON will be shown here and in the review dialog.",
                "Нажмите «Создать отчёт» или «Открыть ожидающий JSON». JSON для отправки появится здесь и в окне подтверждения.",
                "לחץ על צור דוח או טען דוח ממתין. הדוח יופיע כאן ובחלון האישור.");
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    private void Review(PreparedDiagnostic payload, string? path)
    {
        preview.Text = payload.Json;
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

    private void Generate()
    {
        generate.Enabled = false;
        try
        {
            // Read-only Windows diagnostics. The raw report is never uploaded.
            var summary = M365DiagnosticPayload.SafeSummary(Repair.DiagnoseReport());
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
            status.Text = "Diagnostic capture failed: " + ex.GetType().Name;
        }
        finally { if (!IsDisposed) generate.Enabled = true; }
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
