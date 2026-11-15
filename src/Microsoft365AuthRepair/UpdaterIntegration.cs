using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using UniversalUpdater;

namespace Microsoft365AuthRepair;

internal static class UpdaterIntegration
{
    internal const string Repository = "sgennadi/Microsoft365AuthRepair";
    internal static UpdateOptions Options(UpdateChannel channel) => new()
    {
        ProductId = "Microsoft365AuthRepair",
        Repository = Repository,
        InstalledVersion = Repair.Version,
        Architecture = UpdateOptions.CurrentWindowsArchitecture(),
        Channel = channel,
        Assets =
        [
            new UpdateAssetRule("win-x64", "Microsoft365AuthRepair-win-x64.zip", PackageKind.SingleExeZip, "Microsoft365AuthRepair.exe"),
            new UpdateAssetRule("win-arm64", "Microsoft365AuthRepair-win-arm64.zip", PackageKind.SingleExeZip, "Microsoft365AuthRepair.exe")
        ]
    };
}

internal sealed class UpdateCenter : Form
{
    private readonly ComboBox channel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Label state = UiTheme.Caption("No check has been performed.");
    private readonly TextBox releaseNotes = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Button check = UiTheme.CreateActionButton("Check for updates", true);
    private readonly Button stage = UiTheme.CreateActionButton("Download and verify");
    private readonly Button open = UiTheme.CreateActionButton("Open release page");
    private UpdateRelease? selected;
    private bool busy;

    internal UpdateCenter()
    {
        Text = "Microsoft 365 Auth Repair - UniversalUpdater";
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(540, 420);
        Size = new Size(670, 520);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        channel.Items.AddRange(["Stable", "Preview + stable"]);
        channel.SelectedIndex = 0;
        top.Controls.Add(channel);
        top.Controls.Add(check);
        top.Controls.Add(stage);
        top.Controls.Add(open);
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(state, 0, 1);
        root.Controls.Add(UiTheme.Caption("Release notes / What's new:"), 0, 2);
        root.Controls.Add(releaseNotes, 0, 3);
        Controls.Add(root);
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(check, UiGlyph.Updates, primary: true);
        UiIcons.Assign(stage, UiGlyph.Download);
        UiIcons.Assign(open, UiGlyph.Link);
        UiIcons.TrackDpi(this);
        stage.Enabled = open.Enabled = false;
        check.Click += async (_, _) => await CheckAsync();
        stage.Click += async (_, _) => await StageAsync();
        open.Click += (_, _) =>
        {
            if (selected is not null)
                Process.Start(new ProcessStartInfo(selected.ReleaseUrl) { UseShellExecute = true });
        };
    }

    private UpdateChannel Channel => channel.SelectedIndex == 0 ? UpdateChannel.Stable : UpdateChannel.Prerelease;
    private void Busy(bool flag)
    {
        busy = flag;
        check.Enabled = channel.Enabled = !flag;
        stage.Enabled = open.Enabled = !flag && selected is not null;
    }

    private async Task CheckAsync()
    {
        if (busy) return;
        Busy(true);
        state.Text = "Checking GitHub Releases...";
        selected = null;
        try
        {
            using var client = new GitHubReleaseClient();
            selected = await client.CheckAsync(UpdaterIntegration.Options(Channel));
            state.Text = selected is null ? "No newer compatible release (or repository has no published Releases)." :
                $"Available: {selected.Version} / {selected.Rule.Architecture} / SHA-256 metadata verified.";
            releaseNotes.Text = selected?.ReleaseNotes ?? "";
        }
        catch (Exception ex)
        {
            state.Text = "Check failed: " + ex.GetType().Name + ". Private repos need a separately configured trusted access path.";
        }
        finally { Busy(false); }
    }

    private async Task StageAsync()
    {
        if (busy || selected is null) return;
        Busy(true);
        state.Text = "Downloading and verifying SHA-256, ZIP contents and PE architecture...";
        try
        {
            using var downloader = new UpdateDownloader();
            var staged = await downloader.StageAsync(UpdaterIntegration.Options(Channel), selected);
            state.Text = "Verified download staged. No executable was replaced. Install is disabled in this preview until the trusted updater helper is tested.";
            releaseNotes.Text = "Verified ZIP: " + staged.ArchivePath + Environment.NewLine + Environment.NewLine + releaseNotes.Text;
        }
        catch (Exception ex) { state.Text = "Verification failed: " + ex.GetType().Name + "."; }
        finally { Busy(false); }
    }
}
