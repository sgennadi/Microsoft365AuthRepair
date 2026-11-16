using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// One responsive result surface: sign-in, TPM, device keys and safe next steps.
// Never use raw dsregcmd text in a tile; the technical report is separate.
internal sealed class DiagnosticResultCard : TableLayoutPanel
{
    private readonly PictureBox glyph = UiIcons.CreateStatus();
    private readonly Label headline = UiTheme.Caption(string.Empty, heading: true);
    private readonly TableLayoutPanel signalGrid = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0, 9, 0, 7),
        Padding = Padding.Empty
    };
    private readonly Label findings = UiTheme.Caption(string.Empty);
    private readonly Label nextSteps = UiTheme.Caption(string.Empty);
    private readonly List<SignalTile> tiles = new();
    private bool reshaping;
    private int displayedColumns;

    private sealed class SignalTile : TableLayoutPanel
    {
        internal readonly Label Title = UiTheme.Caption(string.Empty);
        internal readonly Label Value = UiTheme.Caption(string.Empty);

        internal SignalTile()
        {
            Dock = DockStyle.Fill;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(3, 3, 6, 3);
            Padding = new Padding(10, 7, 10, 7);
            ColumnCount = 1;
            RowCount = 2;
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(247, 249, 252);
            BorderStyle = BorderStyle.FixedSingle;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Title.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Muted;
            Value.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
            Value.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            Title.Margin = new Padding(0, 0, 0, 3);
            Value.Margin = Padding.Empty;
            Controls.Add(Title, 0, 0);
            Controls.Add(Value, 0, 1);
        }

        internal void Set(WamHealthSignal signal)
        {
            Title.Text = signal.Label;
            string prefix = signal.State switch
            {
                WamSignalState.Good => "✓ ",
                WamSignalState.Attention => "! ",
                WamSignalState.Neutral => "• ",
                _ => "? "
            };
            Value.Text = prefix + signal.Value;
            Value.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : signal.State switch
            {
                WamSignalState.Good => UiTheme.Positive,
                WamSignalState.Attention => Color.FromArgb(158, 48, 60),
                WamSignalState.Neutral => UiTheme.Muted,
                _ => UiTheme.Muted
            };
            AccessibleName = signal.Label + ": " + signal.Value;
            AccessibleDescription = signal.State switch
            {
                WamSignalState.Good => "Verified positive signal",
                WamSignalState.Attention => "May require attention",
                WamSignalState.Neutral => "Neutral: depends on the device's expected configuration",
                _ => "Unverified: no reliable status"
            };
        }
    }

    internal DiagnosticResultCard()
    {
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 2;
        RowCount = 4;
        Padding = new Padding(14, 12, 14, 12);
        Margin = new Padding(0, 9, 0, 4);
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.White;
        Visible = false;

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < 4; i++)
            RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headline.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        headline.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
        findings.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Muted;
        nextSteps.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
        findings.Margin = new Padding(0, 5, 0, 5);
        nextSteps.Margin = new Padding(0, 7, 0, 4);
        headline.AccessibleName = "Diagnostic result";
        findings.AccessibleName = "Diagnostic findings";
        nextSteps.AccessibleName = "Recommended next steps";

        Controls.Add(glyph, 0, 0);
        Controls.Add(headline, 1, 0);
        Controls.Add(signalGrid, 1, 1);
        Controls.Add(findings, 1, 2);
        Controls.Add(nextSteps, 1, 3);
        for (var i = 0; i < 10; i++) tiles.Add(new SignalTile());
        SizeChanged += (_, _) => UpdateGeometry();
        signalGrid.SizeChanged += (_, _) => UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        if (reshaping) return;
        reshaping = true;
        try
        {
            int available = Math.Max(160, ClientSize.Width - Padding.Horizontal - 55);
            var limit = new Size(available, 0);
            if (headline.MaximumSize != limit) headline.MaximumSize = limit;
            if (findings.MaximumSize != limit) findings.MaximumSize = limit;
            if (nextSteps.MaximumSize != limit) nextSteps.MaximumSize = limit;

            int columns = available >= 620 * Math.Max(96, DeviceDpi) / 96 ? 2 : 1;
            if (displayedColumns != columns)
            {
                displayedColumns = columns;
                signalGrid.SuspendLayout();
                signalGrid.Controls.Clear();
                signalGrid.ColumnStyles.Clear();
                signalGrid.RowStyles.Clear();
                signalGrid.ColumnCount = columns;
                signalGrid.RowCount = (tiles.Count + columns - 1) / columns;
                for (int i = 0; i < columns; i++)
                    signalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                for (int i = 0; i < signalGrid.RowCount; i++)
                    signalGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (int i = 0; i < tiles.Count; i++)
                    signalGrid.Controls.Add(tiles[i], i % columns, i / columns);
                signalGrid.ResumeLayout(true);
            }

            // No fixed pixel tile widths. Reflow is based on physical DPI and
            // parent width to avoid clipped Hebrew/Russian labels at 200%.
            var tileWidth = Math.Max(140, (available / columns) - 27);
            foreach (var tile in tiles)
            {
                var tileMax = new Size(Math.Max(110, tileWidth - tile.Padding.Horizontal), 0);
                if (tile.Title.MaximumSize != tileMax) tile.Title.MaximumSize = tileMax;
                if (tile.Value.MaximumSize != tileMax) tile.Value.MaximumSize = tileMax;
            }
        }
        finally { reshaping = false; }
    }

    internal void ShowResult(WamHealthResult report)
    {
        headline.Text = report.Summary;
        foreach (var pair in tiles.Select((tile, i) => (tile, i)))
        {
            if (pair.i < report.Signals.Count)
                pair.tile.Set(report.Signals[pair.i]);
        }
        var shownFindings = report.Level == WamHealthLevel.Healthy
            ? Array.Empty<string>() : report.Findings.Take(2).ToArray();
        findings.Visible = shownFindings.Length > 0;
        findings.Text = shownFindings.Length == 0 ? string.Empty :
            UpdateUiText.T("WHAT WE FOUND", "ЧТО ОБНАРУЖЕНО", "מה נמצא") +
            Environment.NewLine + string.Join(Environment.NewLine,
                shownFindings.Select(v => "• " + v));
        nextSteps.Text = UpdateUiText.T("WHAT TO DO NEXT", "ЧТО ДЕЛАТЬ ДАЛЬШЕ", "מה לעשות עכשיו") +
            Environment.NewLine + string.Join(Environment.NewLine,
                report.NextSteps.Take(2).Select((v, i) => (i + 1) + ". " + v));
        UiIcons.SetStatus(glyph, report.Level switch
        {
            WamHealthLevel.Healthy => UiGlyph.Success,
            WamHealthLevel.RepairRecommended => UiGlyph.Error,
            _ => UiGlyph.Info
        }, report.Summary);
        Visible = true;
        UpdateGeometry();
    }

    internal void ClearResult() => Visible = false;

    internal bool HasSummary => Visible && !string.IsNullOrWhiteSpace(headline.Text)
        && !string.IsNullOrWhiteSpace(nextSteps.Text) && tiles.Count == 10;

    internal static bool SelfTest()
    {
        using var card = new DiagnosticResultCard();
        const string healthy = "BrokerPlugin: PRESENT (1.0)\nAzureAdJoined: YES\n" +
            "DomainJoined: YES\nDeviceAuthStatus: SUCCESS\nAzureAdPrt: YES\nWamDefaultSet: YES";
        card.ShowResult(WamHealthAnalysis.Evaluate(healthy, true));
        return card.HasSummary && card.Controls.Count == 5 &&
            card.RowCount == 4 && card.signalGrid.Controls.Count == 10 &&
            card.tiles.All(t => !string.IsNullOrWhiteSpace(t.Value.Text)) &&
            card.tiles[1].Title.Text.Length > 0 &&
            card.tiles[6].Title.Text == "TPM" &&
            card.tiles[9].Title.Text == "BitLocker";
    }
}
