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
        ScrollBars = ScrollBars.Vertical, WordWrap = false,
        Font = new Font("Consolas", 9f), BackColor = SystemColors.Window
    };
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly CheckBox consent = new()
    {
        AutoSize = true, Text = "I approve sending these reviewed fields to a PUBLIC GitHub Issue.",
        Margin = new Padding(0, 8, 0, 4)
    };
    private readonly Button generate = UiTheme.CreateActionButton("Generate report", true);
    private readonly Button load = UiTheme.CreateActionButton("Load pending JSON");
    private readonly Button open = UiTheme.CreateActionButton("Open local queue");
    private readonly Button send = UiTheme.CreateActionButton("Send anonymously", true);
    private readonly Button close = UiTheme.CreateActionButton("Close");

    private PreparedDiagnostic? prepared;
    private string? sourceFile;
    private CancellationTokenSource? sending;
    private bool busy;

    internal DiagnosticsCenter(bool uiSelfTest = false)
    {
        Text = $"Microsoft 365 Auth Repair v{Repair.Version} - Diagnostics & Reports";
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(770, 600);
        MinimumSize = new Size(620, 480);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, ColumnCount = 1, RowCount = 7
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = UiTheme.Caption("Diagnostics & Reports", true);
        heading.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(UiTheme.Caption(
            "Preview exactly what will be sent. Only typed WAM/Entra states; no usernames, tokens, logs or free text."), 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 10, 0, 10)
        };
        actions.Controls.Add(generate);
        actions.Controls.Add(load);
        actions.Controls.Add(open);
        root.Controls.Add(actions, 0, 2);

        root.Controls.Add(consent, 0, 3);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true
        };
        footer.Controls.Add(send);
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 4);
        status.Text = "Nothing has been submitted. Generate or load a report to review.";
        root.Controls.Add(status, 0, 5);
        root.Controls.Add(preview, 0, 6);
        Controls.Add(root);

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(generate, UiGlyph.Reports, primary: true);
        UiIcons.Assign(load, UiGlyph.Folder);
        UiIcons.Assign(open, UiGlyph.Logs);
        UiIcons.Assign(send, UiGlyph.Link, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);

        consent.CheckedChanged += (_, _) => UpdateSendState();
        generate.Click += (_, _) => Generate();
        load.Click += (_, _) => LoadPending();
        open.Click += (_, _) =>
        {
            var folder = UniversalDiagnosticsEmbedded.QueueDirectory(Project);
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        };
        send.Click += async (_, _) => await SendAsync();
        close.Click += (_, _) => Close();
        FormClosing += (_, _) => sending?.Cancel();

        preview.Text = uiSelfTest
            ? "UI-only diagnostics self-test. No diagnostic data collected or sent."
            : "Click Generate report to collect read-only WAM/Entra status; then inspect the exact outgoing JSON.";
        consent.Checked = false;
        UpdateSendState();
    }

    private void UpdateSendState()
    {
        send.Enabled = !busy && consent.Checked && prepared is not null;
        generate.Enabled = load.Enabled = open.Enabled = !busy;
        close.Enabled = !busy;
    }

    private void SetPrepared(PreparedDiagnostic item, string? path)
    {
        prepared = item;
        sourceFile = path;
        preview.Text = item.Json;
        consent.Checked = false;
        status.Text = "Ready for review. Nothing has been sent.";
        UpdateSendState();
    }

    private void Generate()
    {
        if (busy) return;
        try
        {
            // Local-only read-only device status; account details are discarded.
            var summary = M365DiagnosticPayload.SafeSummary(Repair.DiagnoseReport());
            var capture = JsonSerializer.Serialize(new
            {
                project = Project,
                component = "EntraWAMBroker",
                error_type = "USER_REPORT", code = "WAM_REVIEW",
                level = "info", version = Repair.Version, summary
            });
            var receipt = UniversalDiagnosticsEmbedded.Capture(capture);
            var item = M365DiagnosticPayload.Prepare(File.ReadAllText(receipt.Path));
            SetPrepared(item, receipt.Path);
        }
        catch (Exception ex)
        {
            status.Text = "Diagnostic capture failed: " + ex.GetType().Name;
        }
    }

    private void LoadPending()
    {
        if (busy) return;
        var folder = UniversalDiagnosticsEmbedded.QueueDirectory(Project);
        Directory.CreateDirectory(folder);
        using var picker = new OpenFileDialog
        {
            InitialDirectory = folder,
            Title = "Open reviewed UniversalDiagnostics JSON",
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
                delivery.TryGetProperty("status", out var result) &&
                result.GetString() == "submitted")
            {
                status.Text = "This report is already marked submitted. Choose another pending report.";
                return;
            }
            SetPrepared(M365DiagnosticPayload.Prepare(json), picker.FileName);
        }
        catch (Exception ex)
        {
            status.Text = "Cannot open report: " + ex.GetType().Name;
        }
    }

    private async Task SendAsync()
    {
        if (busy || !consent.Checked || prepared is null) return;
        // Freeze the exact reviewed bytes; do not recapture/alter after consent.
        var agreed = prepared;
        if (!string.Equals(preview.Text, agreed.Json, StringComparison.Ordinal))
        {
            status.Text = "Report changed after review; no data sent.";
            return;
        }
        busy = true;
        UpdateSendState();
        status.Text = "Sending reviewed metadata through the shared Google gateway...";
        sending = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            using var gateway = new GoogleDiagnosticsGateway();
            var issue = await gateway.SendAsync(agreed, sending.Token);
            if (sourceFile is not null)
                M365DiagnosticPayload.MarkDelivered(sourceFile, agreed.Id, issue);
            status.Text = "GitHub confirmed issue: " + issue.AbsoluteUri;
            DialogResult = DialogResult.OK;
            Close(); // Only confirmed success closes automatically.
        }
        catch (OperationCanceledException)
        {
            status.Text = "Send cancelled or timed out; check GitHub Issues before retrying.";
        }
        catch (Exception ex)
        {
            status.Text = "Not confirmed: " + ex.Message;
        }
        finally
        {
            sending.Dispose();
            sending = null;
            busy = false;
            UpdateSendState();
        }
    }
}
