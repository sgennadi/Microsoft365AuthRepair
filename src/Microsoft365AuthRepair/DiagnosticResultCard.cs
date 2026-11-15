using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// A compact, accessible and DPI-aware summary of the last read-only diagnosis.
// Technical details are kept separately, behind a small disclosure link.
// No repair or HTTP action originates from this control.
internal sealed class DiagnosticResultCard : TableLayoutPanel
{
    private readonly PictureBox glyph = UiIcons.CreateStatus();
    private readonly Label headline = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label findings = UiTheme.Caption(string.Empty);
    private readonly Label nextSteps = UiTheme.Caption(string.Empty);

    internal DiagnosticResultCard()
    {
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 2;
        RowCount = 3;
        Padding = new Padding(16, 12, 16, 12);
        Margin = new Padding(0, 9, 0, 4);
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.White;
        Visible = false;

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < 3; i++)
            RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headline.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        headline.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
        findings.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Muted;
        nextSteps.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
        findings.Margin = new Padding(0, 9, 0, 5);
        nextSteps.Margin = new Padding(0, 5, 0, 4);
        headline.AccessibleName = "Diagnostic result";
        findings.AccessibleName = "Diagnostic findings";
        nextSteps.AccessibleName = "Recommended next steps";

        Controls.Add(glyph, 0, 0);
        Controls.Add(headline, 1, 0);
        Controls.Add(findings, 1, 1);
        Controls.Add(nextSteps, 1, 2);

        SizeChanged += (_, _) => WrapLabels();
    }

    private void WrapLabels()
    {
        // WinForms AutoSize labels need a dynamic maximum width to wrap instead
        // of extending a TableLayoutPanel past the right edge at 150-200% DPI.
        int width = Math.Max(160, ClientSize.Width - Padding.Horizontal - 50);
        var limit = new Size(width, 0);
        if (headline.MaximumSize != limit) headline.MaximumSize = limit;
        if (findings.MaximumSize != limit) findings.MaximumSize = limit;
        if (nextSteps.MaximumSize != limit) nextSteps.MaximumSize = limit;
    }

    internal void ShowResult(WamHealthResult report)
    {
        headline.Text = report.Summary;
        var foundHeader = UpdateUiText.T("WHAT WE FOUND", "ЧТО ОБНАРУЖЕНО", "מה נמצא");
        var stepsHeader = UpdateUiText.T("WHAT TO DO NEXT", "ЧТО ДЕЛАТЬ ДАЛЬШЕ", "מה לעשות עכשיו");

        // The full report retains every signal and recommendation. Show only
        // the most useful concise results, without an overwhelming raw dump.
        var shownFindings = report.Findings.Take(2).ToArray();
        findings.Text = foundHeader + Environment.NewLine +
            string.Join(Environment.NewLine, shownFindings.Select(v => "• " + v));
        nextSteps.Text = stepsHeader + Environment.NewLine +
            string.Join(Environment.NewLine, report.NextSteps.Take(2)
                .Select((v, i) => (i + 1) + ". " + v));

        UiIcons.SetStatus(glyph, report.Level switch
        {
            WamHealthLevel.Healthy => UiGlyph.Success,
            WamHealthLevel.RepairRecommended => UiGlyph.Error,
            _ => UiGlyph.Info
        }, report.Summary);
        Visible = true;
        WrapLabels();
    }

    internal void ClearResult() => Visible = false;

    internal bool HasSummary => Visible && !string.IsNullOrWhiteSpace(headline.Text)
        && !string.IsNullOrWhiteSpace(nextSteps.Text);

    internal static bool SelfTest()
    {
        using var card = new DiagnosticResultCard();
        const string healthy = "BrokerPlugin: PRESENT (1.0)\\nAzureAdJoined: YES\\n" +
            "DomainJoined: YES\\nDeviceAuthStatus: SUCCESS\\nAzureAdPrt: YES\\nWamDefaultSet: YES";
        var report = WamHealthAnalysis.Evaluate(healthy.Replace("\\n", Environment.NewLine), true);
        card.ShowResult(report);
        return card.HasSummary && card.Controls.Count == 4 &&
            card.RowCount == 3 && card.findings.Text.Contains("BrokerPlugin", StringComparison.OrdinalIgnoreCase) == false &&
            !string.IsNullOrWhiteSpace(card.nextSteps.Text);
    }
}
