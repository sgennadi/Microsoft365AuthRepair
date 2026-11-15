using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

internal sealed class MainForm : Form
{
    private readonly bool elevated = Identity.IsElevated();
    private readonly Label title = UiTheme.Caption(string.Empty, true);
    private readonly Label subtitle = UiTheme.Caption(string.Empty);
    private readonly Label account = UiTheme.Caption(string.Empty);
    private readonly Label privilege = UiTheme.Caption(string.Empty);
    private readonly Label scopeLabel = UiTheme.Caption(string.Empty);
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly PictureBox statusGlyph = UiIcons.CreateStatus();
    private readonly Label advancedExplanation = UiTheme.Caption(string.Empty);
    private readonly ComboBox scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Margin = new Padding(0, 4, 0, 8) };
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Margin = new Padding(10, 2, 0, 0) };
    private readonly Button repair = UiTheme.CreateActionButton(string.Empty, primary: true);
    private readonly Button reset = UiTheme.CreateActionButton(string.Empty, destructive: true);
    private readonly Button diagnose = UiTheme.CreateActionButton(string.Empty);
    private readonly Button updates = UiTheme.CreateActionButton(string.Empty);
    private readonly Button diagnostics = UiTheme.CreateActionButton(string.Empty);
    private readonly Button logs = UiTheme.CreateActionButton(string.Empty);
    private readonly CheckBox showAdvanced = new() { Text = "Advanced", AutoSize = true, Margin = new Padding(0, 7, 0, 4) };
    private readonly Panel advancedPanel = new() { Dock = DockStyle.Fill, AutoSize = true, Visible = false };
    private readonly TextBox detail = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both,
        WordWrap = false, Margin = new Padding(0, 6, 0, 0), Font = new Font("Consolas", 9f)
    };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 5, Visible = false, Style = ProgressBarStyle.Marquee };
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private bool busy;

    internal MainForm()
    {
        Text = "Microsoft 365 Auth Repair";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(610, 460);
        Size = new Size(795, 655);
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(Size.Width, area.Width), Math.Min(Size.Height, area.Height));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true,
            ColumnCount = 1, RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill, AutoSize = true };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(UiIcons.CreateBrand(), 0, 0);
        title.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.Add(title, 1, 0);
        language.Items.AddRange(["English", "Русский", "עברית"]);
        language.SelectedIndex = UiStrings.Language switch { "ru" => 1, "he" => 2, _ => 0 };
        top.Controls.Add(language, 2, 0);
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(account, 0, 2);

        var target = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        target.Controls.Add(scopeLabel);
        target.Controls.Add(scope);
        if (elevated)
        {
            scope.Items.AddRange([UiStrings.T("current"), UiStrings.T("all")]);
            scope.SelectedIndex = 0;
        }
        else
        {
            scope.Items.Add(UiStrings.T("current"));
            scope.SelectedIndex = 0;
            scope.Enabled = false;
        }
        target.Controls.Add(privilege);
        root.Controls.Add(target, 0, 3);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.Add(repair);
        actions.Controls.Add(diagnose);
        actions.Controls.Add(updates);
        actions.Controls.Add(diagnostics);
        actions.Controls.Add(logs);
        root.Controls.Add(actions, 0, 4);

        var advanced = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        advanced.Controls.Add(showAdvanced, 0, 0);
        var advancedInner = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true };
        advancedInner.Controls.Add(reset);
        advancedInner.Controls.Add(advancedExplanation);
        advancedPanel.Controls.Add(advancedInner);
        advanced.Controls.Add(advancedPanel, 0, 1);
        root.Controls.Add(advanced, 0, 5);

        var banner = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(10, 8, 10, 8) };
        if (!SystemInformation.HighContrast) banner.BackColor = Color.FromArgb(231, 239, 248);
        banner.Controls.Add(statusGlyph);
        banner.Controls.Add(status);
        root.Controls.Add(banner, 0, 6);
        root.Controls.Add(detail, 0, 7);
        Controls.Add(root);
        Controls.Add(progress);
        progress.BringToFront();

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(repair, UiGlyph.Repair, primary: true);
        UiIcons.Assign(reset, UiGlyph.Reset, destructive: true);
        UiIcons.Assign(diagnose, UiGlyph.Search);
        UiIcons.Assign(diagnostics, UiGlyph.Reports);
        UiIcons.Assign(updates, UiGlyph.Updates);
        UiIcons.Assign(logs, UiGlyph.Logs);
        UiIcons.TrackDpi(this);
        var hints = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500, ReshowDelay = 200 };
        hints.SetToolTip(repair, "Re-register WAM / AAD Broker Plugin without clearing user tokens.");
        hints.SetToolTip(reset, "Advanced: remove Office and WAM identity caches for the selected user scope.");
        hints.SetToolTip(diagnose, "Check Hybrid Join, device authentication, PRT and WAM status.");
        hints.SetToolTip(diagnostics, "Review a sanitized diagnostic report before saving it locally.");
        hints.SetToolTip(updates, "Check Stable / Preview releases and verify downloaded packages.");
        hints.SetToolTip(logs, "Open local repair logs without sending any data.");

        showAdvanced.CheckedChanged += (_, _) => advancedPanel.Visible = showAdvanced.Checked;
        repair.Click += async (_, _) => await RunRepairAsync(false);
        reset.Click += async (_, _) => await RunRepairAsync(true);
        diagnose.Click += async (_, _) => await RunDiagnoseAsync();
        diagnostics.Click += (_, _) => new DiagnosticsCenter().ShowDialog(this);
        updates.Click += (_, _) => new UpdateCenter().ShowDialog(this);
        logs.Click += (_, _) => OpenLogs();
        language.SelectedIndexChanged += (_, _) =>
        {
            var index = language.SelectedIndex;
            UiStrings.Language = index == 1 ? "ru" : index == 2 ? "he" : "en";
            ApplyText();
        };
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        Shown += async (_, _) =>
        {
            updateTimer.Start();
            await CheckForUpdatesAsync();
        };
        ApplyText();
    }

    private void ApplyText()
    {
        title.Text = UiStrings.T("title");
        subtitle.Text = UiStrings.T("subtitle");
        account.Text = $"{Environment.UserDomainName}\\{Environment.UserName} | Session {Identity.SessionId()} | {(elevated ? "Administrator" : "Standard user")}";
        privilege.Text = UiStrings.T(elevated ? "admin" : "standard");
        scopeLabel.Text = UiStrings.T("scope") + ": ";
        var previous = scope.SelectedIndex < 0 ? 0 : scope.SelectedIndex;
        scope.Items.Clear();
        scope.Items.Add(UiStrings.T("current"));
        if (elevated) scope.Items.Add(UiStrings.T("all"));
        scope.SelectedIndex = Math.Min(previous, scope.Items.Count - 1);
        repair.Text = UiStrings.T("repair");
        reset.Text = UiStrings.T("full");
        diagnose.Text = UiStrings.T("diagnose");
        diagnostics.Text = UiStrings.T("debug");
        updates.Text = UiStrings.T("updater");
        logs.Text = UiStrings.T("logs");
        advancedExplanation.Text = UiStrings.T("advanced");
        if (string.IsNullOrWhiteSpace(status.Text)) ShowStatus(UiStrings.T("statusReady"), UiGlyph.Info);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    private void ShowStatus(string message, UiGlyph glyph)
    {
        status.Text = message;
        UiIcons.SetStatus(statusGlyph, glyph, message);
    }

    private void SetBusy(bool isBusy)
    {
        busy = isBusy;
        repair.Enabled = reset.Enabled = diagnose.Enabled = diagnostics.Enabled = updates.Enabled = !isBusy;
        scope.Enabled = elevated && !isBusy;
        progress.Visible = isBusy;
    }

    private async Task RunRepairAsync(bool full)
    {
        if (busy) return;
        var everyone = elevated && scope.SelectedIndex == 1;
        if (full)
        {
            var warning = everyone ? UiStrings.T("confirmAll") : UiStrings.T("confirmCurrent");
            if (MessageBox.Show(this, warning, UiStrings.T("confirmTitle"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        }
        SetBusy(true);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            var result = await Task.Run(() =>
            {
                if (!elevated) return full ? Repair.Run(quiet: true) == 0 ? "SUCCESS" : "FAILED" :
                    Repair.ReRegister(quiet: true) == 0 ? "SUCCESS" : "FAILED";
                return ScheduleAdminRepair(everyone, full);
            });
            detail.AppendText(result + Environment.NewLine);
            var failed = result.Contains("FAILED", StringComparison.Ordinal);
            ShowStatus(failed ? UiStrings.T("failed") : elevated
                ? "Scheduled. Per-user completion must be checked in each user log."
                : UiStrings.T("done"), failed ? UiGlyph.Error : UiGlyph.Success);
        }
        catch (Exception ex)
        {
            ShowStatus(UiStrings.T("failed"), UiGlyph.Error);
            detail.AppendText(ex.GetType().Name + ": " + ex.Message + Environment.NewLine);
        }
        finally { SetBusy(false); }
    }

    private static string ScheduleAdminRepair(bool everyone, bool full)
    {
        var profiles = UserProfiles.Enumerate();
        var sessions = Native.FindExplorerSessions();
        var selected = profiles;
        if (!everyone)
        {
            if (!sessions.TryGetValue(Identity.SessionId(), out var owners) || owners.Count != 1)
                throw new InvalidOperationException("The active desktop owner could not be determined uniquely. Run the program as the affected user.");
            selected = profiles.Where(p => owners.Contains(p.Sid)).ToList();
            if (selected.Count != 1) throw new InvalidOperationException("The desktop user profile could not be matched.");
        }
        if (selected.Count == 0) throw new InvalidOperationException("No suitable user profiles found.");
        Scheduling.PrepareProtectedExe();
        var onlineSids = sessions.Values.SelectMany(s => s).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();
        foreach (var profile in selected)
        {
            try { results.Add(Scheduling.QueueForSid(profile, onlineSids.Contains(profile.Sid), full ? "full" : "reregister")); }
            catch (Exception ex) { results.Add("FAILED to queue " + profile.Account + ": " + ex.GetType().Name); }
        }
        return string.Join(Environment.NewLine, results);
    }

    private async Task RunDiagnoseAsync()
    {
        if (busy) return;
        SetBusy(true);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            detail.Text = await Task.Run(Repair.DiagnoseReport);
            ShowStatus(UiStrings.T("statusReady"), UiGlyph.Info);
        }
        catch (Exception ex)
        {
            ShowStatus(UiStrings.T("failed"), UiGlyph.Error);
            detail.Text = ex.GetType().Name;
        }
        finally { SetBusy(false); }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (busy) return;
        try
        {
            using var client = new UniversalUpdater.GitHubReleaseClient();
            var release = await client.CheckAsync(UpdaterIntegration.Options(UniversalUpdater.UpdateChannel.Stable));
            if (release is not null) updates.Text = UiStrings.T("updater") + " *";
        }
        catch (Exception) { /* Updater must not block WAM repair or expose credentials. */ }
    }

    private static void OpenLogs()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft365AuthRepair", "Logs");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}
