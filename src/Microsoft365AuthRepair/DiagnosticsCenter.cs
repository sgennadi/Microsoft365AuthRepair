using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft365AuthRepair.DiagnosticsEmbedded;

namespace Microsoft365AuthRepair;

internal sealed class DiagnosticsCenter : Form
{
    private const string Project = "sgennadi/Microsoft365AuthRepair";
    private readonly TextBox preview = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Vertical
    };
    private readonly Label status = UiTheme.Caption("No report has been captured or transmitted.");
    private readonly Button save = UiTheme.CreateActionButton("Save reviewed report locally", true);
    private readonly Button open = UiTheme.CreateActionButton("Open report queue");

    internal DiagnosticsCenter()
    {
        Text = "Microsoft 365 Auth Repair - UniversalDiagnostics";
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(740, 525);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(UiTheme.Caption("Review this sanitized report before saving. No report is sent automatically."), 0, 0);
        root.Controls.Add(preview, 0, 1);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        buttons.Controls.Add(save);
        buttons.Controls.Add(open);
        root.Controls.Add(buttons, 0, 2);
        root.Controls.Add(status, 0, 3);
        Controls.Add(root);
        preview.Text = BuildPreview();
        save.Click += (_, _) => SaveReviewedReport();
        open.Click += (_, _) =>
        {
            var path = UniversalDiagnosticsEmbedded.QueueDirectory(Project);
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        };
    }

    private static string BuildPreview()
    {
        var diagnostic = Repair.DiagnoseReport();
        var facts = diagnostic.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => new[] { "DomainJoined ", "AzureAdJoined ", "DeviceAuthStatus ", "AzureAdPrt ", "WamDefaultSet " }
                .Any(key => line.StartsWith(key, StringComparison.Ordinal)))
            .Select(x => x.Length > 96 ? x[..96] : x);
        var summary = string.Join("; ", facts);
        if (summary.Length == 0) summary = "Read-only WAM/Entra status not available";
        return JsonSerializer.Serialize(new
        {
            project = Project,
            component = "Entra WAM / AAD BrokerPlugin",
            version = Repair.Version,
            error_type = "USER_REPORT",
            code = "WAM_REVIEW",
            level = "info",
            summary = summary.Length > 420 ? summary[..420] : summary
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private void SaveReviewedReport()
    {
        try
        {
            // This is the canonical embedded UniversalDiagnostics capture implementation, not a separate module.
            var receipt = UniversalDiagnosticsEmbedded.Capture(preview.Text);
            status.Text = "Saved locally: " + receipt.Id + ". Not submitted to a server.";
        }
        catch (Exception ex) { status.Text = "Local capture failed: " + ex.GetType().Name; }
    }
}
