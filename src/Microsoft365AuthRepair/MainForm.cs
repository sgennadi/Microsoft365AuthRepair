using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

internal sealed class MainForm : Form
{
    private readonly bool elevated = Identity.IsElevated();
    private readonly DesktopUser desktopUser = UserScopeSupport.Resolve();
    private readonly Label title = UiTheme.Caption(string.Empty, true);
    private readonly Label subtitle = UiTheme.Caption(string.Empty);
    private readonly Label account = UiTheme.Caption(string.Empty);
    private readonly Label privilege = UiTheme.Caption(string.Empty);
    private readonly Label scopeLabel = UiTheme.Caption(string.Empty);
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly PictureBox statusGlyph = UiIcons.CreateStatus();
    private readonly DiagnosticResultCard diagnosticResult = new();
    private readonly DeviceSecurityCard deviceSecurityCard = new();
    private readonly Label officeLicenseSummary = UiTheme.Caption(string.Empty);
    private readonly LinkLabel officeVolumeLink = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(7, 4, 0, 8)
    };
    private OfficeLicenseSnapshot? lastOfficeLicense;
    private OfficeActivationHealth? lastOfficeActivation;
    private RepairPreflight? lastPreflight;
    private TpmBitLockerSnapshot? lastDeviceProtection;
    // The device card may push Activity below the fold on smaller 200%-DPI
    // windows. A compact status-banner link jumps to the existing journal;
    // no duplicate diagnostic action or re-query is created.
    private readonly LinkLabel activityLink = new()
    {
        AutoSize = true, TabStop = true, Visible = false,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(12, 5, 0, 5)
    };
    private SmartRepairDecision? lastSmartDecision;
    private bool awaitingAppSignInCheck;
    private bool appSignInReportedWorking;
    private bool appSignInReportedFailed;
    private readonly TableLayoutPanel smartPanel = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1,
        RowCount = 5, Margin = new Padding(0, 7, 0, 3),
        Padding = new Padding(10, 8, 10, 8), Visible = false,
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(240, 246, 252)
    };
    private readonly Label smartHeading = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label smartExplanation = UiTheme.Caption(string.Empty);
    private readonly Label preflightSummary = UiTheme.Caption(string.Empty);
    private readonly Label activationDetail = UiTheme.Caption(string.Empty);
    private readonly Button applyRecommendation = UiTheme.CreateActionButton(string.Empty, primary: true);
    private readonly LinkLabel entraReview = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 6, 0, 6)
    };
    private readonly FlowLayoutPanel verificationPanel = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, WrapContents = true,
        Visible = false, Margin = new Padding(0, 8, 0, 6)
    };
    private readonly Label verificationSummary = UiTheme.Caption(string.Empty);
    private readonly Button signInWorks = UiTheme.CreateActionButton(string.Empty, primary: true);
    private readonly Button signInFailed = UiTheme.CreateActionButton(string.Empty);
    private string? recommendedHelpKey;
    private readonly LinkLabel technicalToggle = new()
    {
        AutoSize = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(4, 5, 0, 4),
        Visible = false,
        TabStop = true
    };
    private readonly LinkLabel diagnosticToggle = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(12, 5, 0, 5)
    };
    private bool diagnosticsCollapsed;
    private bool technicalWasOpen;
    private bool suppressScopeWizard;
    private bool allUsersWizardOpen;
    private readonly AllUsersWizardAction pendingAction;
    private string? lastRawDiagnostic;
    private FlowLayoutPanel? statusBanner;
    private readonly Label repairExplanation = UiTheme.Caption(string.Empty);
    private readonly Label resetExplanation = UiTheme.Caption(string.Empty);
    private readonly Label allUsersSettingsSummary = UiTheme.Caption(string.Empty);
    private readonly LinkLabel taskStatusLink = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 6, 0, 7)
    };
    private readonly ComboBox scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, Margin = new Padding(0, 4, 0, 8) };
    // All-users options are configured in its mini wizard; UAC continues to
    // receive only the two existing, validated command-line flags.
    private ScheduledWorkerMode allUsersTaskMethod = ScheduledWorkerMode.Exe;
    private bool allUsersScheduleOnly;
    private readonly ComboBox language = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 138,
        Margin = new Padding(0, 0, 0, 0),
        AccessibleName = "Interface language"
    };
    private readonly Label languageLabel = UiTheme.Caption(string.Empty);
    private TableLayoutPanel? headerRow;
    private TableLayoutPanel? subtitleRow;
    private readonly Button repair = UiTheme.CreateActionButton(string.Empty, primary: true);
    private readonly Button reset = UiTheme.CreateActionButton(string.Empty, destructive: true);
    private readonly Button diagnose = UiTheme.CreateActionButton(string.Empty);
    private readonly LinkLabel signInErrors = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(7, 15, 6, 8)
    };
    private readonly Button updates = UiTheme.CreateActionButton(string.Empty);
    private readonly Button diagnostics = UiTheme.CreateActionButton(string.Empty);
    // Scope-specific Setup is for All users only; Current user has direct actions.
    private readonly LinkLabel settings = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(14, 8, 0, 0)
    };
    private readonly TextBox detail = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Top, Height = 210,
        ScrollBars = ScrollBars.Vertical, WordWrap = true,
        Margin = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9.5f),
        BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window,
        Visible = false
    };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 5, Visible = false, Style = ProgressBarStyle.Marquee };
    private readonly ActivityJournalView activityJournal = new();
    private readonly System.Windows.Forms.Timer diagnosticTimer = new() { Interval = 1000 };
    private CancellationTokenSource? diagnosticCancellation;
    private DateTimeOffset diagnosticStartedUtc;
    private int diagnosticCompletedCount;
    private string diagnosticLastCompleted = string.Empty;
    private TableLayoutPanel? mainLayout;
    private Panel? scrollViewport;
    private bool activityExpanded;
    private bool autoHeightReady;
    private bool autoHeightQueued;
    private bool autoHeightApplying;
    private bool userChangedWindowSize;
    private bool userResizingWindow;
    private Size resizeStartSize;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private bool busy;
    private UniversalUpdater.UpdateRelease? availableUpdate;

    internal MainForm(bool preferAllUsers = false,
        bool usePowerShellTasks = false, bool scheduleOnlyAtLogon = false,
        AllUsersWizardAction pendingAction = AllUsersWizardAction.SaveOnly)
    {
        this.pendingAction = pendingAction;
        allUsersTaskMethod = usePowerShellTasks ? ScheduledWorkerMode.PowerShell : ScheduledWorkerMode.Exe;
        allUsersScheduleOnly = scheduleOnlyAtLogon;
        Text = $"Microsoft 365 Auth Repair v{Repair.Version}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(820, 560);
        Size = new Size(1120, 760);
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(1120, 760),
            new Size(1460, 920), widthFraction: 0.90, heightFraction: 0.70);

        // A scrollable Panel owns viewport scrolling. TableLayoutPanel's own
        // AutoScroll + mixed AutoSize/percent rows can clip the bottom at high DPI
        // and retain empty row height after collapse.
        var viewport = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Padding = Padding.Empty,
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas
        };
        scrollViewport = viewport;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20), AutoScroll = false,
            ColumnCount = 1, RowCount = 17,
            BackColor = viewport.BackColor
        };
        mainLayout = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 17; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Distinct header zones: Settings belongs with the app title on the
        // upper right; language has its own labeled selector below. Neither
        // is squeezed against the other, including in RU/HE at 200% DPI.
        var top = new TableLayoutPanel
        {
            ColumnCount = 3, RowCount = 1, Dock = DockStyle.Fill,
            AutoSize = true, Margin = new Padding(0, 0, 0, 3)
        };
        headerRow = top;
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(UiIcons.CreateBrand(), 0, 0);
        title.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        title.Margin = new Padding(12, 0, 15, 0);
        top.Controls.Add(title, 1, 0);
        settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        settings.Margin = new Padding(18, 0, 0, 0);
        top.Controls.Add(settings, 2, 0);

        language.Items.AddRange(["English", "Русский", "עברית"]);
        language.SelectedIndex = UiStrings.Language switch { "ru" => 1, "he" => 2, _ => 0 };
        languageLabel.Margin = new Padding(0, 5, 9, 0);
        languageLabel.Anchor = AnchorStyles.Left;
        var chooser = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 0, 5),
            Margin = new Padding(18, 2, 0, 2)
        };
        chooser.Controls.Add(languageLabel);
        chooser.Controls.Add(language);
        var subline = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = 1,
            AutoSize = true, Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 9)
        };
        subtitleRow = subline;
        subline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        subline.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        subline.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        subtitle.Margin = new Padding(0, 8, 10, 2);
        subline.Controls.Add(subtitle, 0, 0);
        subline.Controls.Add(chooser, 1, 0);
        top.SizeChanged += (_, _) =>
        {
            int available = Math.Max(150, top.ClientSize.Width -
                settings.GetPreferredSize(Size.Empty).Width - 105);
            title.MaximumSize = new Size(available, 0);
        };
        subline.SizeChanged += (_, _) =>
        {
            int available = Math.Max(150, subline.ClientSize.Width -
                chooser.GetPreferredSize(Size.Empty).Width - 40);
            subtitle.MaximumSize = new Size(available, 0);
        };
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(subline, 0, 1);
        root.Controls.Add(account, 0, 2);

        var target = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        target.Controls.Add(scopeLabel);
        target.Controls.Add(scope);
        // Standard users may SELECT All users. Only executing that scope
        // requires a consented UAC restart; current-user operations never do.
        scope.Items.AddRange([CurrentUserOption(), UiStrings.T("all")]);
        scope.SelectedIndex = preferAllUsers && elevated ? 1 : 0;
        scope.Enabled = true;
        target.Controls.Add(privilege);
        root.Controls.Add(target, 0, 3);

        // Distinct, explained choices. The destructive reset is no longer hidden
        // behind Advanced, and is visually separated from safe WAM registration.
        var actions = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Margin = new Padding(0, 6, 0, 6)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        repairExplanation.MaximumSize = new Size(850, 0);
        resetExplanation.MaximumSize = new Size(850, 0);
        actions.Controls.Add(repairExplanation, 0, 0);
        var safeActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        safeActions.Controls.Add(diagnose);
        safeActions.Controls.Add(repair);
        safeActions.Controls.Add(signInErrors);
        actions.Controls.Add(safeActions, 0, 1);
        actions.Controls.Add(resetExplanation, 0, 2);
        var resetActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        resetActions.Controls.Add(reset);
        actions.Controls.Add(resetActions, 0, 3);
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        tools.Controls.Add(diagnostics);
        tools.Controls.Add(updates);
        actions.Controls.Add(tools, 0, 4);
        root.Controls.Add(actions, 0, 4);

        // All-users task choices are summarized here; all controls now live
        // in the scope-specific Setup wizard.
        allUsersSettingsSummary.MaximumSize = new Size(1100, 0);
        var allUsersOverview = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };
        allUsersOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        allUsersOverview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        allUsersOverview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        allUsersOverview.Controls.Add(allUsersSettingsSummary, 0, 0);
        allUsersOverview.Controls.Add(taskStatusLink, 0, 1);
        root.Controls.Add(allUsersOverview, 0, 5);

        var banner = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = true,
            Padding = new Padding(12, 9, 12, 9),
            Margin = new Padding(0, 10, 0, 4)
        };
        if (!SystemInformation.HighContrast)
            banner.BackColor = Color.FromArgb(236, 242, 249);
        statusBanner = banner;
        banner.Controls.Add(statusGlyph);
        banner.Controls.Add(status);
        banner.Controls.Add(diagnosticToggle);
        banner.Controls.Add(activityLink);
        root.Controls.Add(banner, 0, 6);

        // WAM and Office evidence belong together and appear directly after
        // the diagnosis banner (not below 14 large TPM/BitLocker tiles).
        // The TPM card remains independent and is visible DURING a scan,
        // while these WAM/Office rows remain collapsed until results exist.
        root.Controls.Add(diagnosticResult, 0, 7);
        officeLicenseSummary.Visible = false;
        officeLicenseSummary.Margin = new Padding(4, 8, 0, 4);
        root.Controls.Add(officeLicenseSummary, 0, 8);
        root.Controls.Add(officeVolumeLink, 0, 9);
        smartPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            smartPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        smartPanel.Controls.Add(smartHeading, 0, 0);
        smartPanel.Controls.Add(smartExplanation, 0, 1);
        smartPanel.Controls.Add(preflightSummary, 0, 2);
        var recommendationRow = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = true
        };
        recommendationRow.Controls.Add(applyRecommendation);
        smartPanel.Controls.Add(recommendationRow, 0, 3);
        smartPanel.Controls.Add(entraReview, 0, 4);
        root.Controls.Add(smartPanel, 0, 10);
        activationDetail.Visible = false;
        activationDetail.Margin = new Padding(6, 8, 0, 4);
        root.Controls.Add(activationDetail, 0, 11);
        verificationPanel.Controls.Add(verificationSummary);
        verificationPanel.Controls.Add(signInWorks);
        verificationPanel.Controls.Add(signInFailed);
        root.Controls.Add(verificationPanel, 0, 12);
        root.Controls.Add(technicalToggle, 0, 13);
        root.Controls.Add(detail, 0, 14);
        root.Controls.Add(deviceSecurityCard, 0, 15);
        activityJournal.Height = 188;
        activityJournal.Visible = false;
        root.Controls.Add(activityJournal, 0, 16);
        SetActivityVisible(false);
        SetWamResultsVisible(false);
        root.SizeChanged += (_, _) =>
        {
            int available = Math.Max(190, root.ClientSize.Width - root.Padding.Horizontal - 45);
            smartHeading.MaximumSize = new Size(available, 0);
            smartExplanation.MaximumSize = new Size(available, 0);
            preflightSummary.MaximumSize = new Size(available, 0);
            activationDetail.MaximumSize = new Size(available, 0);
            verificationSummary.MaximumSize = new Size(available, 0);
        };
        viewport.Controls.Add(root);
        Controls.Add(ResponsiveLayout.Center(this, viewport, 1340));

        // Fit vertically when actual results change. Preserve user-selected
        // sizes and maximized windows; never alter window width.
        root.SizeChanged += (_, _) => QueueAutoHeight();
        ResizeBegin += (_, _) =>
        {
            userResizingWindow = true;
            resizeStartSize = Size;
        };
        ResizeEnd += (_, _) =>
        {
            userResizingWindow = false;
            if (Size != resizeStartSize) userChangedWindowSize = true;
        };
        SizeChanged += (_, _) =>
        {
            // Windows snap can change size without ResizeBegin.
            if (autoHeightReady && !autoHeightApplying && !userResizingWindow)
                userChangedWindowSize = true;
        };
        Shown += (_, _) =>
        {
            autoHeightReady = true;
            QueueAutoHeight();
        };
        Controls.Add(progress);
        progress.BringToFront();

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(repair, UiGlyph.Repair, primary: true);
        UiIcons.Assign(reset, UiGlyph.Reset, destructive: true);
        UiIcons.Assign(diagnose, UiGlyph.Search);
        UiIcons.Assign(applyRecommendation, UiGlyph.Repair, primary: true);
        UiIcons.Assign(signInWorks, UiGlyph.Success, primary: true);
        UiIcons.Assign(signInFailed, UiGlyph.Search);
        UiIcons.Assign(diagnostics, UiGlyph.Reports);
        diagnostics.UseMnemonic = false; // show literal & instead of treating it as accelerator
        UiIcons.Assign(updates, UiGlyph.Updates);
        UiIcons.TrackDpi(this);
        technicalToggle.LinkClicked += (_, _) =>
        {
            detail.Visible = !detail.Visible;
            technicalWasOpen = detail.Visible;
            UpdateTechnicalLink();
            if (detail.Visible) scrollViewport?.ScrollControlIntoView(detail);
        };
        diagnosticToggle.LinkClicked += (_, _) => SetDiagnosticsCollapsed(!diagnosticsCollapsed);
        officeVolumeLink.LinkClicked += (_, _) =>
        {
            using var licensing = new OfficeVolumeLicenseForm();
            licensing.ShowDialog(this);
        };
        root.SizeChanged += (_, _) =>
        {
            int available = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 25);
            status.MaximumSize = new Size(Math.Max(160, available - 115), 0);
            officeLicenseSummary.MaximumSize = new Size(available, 0);
        };
        var hints = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500, ReshowDelay = 200 };
        hints.SetToolTip(repair, "Re-register Microsoft.AAD.BrokerPlugin for the selected user. No credentials or sign-in caches are deleted; apps may briefly close.");
        hints.SetToolTip(reset, "Destructive: remove Office/WAM identity and sign-in caches for the selected users. Re-authentication required; explicit confirmation.");
        hints.SetToolTip(diagnose, "Read-only check of this application's Windows account and session, NOT every profile. It does not prove Office app sign-in works.");
        hints.SetToolTip(signInErrors, "Offline Microsoft 365 error-code guide: no report is sent and no account is changed.");
        hints.SetToolTip(diagnostics, "Generate a sanitized report, review the exact JSON and explicitly choose whether to send.");
        hints.SetToolTip(updates, "Check Stable / Preview releases and safely update with rollback.");
        hints.SetToolTip(scope, "Current user never requires UAC. All users needs UAC and per-user tasks.");
        hints.SetToolTip(settings, "Configure all-users repair tasks and review old scheduled workers.");
        hints.SetToolTip(applyRecommendation, "Start only the specifically recommended WAM action with the existing independent confirmation.");
        hints.SetToolTip(entraReview, "Optional read-only Microsoft Graph sign-in audit using Device Code or an approved certificate.");
        hints.SetToolTip(activityLink,
            "Expand or collapse the existing local Activity log; no new checks are started.");
        hints.SetToolTip(signInWorks, "You confirm that an actual Microsoft 365 application completed sign-in; this is not independently tested.");
        hints.SetToolTip(signInFailed, "App sign-in still failed. Review the error and optional Entra audit.");

        scope.SelectedIndexChanged += async (_, _) =>
        {
            if (suppressScopeWizard) return;
            if (scope.SelectedIndex == 1 && !allUsersWizardOpen)
            {
                bool confirmed = await ShowAllUsersWizardAsync();
                if (IsDisposed || Disposing) return;
                if (!confirmed)
                {
                    // Cancel doesn't silently leave All users selected.
                    suppressScopeWizard = true;
                    try { scope.SelectedIndex = 0; }
                    finally { suppressScopeWizard = false; }
                    UpdateScopeExplanation();
                    return;
                }
            }
            UpdateScopeExplanation();
            if (lastRawDiagnostic is not null) DisplayDiagnosis(lastRawDiagnostic);
        };
        settings.LinkClicked += async (_, _) =>
        {
            if (busy || scope.SelectedIndex != 1) return;
            await ShowAllUsersWizardAsync();
        };
        taskStatusLink.LinkClicked += (_, _) =>
        {
            if (busy || scope.SelectedIndex != 1 || !elevated) return;
            using var viewer = new AllUsersTaskStatusForm();
            viewer.ShowDialog(this);
        };
        diagnosticTimer.Tick += (_, _) =>
        {
            if (diagnosticCancellation is not null && busy &&
                !diagnosticCancellation.IsCancellationRequested)
                ShowDiagnosticProgress();
        };
        activityJournal.CancelRequested += (_, _) =>
        {
            if (diagnosticCancellation is null || diagnosticCancellation.IsCancellationRequested)
                return;
            diagnosticCancellation.Cancel();
            activityJournal.SetCancellationAvailable(false);
            diagnosticTimer.Stop();
            ShowStatus(UpdateUiText.T(
                "Stopping read-only checks. No changes were made.",
                "Останавливаю ожидание диагностики. Изменений нет.",
                "עצירת האבחון לקריאה בלבד. לא נעשו שינויים."), UiGlyph.Info);
        };
        FormClosed += (_, _) =>
        {
            diagnosticCancellation?.Cancel();
            diagnosticTimer.Stop();
            diagnosticTimer.Dispose();
        };
        repair.Click += async (_, _) => await RunRepairAsync(false);
        reset.Click += async (_, _) => await RunRepairAsync(true);
        diagnose.Click += async (_, _) => await RunDiagnoseAsync();
        applyRecommendation.Click += async (_, _) =>
        {
            if (!busy && scope.SelectedIndex == 0 &&
                lastSmartDecision?.Action == SmartRepairAction.ReRegisterWam &&
                lastSmartDecision.SafeToOfferRepair)
                await RunRepairAsync(false);
        };
        entraReview.LinkClicked += (_, _) =>
        {
            if (busy) return;
            using var reviewer = new EntraSignInReviewDialog();
            reviewer.ShowDialog(this);
        };
        activityLink.LinkClicked += (_, _) =>
            SetActivityVisible(!activityExpanded, scrollWhenExpanded: true);
        deviceSecurityCard.DetailsRequested += (_, _) =>
        {
            if (lastDeviceProtection is null) return;
            // Inspect the current snapshot without starting any new WMI query.
            using var inspector = new TpmBitLockerDialog(lastDeviceProtection);
            inspector.ShowDialog(this);
        };
        deviceSecurityCard.AdvancedRequested += (_, _) =>
        {
            if (busy || lastDeviceProtection is null) return;
            // Never elevate implicitly or run actions from Check & Diagnose.
            // A non-admin can inspect the dialog but cannot execute operations.
            using var advanced = new DeviceSecurityAdvancedForm();
            advanced.ShowDialog(this);
        };
        deviceSecurityCard.BitLockerSettingsRequested += (_, _) =>
        {
            if (busy || lastDeviceProtection is null) return;
            try { DeviceSecurityAdvancedActions.OpenBitLockerManagement(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ShowStatus(UpdateUiText.T(
                    "Windows BitLocker settings unavailable.",
                    "Не удалось открыть настройки BitLocker Windows.",
                    "הגדרות BitLocker של Windows אינן זמינות."), UiGlyph.Info);
            }
        };
        signInWorks.Click += (_, _) =>
        {
            if (busy || lastRawDiagnostic is null) return;
            appSignInReportedWorking = true;
            appSignInReportedFailed = false;
            UpdateVerificationSummary();
        };
        signInFailed.Click += (_, _) =>
        {
            if (busy || lastRawDiagnostic is null) return;
            appSignInReportedFailed = true;
            appSignInReportedWorking = false;
            UpdateVerificationSummary();
        };
        signInErrors.LinkClicked += (_, _) =>
        {
            using var errorGuide = new SignInErrorDialog(recommendedHelpKey);
            errorGuide.ShowDialog(this);
        };
        diagnostics.Click += (_, _) => new DiagnosticsCenter().ShowDialog(this);
        updates.Click += async (_, _) =>
        {
            using var window = new UpdateCenter();
            window.ShowDialog(this);
            await CheckForUpdatesAsync();
        };
        language.SelectedIndexChanged += (_, _) =>
        {
            var index = language.SelectedIndex;
            UiStrings.Language = index == 1 ? "ru" : index == 2 ? "he" : "en";
            ApplyText();
        };
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        Shown += async (_, _) =>
        {
            // A successful update retains its rollback .bak until this
            // exact installed version has opened a real interactive UI.
            // --ui-self-test / worker / unsuccessful updates never clean it.
            UpdateHandoff.CleanupSuccessfulBackupAfterStartup();
            updateTimer.Start();
            // UAC handoff can only request this explicit, independently
            // confirmed action; no repair runs on wizard completion or UAC.
            if (UserScopeSupport.SafePendingAction(pendingAction, elevated,
                scope.SelectedIndex == 1))
            {
                await RunRepairAsync(pendingAction == AllUsersWizardAction.ResetOffice);
                if (IsDisposed || Disposing) return;
            }
            await CheckForUpdatesAsync();
        };
        ApplyText();
        var lastUpdate = UpdateHandoff.ConsumeResult();
        if (lastUpdate is not null) ShowStatus(lastUpdate, lastUpdate.StartsWith("Updated successfully", StringComparison.Ordinal)
            ? UiGlyph.Success : UiGlyph.Error);
    }

    private async Task<bool> ShowAllUsersWizardAsync()
    {
        if (allUsersWizardOpen || busy) return false;
        allUsersWizardOpen = true;
        AllUsersWizardAction action = AllUsersWizardAction.SaveOnly;
        bool confirmed;
        try
        {
            using var wizard = new AllUsersWizard(allUsersTaskMethod,
                allUsersScheduleOnly, elevated);
            confirmed = wizard.ShowDialog(this) == DialogResult.OK;
            if (confirmed)
            {
                allUsersTaskMethod = wizard.TaskMethod;
                allUsersScheduleOnly = wizard.ScheduleOnly;
                action = wizard.SelectedAction;
            }
        }
        finally { allUsersWizardOpen = false; }
        if (!confirmed) return false;

        UpdateScopeExplanation();
        if (action != AllUsersWizardAction.SaveOnly)
            await RunRepairAsync(action == AllUsersWizardAction.ResetOffice);
        return true;
    }

    // UI-only scope transition. Suppress the modal All users wizard for the
    // Windows smoke test, then restore the default Current user selection.
    internal bool ScopeWorkflowSelfTest()
    {
        if (scope.SelectedIndex != 0 || scope.Items.Count != 2 ||
            allUsersWizardOpen || repair.Parent is null || reset.Parent is null)
            return false;
        bool previousSuppression = suppressScopeWizard;
        suppressScopeWizard = true;
        try
        {
            scope.SelectedIndex = 1;
            UpdateScopeExplanation();
            bool bulk = scope.SelectedIndex == 1 &&
                allUsersSettingsSummary.Text.Length > 10 &&
                settings.Text.Length > 3 &&
                taskStatusLink.Parent is not null &&
                diagnose.Text == UpdateUiText.T("Check this account only",
                    "Проверить только этот аккаунт", "בדיקת חשבון זה בלבד");
            scope.SelectedIndex = 0;
            UpdateScopeExplanation();
            return bulk && scope.SelectedIndex == 0 &&
                repair.Parent is not null && reset.Parent is not null &&
                diagnose.Text == UpdateUiText.T("Check & Diagnose",
                    "Проверить и объяснить", "בדוק ואבחן");
        }
        finally
        {
            suppressScopeWizard = previousSuppression;
            if (scope.SelectedIndex != 0)
            {
                suppressScopeWizard = true;
                scope.SelectedIndex = 0;
                UpdateScopeExplanation();
                suppressScopeWizard = previousSuppression;
            }
        }
    }

    internal bool HeaderLayoutSelfTest()
    {
        if (headerRow is null || subtitleRow is null) return false;
        return settings.Parent == headerRow &&
            headerRow.Controls.Contains(title) &&
            !headerRow.Controls.Contains(language) &&
            subtitleRow.Controls.Contains(subtitle) &&
            subtitleRow.Controls.OfType<FlowLayoutPanel>()
                .Any(group => group.Controls.Contains(language) &&
                    group.Controls.Contains(languageLabel)) &&
            !string.IsNullOrWhiteSpace(languageLabel.Text);
    }

    private void ApplyText()
    {
        title.Text = UiStrings.T("title");
        subtitle.Text = $"{UiStrings.T("subtitle")}  ·  v{Repair.Version}";
        UpdateTechnicalLink();
        UpdateDiagnosticsToggle();
        account.Text = UpdateUiText.T(
            $"Desktop user: {desktopUser.Name}  |  Session {Identity.SessionId()}  |  {(elevated ? "Administrator" : "Standard")}",
            $"Пользователь сеанса: {desktopUser.Name}  |  Сеанс {Identity.SessionId()}  |  {(elevated ? "Администратор" : "Обычный доступ")}",
            $"משתמש פעיל: {desktopUser.Name}  |  הפעלה {Identity.SessionId()}  |  {(elevated ? "מנהל" : "משתמש רגיל")}");
        scopeLabel.Text = UiStrings.T("scope") + ": ";
        var previous = scope.SelectedIndex < 0 ? 0 : scope.SelectedIndex;
        // Translation rebuilds the combo box without re-opening the wizard.
        suppressScopeWizard = true;
        try
        {
            scope.Items.Clear();
            scope.Items.Add(CurrentUserOption());
            scope.Items.Add(UiStrings.T("all"));
            scope.SelectedIndex = Math.Min(previous, scope.Items.Count - 1);
        }
        finally { suppressScopeWizard = false; }
        settings.Text = UpdateUiText.T("All users setup", "Мастер: все пользователи",
            "הגדרות כל המשתמשים");
        taskStatusLink.Text = UpdateUiText.T(
            "View task status (read-only)",
            "Состояние заданий (только просмотр)",
            "מצב המשימות (קריאה בלבד)");
        taskStatusLink.AccessibleName = taskStatusLink.Text;
        settings.AccessibleDescription = UpdateUiText.T(
            "Open the all-users task wizard and review protected worker cleanup",
            "Открыть мастер заданий всех пользователей и очистку worker-файлов",
            "פתח אשף משימות לכל המשתמשים וניקוי קובצי worker");
        activityJournal.RefreshLanguage();
        languageLabel.Text = UpdateUiText.T("Language", "Язык", "שפה");
        languageLabel.AccessibleName = languageLabel.Text;
        language.AccessibleDescription = UpdateUiText.T(
            "Change the interface language", "Изменить язык интерфейса",
            "שינוי שפת הממשק");
        repair.Text = UiStrings.T("repair");
        reset.Text = UiStrings.T("full");
        repairExplanation.Text = UpdateUiText.T(
            "Check & Diagnose only checks sign-in health. Re-register WAM fixes broker registration without deleting Office sign-in caches.",
            "Диагностика проверяет авторизацию без изменений. Re-register WAM восстанавливает регистрацию без удаления кеша входа Office.",
            "האבחון בודק את מצב הכניסה ללא שינוי. רישום WAM מחדש אינו מוחק מטמוני כניסה של Office.");
        resetExplanation.Text = UpdateUiText.T(
            "Last resort: Reset Office sign-in deletes saved identity caches and requires signing in again.",
            "Крайняя мера: Reset Office sign-in удаляет кеш авторизации и потребует повторного входа.",
            "מוצא אחרון: איפוס Office מוחק מטמוני זהות ומחייב כניסה מחדש.");
        diagnose.Text = UpdateUiText.T("Check & Diagnose", "Проверить и объяснить", "בדוק ואבחן");
        applyRecommendation.Text = UpdateUiText.T(
            "Re-register WAM (confirm first)", "Восстановить WAM (с подтверждением)",
            "תיקון WAM באישור");
        entraReview.Text = UpdateUiText.T(
            "Entra sign-in log (optional)", "Журнал входов Entra (необязательно)",
            "יומן כניסה ל-Entra (אופציונלי)");
        entraReview.AccessibleName = entraReview.Text;
        deviceSecurityCard.UpdateLanguage();
        UpdateActivityLink();
        signInWorks.Text = UpdateUiText.T(
            "App sign-in works", "Вход в приложение работает", "הכניסה לאפליקציה פועלת");
        signInFailed.Text = UpdateUiText.T(
            "Still fails", "Вход всё ещё не работает", "עדיין לא עובד");
        signInErrors.Text = UpdateUiText.T("Explain sign-in error",
            "Объяснить ошибку входа", "הסבר שגיאת התחברות");
        signInErrors.AccessibleName = signInErrors.Text;
        officeVolumeLink.Text = UpdateUiText.T(
            "Office / Visio / Project — Volume KMS / MAK",
            "Office / Visio / Project — лицензирование KMS / MAK",
            "Office / Visio / Project — רישוי KMS / MAK");
        officeVolumeLink.AccessibleName = officeVolumeLink.Text;
        diagnostics.Text = UiStrings.T("debug");
        updates.Text = availableUpdate is null ? UiStrings.T("updater") :
            $"{UiStrings.T("updater")}  •  v{availableUpdate.Version}";
        updates.AccessibleDescription = availableUpdate is null
            ? "Check for Stable and Preview updates"
            : $"Update available: {availableUpdate.Version}";
        if (!SystemInformation.HighContrast && updates is ModernButton modern)
        {
            // Soft green notification is visible without garish badge colors.
            modern.BackColor = availableUpdate is null
                ? Color.White : Color.FromArgb(221, 245, 230);
            modern.Invalidate();
        }
        foreach (Control button in new Control[] { repair, reset, diagnose, diagnostics, updates, settings,
            applyRecommendation, signInWorks, signInFailed })
            button.AccessibleName = button.Text;
        UpdateScopeExplanation();
        if (string.IsNullOrWhiteSpace(status.Text)) ShowStatus(UiStrings.T("statusReady"), UiGlyph.Info);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        if (lastRawDiagnostic is not null) DisplayDiagnosis(lastRawDiagnostic);
    }

    private void UpdateVerificationSummary()
    {
        if (lastRawDiagnostic is null)
        {
            verificationPanel.Visible = false;
            return;
        }
        verificationSummary.Text = appSignInReportedWorking
            ? UpdateUiText.T(
                "App sign-in: working (reported by user, not automatically verified).",
                "Вход в приложение: работает (со слов пользователя, без автоматической проверки).",
                "הכניסה פועלת לפי דיווח המשתמש, ללא אימות אוטומטי.")
            : appSignInReportedFailed
                ? UpdateUiText.T(
                    "App sign-in: still failing. Inspect the error code or Entra sign-in audit.",
                    "Вход в приложение всё ещё не работает. Проверьте код ошибки или журнал Entra.",
                    "הכניסה עדיין נכשלת. בדוק יומני Entra.")
                : awaitingAppSignInCheck
                    ? UpdateUiText.T(
                        "Local repair finished; app sign-in NOT tested. Open Word, Outlook or Teams to confirm.",
                        "Локальное восстановление выполнено; вход в Office НЕ проверен. Откройте Word, Outlook или Teams.",
                        "התיקון המקומי הסתיים. כניסה לאפליקציה טרם אומתה.")
                    : UpdateUiText.T(
                        "Office app sign-in has NOT been tested. Try a real sign-in before resetting caches.",
                        "Вход в приложения Office НЕ проверен. Попробуйте войти до сброса кешей.",
                        "כניסה ל-Office טרם אומתה.");
        signInWorks.Enabled = signInFailed.Enabled = !busy;
        verificationPanel.Visible = !diagnosticsCollapsed;
    }

    private void UpdateTechnicalLink()
    {
        technicalToggle.Text = detail.Visible
            ? UpdateUiText.T("Hide technical details", "Скрыть технические подробности", "הסתר פרטים טכניים")
            : UpdateUiText.T("Show technical details", "Показать технические подробности", "הצג פרטים טכניים");
        technicalToggle.AccessibleName = technicalToggle.Text;
    }

    private void DisplayDiagnosis(string raw)
    {
        // Evaluation uses current UI language and this exact user's context.
        // No network call or repeated AppX probe when the language changes.
        var report = WamHealthAnalysis.Evaluate(raw, !elevated || desktopUser.SameAsProcess);
        recommendedHelpKey = SignInKnowledgeBase.Recommend(
            report, !elevated || desktopUser.SameAsProcess).Key;
        if (scope.SelectedIndex == 1)
            report = report with
            {
                Details = report.Details + Environment.NewLine + Environment.NewLine +
                    UpdateUiText.T(
                        "Note: this check reflects only the current Windows session, NOT all user profiles.",
                        "Примечание: проверен только текущий сеанс Windows, а НЕ все пользовательские профили.",
                        "הבדיקה מתייחסת רק להפעלה הנוכחית, ולא לכל הפרופילים.")
            };

        detail.Text = report.Details;
        if (lastOfficeLicense is not null)
        {
            officeLicenseSummary.Text = UpdateUiText.T(
                "OFFICE LICENSING · ", "ЛИЦЕНЗИРОВАНИЕ OFFICE · ", "רישוי OFFICE · ") +
                OfficeLicenseDiagnostics.Summary(lastOfficeLicense);
            detail.Text += Environment.NewLine + Environment.NewLine +
                OfficeLicenseDiagnostics.Details(lastOfficeLicense);
        }
        if (lastOfficeLicense is null)
            officeLicenseSummary.Text = UpdateUiText.T(
                "OFFICE LICENSING · Not verified. Use the Office licensing tools for details.",
                "ЛИЦЕНЗИРОВАНИЕ OFFICE · Не проверено. Откройте инструменты лицензирования.",
                "רישוי OFFICE · לא אומת. פתח את כלי הרישוי לפרטים.");
        var ownSession = !elevated || desktopUser.SameAsProcess;
        lastSmartDecision = SmartRepairEngine.Evaluate(report, lastOfficeLicense,
            lastPreflight, ownSession, lastDeviceProtection);
        smartHeading.Text = UpdateUiText.T(
            "SMART RECOMMENDATION: ", "РЕКОМЕНДАЦИЯ: ", "המלצה חכמה: ") +
            lastSmartDecision.Heading;
        smartExplanation.Text = lastSmartDecision.Explanation +
            Environment.NewLine + UpdateUiText.T("Next: ", "Дальше: ", "הבא: ") +
            lastSmartDecision.NextAction;
        preflightSummary.Text = FormatPreflight(lastPreflight);
        deviceSecurityCard.ShowResult(lastDeviceProtection);
        applyRecommendation.Visible = lastSmartDecision.SafeToOfferRepair &&
            lastSmartDecision.Action == SmartRepairAction.ReRegisterWam &&
            scope.SelectedIndex == 0 && ownSession;
        smartPanel.Visible = !diagnosticsCollapsed;
        activationDetail.Text = lastOfficeActivation is null ? string.Empty :
            lastOfficeActivation.Summary + Environment.NewLine + lastOfficeActivation.Evidence;
        activationDetail.Visible = lastOfficeActivation is not null && !diagnosticsCollapsed;
        UpdateVerificationSummary();
        officeLicenseSummary.Visible = lastOfficeLicense is not null && !diagnosticsCollapsed;
        officeVolumeLink.Visible = lastOfficeLicense is not null && !diagnosticsCollapsed;
        diagnosticResult.ShowResult(report);
        diagnosticToggle.Visible = true;
        statusBanner!.Visible = true;
        if (diagnosticsCollapsed)
        {
            diagnosticResult.Visible = false;
            officeLicenseSummary.Visible = false;
            officeVolumeLink.Visible = false;
            smartPanel.Visible = false;
            activationDetail.Visible = false;
            verificationPanel.Visible = false;
            technicalToggle.Visible = false;
            detail.Visible = false;
        }
        else
        {
            diagnosticResult.Visible = true;
            deviceSecurityCard.Visible = true;
            officeLicenseSummary.Visible = lastOfficeLicense is not null;
            officeVolumeLink.Visible = lastOfficeLicense is not null;
            technicalToggle.Visible = true;
            detail.Visible = technicalWasOpen;
        }
        ShowStatus(diagnosticsCollapsed
            ? report.Summary
            : UpdateUiText.T("Diagnostic results", "Результаты диагностики", "תוצאות האבחון"),
            report.Level switch
            {
                WamHealthLevel.Healthy => UiGlyph.Success,
                WamHealthLevel.RepairRecommended => UiGlyph.Error,
                _ => UiGlyph.Info
            });
        UpdateTechnicalLink();
        UpdateDiagnosticsToggle();
        SetWamResultsVisible(!diagnosticsCollapsed);
    }

    private static string FormatPreflight(RepairPreflight? check)
    {
        if (check is null)
            return UpdateUiText.T("Preflight: unavailable", "Предпроверка: недоступна",
                "בדיקה מוקדמת אינה זמינה");
        string network = check.MicrosoftLoginReachable switch
        {
            true => UpdateUiText.T("HTTPS transport reachable (not signed in)",
                "HTTPS доступен (вход не проверен)", "HTTPS זמין ללא אימות כניסה"),
            false => UpdateUiText.T("HTTPS login endpoint unavailable",
                "HTTPS сервера входа недоступен", "שרת הכניסה אינו זמין"),
            _ => UpdateUiText.T("HTTPS not confirmed", "HTTPS не подтверждён", "HTTPS לא אומת")
        };
        string apps = check.OfficeAppsRunning
            ? UpdateUiText.T("Office apps running: ", "Работают приложения Office: ",
                "יישומי Office פעילים: ") + check.OfficeApps
            : UpdateUiText.T("No Office app processes found",
                "Процессы Office не обнаружены", "לא נמצאו תהליכי Office");
        string clock = check.ClockAcceptable switch
        {
            true => UpdateUiText.T("clock appears aligned", "время согласовано", "השעון תואם"),
            false => UpdateUiText.T("clock requires review", "проверьте время", "יש לבדוק את השעון"),
            _ => UpdateUiText.T("clock unverified", "время не проверено", "השעון לא נבדק")
        };
        return UpdateUiText.T("PREFLIGHT", "ПРЕДВАРИТЕЛЬНАЯ ПРОВЕРКА", "בדיקה מוקדמת") +
            ": " + network + " · " + clock + Environment.NewLine + apps +
            (check.AppxDeploymentRestricted == true
                ? Environment.NewLine + UpdateUiText.T(
                    "AppX enterprise deployment restriction detected (advisory only).",
                    "Найдена политика AppX (только индикатор, не доказательство блокировки).",
                    "זוהתה מדיניות AppX.")
                : "");
    }

    private void SetDiagnosticsCollapsed(bool collapsed)
    {
        if (lastRawDiagnostic is null) return;
        if (collapsed == diagnosticsCollapsed) return;
        if (collapsed) technicalWasOpen = detail.Visible;
        diagnosticsCollapsed = collapsed;
        DisplayDiagnosis(lastRawDiagnostic);
        if (!collapsed)
            scrollViewport?.ScrollControlIntoView(diagnosticResult);
        else
            scrollViewport?.ScrollControlIntoView(statusBanner!);
    }

    // Collapse the actual table rows, not just the child controls. Otherwise a
    // hidden 200%-DPI region can still reserve screen space and the link appears
    // ineffective. Control.Visible cannot be used as an authoritative flag on
    // forms that have not been shown yet.
    private void SetContentRow(Control control, bool show)
    {
        if (mainLayout is null) return;
        var row = mainLayout.GetRow(control);
        if (row < 0 || row >= mainLayout.RowStyles.Count) return;
        control.Visible = show;
        var style = mainLayout.RowStyles[row];
        style.SizeType = show ? SizeType.AutoSize : SizeType.Absolute;
        style.Height = 0;
    }

    private void SetWamResultsVisible(bool expanded)
    {
        if (mainLayout is null) return;
        var show = expanded && lastRawDiagnostic is not null;
        mainLayout.SuspendLayout();
        try
        {
            SetContentRow(diagnosticResult, show);
            SetContentRow(officeLicenseSummary, show);
            SetContentRow(officeVolumeLink, show && lastOfficeLicense is not null);
            SetContentRow(smartPanel, show);
            SetContentRow(activationDetail, show && lastOfficeActivation is not null);
            SetContentRow(verificationPanel, show);
            SetContentRow(technicalToggle, show);
            SetContentRow(detail, show && technicalWasOpen);
        }
        finally { mainLayout.ResumeLayout(true); }
        scrollViewport?.PerformLayout();
        QueueAutoHeight();
    }

    private void UpdateActivityLink()
    {
        activityLink.Text = activityExpanded
            ? UpdateUiText.T("Hide activity log", "Скрыть журнал", "הסתר יומן פעילות")
            : UpdateUiText.T("Show activity log", "Показать журнал", "הצג יומן פעילות");
        activityLink.AccessibleName = activityLink.Text;
    }

    private void SetActivityVisible(bool visible, bool scrollWhenExpanded = false)
    {
        activityExpanded = visible;
        if (mainLayout is not null)
        {
            mainLayout.SuspendLayout();
            try { SetContentRow(activityJournal, visible); }
            finally { mainLayout.ResumeLayout(true); }
        }
        else activityJournal.Visible = visible;
        UpdateActivityLink();
        scrollViewport?.PerformLayout();
        QueueAutoHeight();
        if (visible && scrollWhenExpanded)
            scrollViewport?.ScrollControlIntoView(activityJournal);
    }

    internal bool ResultsAndScrollLayoutSelfTest()
    {
        if (mainLayout is null || scrollViewport is null ||
            !scrollViewport.AutoScroll || mainLayout.AutoScroll ||
            !mainLayout.AutoSize || mainLayout.Dock != DockStyle.Top ||
            mainLayout.GetRow(diagnosticResult) != 7 ||
            mainLayout.GetRow(officeLicenseSummary) != 8 ||
            mainLayout.GetRow(deviceSecurityCard) != 15 ||
            mainLayout.GetRow(activityJournal) != 16) return false;
        SetActivityVisible(true);
        bool expanded = activityExpanded &&
            mainLayout.RowStyles[16].SizeType == SizeType.AutoSize;
        SetActivityVisible(false);
        bool collapsed = !activityExpanded &&
            mainLayout.RowStyles[16].SizeType == SizeType.Absolute &&
            mainLayout.RowStyles[16].Height == 0f;
        SetWamResultsVisible(false);
        bool officeCollapsed = mainLayout.RowStyles[7].SizeType == SizeType.Absolute &&
            mainLayout.RowStyles[8].SizeType == SizeType.Absolute &&
            mainLayout.RowStyles[15].SizeType == SizeType.AutoSize;
        return expanded && collapsed && officeCollapsed;
    }

    // Coalesce many nested TableLayoutPanel events into a single UI-thread
    // resize; no timers, background tasks, or hardware checks involved.
    private void QueueAutoHeight()
    {
        if (!autoHeightReady || autoHeightQueued || autoHeightApplying ||
            userChangedWindowSize || userResizingWindow ||
            WindowState != FormWindowState.Normal || !IsHandleCreated ||
            !Visible || IsDisposed || Disposing) return;
        autoHeightQueued = true;
        BeginInvoke((MethodInvoker)(() =>
        {
            autoHeightQueued = false;
            ApplyAutoHeight();
        }));
    }

    private void ApplyAutoHeight()
    {
        if (!autoHeightReady || autoHeightApplying ||
            userChangedWindowSize || userResizingWindow ||
            WindowState != FormWindowState.Normal || !Visible ||
            IsDisposed || Disposing ||
            mainLayout is null || scrollViewport is null) return;
        autoHeightApplying = true;
        try
        {
            mainLayout.PerformLayout();
            scrollViewport.PerformLayout();
            int contentHeight = mainLayout.Height;
            int viewHeight = scrollViewport.ClientSize.Height;
            if (contentHeight < 50 || viewHeight <= 0) return;

            var work = Screen.FromControl(this).WorkingArea;
            int proposed = ResponsiveLayout.WindowHeightForContent(
                Height, viewHeight, contentHeight, work.Height,
                MinimumSize.Height, DeviceDpi);
            int tolerance = (int)Math.Ceiling(12d * Math.Max(96, DeviceDpi) / 96d);
            if (Math.Abs(proposed - Height) < tolerance) return;

            int inset = (int)Math.Ceiling(8d * Math.Max(96, DeviceDpi) / 96d);
            int adjustedTop = Math.Clamp(Top, work.Top + inset,
                Math.Max(work.Top + inset, work.Bottom - proposed - inset));
            SetBounds(Left, adjustedTop, Width, proposed,
                BoundsSpecified.Y | BoundsSpecified.Height);
        }
        finally { autoHeightApplying = false; }
    }

    internal bool AutoHeightLayoutSelfTest =>
        mainLayout is { AutoSize: true, Dock: DockStyle.Top } &&
        scrollViewport is { AutoScroll: true, Dock: DockStyle.Fill } &&
        !userChangedWindowSize && !autoHeightApplying;

    private void UpdateDiagnosticsToggle()
    {
        diagnosticToggle.Text = diagnosticsCollapsed
            ? UpdateUiText.T("Show WAM / Office results", "Показать WAM / Office", "הצג תוצאות WAM / Office")
            : UpdateUiText.T("Hide WAM / Office results", "Скрыть WAM / Office", "הסתר תוצאות WAM / Office");
        diagnosticToggle.AccessibleName = diagnosticToggle.Text;
        diagnosticToggle.Visible = lastRawDiagnostic is not null;
    }

    internal bool CanCollapseDiagnostics => diagnosticToggle.Visible;
    internal bool AreDiagnosticsCollapsed => diagnosticsCollapsed;
    internal bool HasActivityJournal => activityJournal.HasCopyControl &&
        activityJournal.CopyText.Contains("Microsoft 365 Auth Repair", StringComparison.Ordinal);
    internal bool HasDeviceSecuritySection =>
        deviceSecurityCard.Parent == mainLayout &&
        deviceSecurityCard.HasReadOnlyDetailsLink &&
        deviceSecurityCard.HasDirectSecurityShortcuts &&
        mainLayout?.GetRow(deviceSecurityCard) == 15;
    // A hidden WinForms form has no calculated table layout yet, so
    // GetPositionFromControl may report an empty/uncomputed position in CI.
    // Verify the actual control ownership and compact cancel composition;
    // main-screen placement is explicitly defined by Add(..., 0, row).
    internal bool HasLiveDeviceSecurityAndCompactCancel =>
        deviceSecurityCard.Parent == mainLayout &&
        activityJournal.Parent == mainLayout &&
        activityLink.Parent == statusBanner &&
        activityLink.TabStop &&
        activityJournal.HasCompactCancellation;

    private void ShowStatus(string message, UiGlyph glyph)
    {
        status.Text = message;
        UiIcons.SetStatus(statusGlyph, glyph, message);
    }

    private void SetBusy(bool isBusy)
    {
        busy = isBusy;
        repair.Enabled = reset.Enabled = diagnose.Enabled = diagnostics.Enabled =
            updates.Enabled = settings.Enabled = taskStatusLink.Enabled =
            applyRecommendation.Enabled = entraReview.Enabled =
            signInWorks.Enabled = signInFailed.Enabled = !isBusy;
        // Read-only device details remain accessible while checking.
        scope.Enabled = !isBusy;
        activityJournal.SetCancellationAvailable(isBusy &&
            diagnosticCancellation is not null &&
            !diagnosticCancellation.IsCancellationRequested);
        progress.Visible = isBusy;
    }

    private string CurrentUserOption() =>
        $"{UiStrings.T("current")} — {desktopUser.Name}";

    private void UpdateScopeExplanation()
    {
        if (scope.SelectedIndex < 0) return;
        bool everyone = scope.SelectedIndex == 1;
        // All users uses the scope-specific wizard, not a second set of
        // repair buttons. Current user always acts directly on this screen.
        settings.Visible = everyone;
        taskStatusLink.Visible = everyone && elevated;
        // This diagnosis runs as the app's current Windows account only.
        // An elevated All users view must not imply a scan of every profile.
        diagnose.Text = everyone
            ? UpdateUiText.T("Check this account only",
                "Проверить только этот аккаунт", "בדיקת חשבון זה בלבד")
            : UpdateUiText.T("Check & Diagnose",
                "Проверить и объяснить", "בדוק ואבחן");
        diagnose.AccessibleName = diagnose.Text;
        diagnose.AccessibleDescription = UpdateUiText.T(
            "Only the account running this application is diagnosed, not all users.",
            "Диагностируется только учётная запись этой программы, а не все пользователи.",
            "רק החשבון שמפעיל את התוכנה נבדק.");
        repair.Visible = !everyone;
        reset.Visible = !everyone;
        if (reset.Parent is not null) reset.Parent.Visible = !everyone;
        repairExplanation.Visible = !everyone;
        resetExplanation.Visible = !everyone;
        repair.Text = UiStrings.T("repair");
        reset.Text = UiStrings.T("full");
        repair.AccessibleName = repair.Text;
        reset.AccessibleName = reset.Text;
        allUsersSettingsSummary.Visible = everyone;
        allUsersSettingsSummary.Text = UpdateUiText.T(
            $"All users: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell (WAM only)" : "EXE")} · " +
                (allUsersScheduleOnly ? "at next sign-in" : "now for online users") +
                ". Configure in All users setup. Sign-in diagnostics below check this account only.",
            $"Все пользователи: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell (только WAM)" : "EXE")} · " +
                (allUsersScheduleOnly ? "при следующем входе" : "сейчас для активных") +
                ". Настройка — в мастере. Диагностика ниже относится только к текущей учётной записи.",
            $"כל המשתמשים: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE")} · " +
                (allUsersScheduleOnly ? "בכניסה הבאה" : "כעת למשתמשים מחוברים") +
                ". ההגדרה באשף; הבדיקה להלן מתייחסת לחשבון זה בלבד.");
        var admin = everyone
            ? elevated
                ? UpdateUiText.T("Administrator enabled; each user runs under their own account.",
                    "Права администратора есть; задача выполняется от имени каждого пользователя.",
                    "הרשאת מנהל פעילה; הפעולה מתבצעת בחשבון של כל משתמש.")
                : UpdateUiText.T("Administrator required (UAC when you start). Nothing runs after UAC until you confirm again.",
                    "Нужен администратор (UAC при запуске). После UAC действие не начнётся без повторного подтверждения.",
                    "דרושה הרשאת מנהל (UAC). לאחר האישור הפעולה לא תתחיל ללא לחיצה נוספת.")
            : elevated
                ? UpdateUiText.T("Elevated window; actions will be scheduled as the desktop user (never as a different administrator).",
                    "Окно администратора; действие выполняется под пользователем сеанса, а не чужой учётной записью администратора.",
                    "חלון מנהל: הפעולה תרוץ בחשבון משתמש שולחן העבודה.")
                : UpdateUiText.T("No administrator rights required. Only this user's sign-in is affected.",
                    "Права администратора не нужны. Затрагивается только авторизация этого пользователя.",
                    "אין צורך בהרשאת מנהל. רק התחברות המשתמש הזה תושפע.");

        privilege.Text = admin;
        var target = everyone
            ? UpdateUiText.T("All existing profiles on this PC",
                "Все существующие профили на этом ПК",
                "כל הפרופילים הקיימים במחשב זה")
            : desktopUser.Name;
        reset.AccessibleDescription = $"Full identity reset, target: {target}. {admin}";
        repair.AccessibleDescription = $"Re-register WAM, target: {target}. {admin}";
    }

    private static bool ShouldRecheckAfterRepair(bool failed, bool everyone, bool elevated) =>
        !failed && !everyone && !elevated;

    internal static bool PostRepairSelfTest() =>
        ShouldRecheckAfterRepair(false, false, false) &&
        !ShouldRecheckAfterRepair(true, false, false) &&
        !ShouldRecheckAfterRepair(false, true, false) &&
        !ShouldRecheckAfterRepair(false, false, true);

    private async Task RunRepairAsync(bool full)
    {
        if (busy) return;
        var everyone = scope.SelectedIndex == 1;
        var selectedMode = everyone ? allUsersTaskMethod : ScheduledWorkerMode.Exe;
        var onlySchedule = everyone && allUsersScheduleOnly;
        bool verifyCurrentUserAfterSuccess = false;

        if (everyone && full && selectedMode == ScheduledWorkerMode.PowerShell)
        {
            ShowStatus(UpdateUiText.T(
                "Full identity reset requires EXE mode. PowerShell mode only re-registers WAM.",
                "Полный сброс требует режим EXE. PowerShell только регистрирует WAM.",
                "איפוס מלא דורש EXE. מצב PowerShell רושם רק WAM."), UiGlyph.Error);
            return;
        }

        // A normal user never needs UAC for their own WAM/Office caches.
        // Elevation is ONLY offered when they explicitly choose all profiles.
        if (UserScopeSupport.RequiresUac(everyone, elevated))
        {
            var prompt = UpdateUiText.T(
                "All users on this PC requires administrator rights. A UAC window will open. The elevated application will select All users, but will NOT start any repair until you confirm the action again. Continue?",
                "Для всех пользователей этого ПК нужны права администратора. Откроется UAC. Новое окно выберет «Все пользователи», но ничего не запустит до повторного подтверждения. Продолжить?",
                "לפעולה על כל המשתמשים נדרשת הרשאת מנהל. ייפתח UAC, אך לא תתחיל פעולה עד לאישור חוזר.");
            if (MessageBox.Show(this, prompt,
                    UpdateUiText.T("Administrator required", "Требуются права администратора", "דרושה הרשאת מנהל"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            if (UserScopeSupport.TryElevateForAll(
                selectedMode == ScheduledWorkerMode.PowerShell, onlySchedule,
                full ? AllUsersWizardAction.ResetOffice : AllUsersWizardAction.RepairWam,
                out var reason))
            {
                Close(); // Only after Windows confirms the new process started.
            }
            else if (reason == "UAC_CANCELLED")
            {
                ShowStatus(UpdateUiText.T("UAC cancelled. No changes made.",
                    "UAC отменён. Изменений нет.", "ההרשאה בוטלה. לא בוצעו שינויים."), UiGlyph.Info);
            }
            else
            {
                ShowStatus(UpdateUiText.T("Unable to start the administrator window: " + reason,
                    "Не удалось открыть окно администратора: " + reason,
                    "לא ניתן להפעיל חלון מנהל: " + reason), UiGlyph.Error);
            }
            return;
        }

        if (!everyone && !UserScopeSupport.CanRunCurrent(elevated, desktopUser))
        {
            ShowStatus(UpdateUiText.T(
                "Desktop owner cannot be identified. Run as that user, or choose All users with administrator approval.",
                "Не удалось определить пользователя сеанса. Запустите от его имени либо выберите «Все пользователи» с правами администратора.",
                "לא ניתן לזהות את המשתמש הפעיל. הפעל בחשבונו או בחר את כל המשתמשים עם הרשאת מנהל."), UiGlyph.Error);
            return;
        }

        // Read-only preflight before touching WAM or Office identity state.
        // Network failures are advisory for WAM and do not silently prevent
        // an explicitly requested re-registration.
        RepairPreflight? beforeRepair = null;
        SetBusy(true);
        try
        {
            beforeRepair = await RepairPreflightProbe.CollectAsync(
                !elevated || desktopUser.SameAsProcess);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            or InvalidOperationException or UnauthorizedAccessException
            or System.Security.SecurityException) { /* Unknown, never "healthy". */ }
        finally { if (!IsDisposed && !Disposing) SetBusy(false); }
        if (IsDisposed || Disposing) return;
        lastPreflight = beforeRepair;
        if (full && beforeRepair is { } check &&
            (check.OfficeAppsRunning && (!everyone || !onlySchedule) ||
             check.ClockAcceptable == false && !everyone))
        {
            ShowStatus(UpdateUiText.T(
                "Full reset paused: close Office apps or review Windows time; for All users choose next-sign-in-only tasks.",
                "Полный сброс остановлен: закройте Office или проверьте время. Для All users выберите запуск только при следующем входе.",
                "איפוס מלא נעצר. סגור Office או בדוק את השעון; לכל המשתמשים בחר הפעלה בכניסה הבאה."),
                UiGlyph.Info);
            return;
        }

        // Preflight the exact list before confirmation. These are existing
        // profile registry entries, not every possible future account.
        // Freeze the reviewed profile list so new profiles cannot silently
        // become targets between confirmation and scheduling.
        List<WindowsProfile>? reviewedProfiles = null;
        int onlineAtReview = 0;
        if (everyone)
        {
            try
            {
                reviewedProfiles = UserProfiles.Enumerate();
                var onlineSids = Native.FindExplorerSessions().Values
                    .SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
                onlineAtReview = reviewedProfiles.Count(x => onlineSids.Contains(x.Sid));
                if (reviewedProfiles.Count == 0)
                {
                    ShowStatus(UpdateUiText.T(
                        "No existing Windows profiles found. No tasks were created.",
                        "Существующие профили Windows не найдены. Задания не создавались.",
                        "לא נמצאו פרופילים קיימים. לא נוצרו משימות."),
                        UiGlyph.Info);
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowStatus(UpdateUiText.T(
                    "Unable to enumerate existing user profiles: ",
                    "Не удалось получить список существующих профилей: ",
                    "לא ניתן לקבל רשימת פרופילים: ") + ex.GetType().Name, UiGlyph.Error);
                return;
            }
        }

        // Even the non-destructive current-user action makes changes to AppX
        // registration and may interrupt Microsoft apps. Require a clear consent.
        if (!everyone && !full)
        {
            var prompt = UpdateUiText.T(
                "Re-register Microsoft.AAD.BrokerPlugin for " + desktopUser.Name +
                "? This does NOT delete Office tokens or sign-in caches, but Microsoft apps may briefly close. Continue?",
                "Перерегистрировать Microsoft.AAD.BrokerPlugin для " + desktopUser.Name +
                "? Токены и кеши входа Office НЕ удаляются, но приложения Microsoft могут закрыться. Продолжить?",
                "לבצע רישום מחדש של Microsoft.AAD.BrokerPlugin עבור " + desktopUser.Name +
                "? נתוני ההתחברות לא יימחקו, אך יישומי Microsoft עשויים להיסגר. להמשיך?");
            if (MessageBox.Show(this, prompt,
                    UpdateUiText.T("Confirm WAM re-registration", "Подтвердите регистрацию WAM", "אישור רישום WAM מחדש"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
        }

        if (everyone || full)
        {
            var target = everyone ? UiStrings.T("confirmAll") : UiStrings.T("confirmCurrent");
            var prompt = everyone && !full
                ? UpdateUiText.T(
                    "Run WAM re-registration for ALL existing user profiles on this PC? Online users may have Office apps interrupted; offline users will run at their next sign-in.",
                    "Запустить восстановление WAM у ВСЕХ пользователей с профилями на этом ПК? У активных пользователей могут закрыться приложения Office; у остальных — при следующем входе.",
                    "להריץ תיקון WAM לכל המשתמשים? ייתכן שיישומי Office ייסגרו.")
                : target;
            if (everyone)
            {
                prompt += Environment.NewLine + UpdateUiText.T(
                    $"Method: {selectedMode}; " + (onlySchedule ? "next sign-in only" : "run now where online, otherwise next sign-in"),
                    $"Способ: {(selectedMode == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE")}; " +
                        (onlySchedule ? "только при следующем входе" : "сразу у активных, у остальных при входе"),
                    $"שיטה: {selectedMode}; " + (onlySchedule ? "רק בכניסה הבאה" : "כעת למשתמשים פעילים"));
                int count = reviewedProfiles!.Count;
                int immediate = onlySchedule ? 0 : onlineAtReview;
                int later = count - immediate;
                prompt += Environment.NewLine + UpdateUiText.T(
                    $"Existing profiles: {count}; start now: {immediate}; at next sign-in: {later}. No new-user profiles are enrolled.",
                    $"Существующих профилей: {count}; запуск сейчас: {immediate}; при следующем входе: {later}. Новые профили не добавляются.",
                    $"פרופילים קיימים: {count}; כעת: {immediate}; בכניסה הבאה: {later}. פרופילים חדשים לא נכללים.");
            }
            if (!everyone)
                prompt += Environment.NewLine + UpdateUiText.T(
                    "Selected desktop user: ", "Выбранный пользователь: ", "המשתמש שנבחר: ") + desktopUser.Name;
            var caption = !full && everyone
                ? UpdateUiText.T("Confirm repair for all users", "Подтвердите восстановление всех пользователей", "אישור תיקון לכל המשתמשים")
                : UiStrings.T("confirmTitle");
            if (MessageBox.Show(this, prompt, caption, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
        }

        // Capture WinForms checkbox state on UI thread before Task.Run.
        // Repair invalidates the previous device snapshot. Never show a
        // stale green TPM or BitLocker status after changing sign-in state.
        lastDeviceProtection = null;
        deviceSecurityCard.ClearResult();
        activityLink.Visible = false;
        lastRawDiagnostic = null;
        lastOfficeLicense = null;
        lastOfficeActivation = null;
        lastSmartDecision = null;
        awaitingAppSignInCheck = false;
        appSignInReportedWorking = appSignInReportedFailed = false;
        smartPanel.Visible = activationDetail.Visible = verificationPanel.Visible = false;
        recommendedHelpKey = null;
        officeLicenseSummary.Visible = false;
        officeVolumeLink.Visible = false;
        diagnosticsCollapsed = false;
        technicalWasOpen = false;
        diagnosticToggle.Visible = false;
        diagnosticResult.ClearResult();
        SetWamResultsVisible(false);
        statusBanner!.Visible = true;
        SetBusy(true);
        var operationStep = full ? ActivityStep.OfficeReset :
            everyone ? ActivityStep.UserTaskScheduling : ActivityStep.WamRegistration;
        activityJournal.Add(operationStep, ActivityOutcome.Started);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            var result = await Task.Run(() =>
            {
                if (!elevated) return full ? Repair.Run(quiet: true) == 0 ? "SUCCESS" : "FAILED" :
                    Repair.ReRegister(quiet: true) == 0 ? "SUCCESS" : "FAILED";
                return ScheduleAdminRepair(everyone, full, selectedMode, onlySchedule, reviewedProfiles);
            });
            detail.Text = result;
            detail.Visible = false;
            technicalToggle.Visible = true;
            UpdateTechnicalLink();
            var failed = result.Contains("FAILED", StringComparison.Ordinal);
            activityJournal.Add(operationStep, failed ? ActivityOutcome.Failed :
                elevated ? ActivityOutcome.Scheduled : ActivityOutcome.Finished);
            // The local user has already finished repair synchronously.
            // A scheduled all-users / elevated worker has NOT necessarily
            // finished: never auto-claim a queued task as a successful repair.
            verifyCurrentUserAfterSuccess = ShouldRecheckAfterRepair(failed, everyone, elevated);
            awaitingAppSignInCheck = verifyCurrentUserAfterSuccess;
            ShowStatus(failed ? UiStrings.T("failed") : elevated
                ? UpdateUiText.T("Per-user tasks queued; check user logs for completion.",
                    "Задачи пользователей назначены. Проверяйте результат в их журналах.",
                    "המשימות תוזמנו. בדוק את יומני המשתמשים.")
                : UiStrings.T("done"), failed ? UiGlyph.Error : UiGlyph.Success);

        }
        catch (Exception ex)
        {
            activityJournal.Add(operationStep, ActivityOutcome.Failed);
            ShowStatus(UiStrings.T("failed"), UiGlyph.Error);
            detail.Text = ex.GetType().Name + ": " + ex.Message;
            detail.Visible = false;
            technicalToggle.Visible = true;
            UpdateTechnicalLink();
        }
        finally { SetBusy(false); }

        // No second Check button is needed after a successful direct repair.
        // Read-only post-check still cannot prove Word/Teams/OneDrive login.
        // Do not run it for queued per-user jobs.
        if (verifyCurrentUserAfterSuccess && !IsDisposed && !Disposing)
            await RunDiagnoseAsync();
    }

    private static string ScheduleAdminRepair(bool everyone, bool full,
        ScheduledWorkerMode mode, bool onlySchedule,
        List<WindowsProfile>? reviewedProfiles = null)
    {
        var profiles = reviewedProfiles ?? UserProfiles.Enumerate();
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
        if (mode == ScheduledWorkerMode.PowerShell)
            Scheduling.PrepareProtectedPowerShell();
        else
            Scheduling.PrepareProtectedExe();
        var onlineSids = sessions.Values.SelectMany(s => s).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();
        // Preview.5 mistakenly enrolled accounts with NO existing profile.
        // Only actual entries in Windows ProfileList should be processed.
        // Remove our own legacy machine-wide first-logon trigger, if present.
        if (everyone)
        {
            try
            {
                if (FirstLogonDeployment.DisableLegacyIfPresent())
                    results.Add("Legacy new-user auto-run was disabled.");
            }
            catch (Exception ex)
            {
                results.Add("FAILED to remove legacy new-user trigger: " + ex.GetType().Name);
            }
        }
        foreach (var profile in selected)
        {
            try
            {
                results.Add(Scheduling.QueueForSid(profile, onlineSids.Contains(profile.Sid),
                    full ? "full" : "reregister", mode, onlySchedule));
            }
            catch (Exception ex) { results.Add("FAILED to queue " + profile.Account + ": " + ex.GetType().Name); }
        }
        return string.Join(Environment.NewLine, results);
    }

    private static string DiagnosticSectionLabel(string key) => key switch
    {
        "WAM_ENTRA" => UpdateUiText.T("WAM / Entra", "WAM / Entra", "WAM / Entra"),
        "OFFICE_LICENSE" => UpdateUiText.T("Office licensing", "Лицензия Office", "רישוי Office"),
        "TPM_BITLOCKER" => UpdateUiText.T("TPM / BitLocker", "TPM / BitLocker", "TPM / BitLocker"),
        "NETWORK_PREFLIGHT" => UpdateUiText.T("Network / preflight", "Сеть / предпроверка", "רשת / בדיקה מקדימה"),
        "TPM_WMI" => "TPM (WMI)",
        "BITLOCKER_WMI" => UpdateUiText.T("BitLocker protection (WMI)",
            "Защита BitLocker (WMI)", "הגנת BitLocker (WMI)"),
        "BITLOCKER_CONVERSION" => UpdateUiText.T("BitLocker encryption (WMI)",
            "Шифрование BitLocker (WMI)", "הצפנת BitLocker (WMI)"),
        "THIRD_PARTY_FDE" => UpdateUiText.T("Third-party disk encryption software",
            "Стороннее шифрование — программы", "תוכנות הצפנת דיסק חיצוניות"),
        "DEVICE_GUARD_WMI" => "Device Guard / HVCI (WMI)",
        "DOMAIN_RWDC" => UpdateUiText.T("Domain controller", "Контроллер домена", "בקר תחום"),
        "TPM_LOCKOUT" => UpdateUiText.T("TPM lockout", "Блокировка TPM", "נעילת TPM"),
        "DPAPI_ATTESTATION" => "DPAPI / AIK",
        "SECURE_BOOT" => "Secure Boot / UEFI",
        "VIRTUAL_MACHINE" => UpdateUiText.T("Virtual machine", "Виртуальная машина", "מכונה וירטואלית"),
        _ => UpdateUiText.T("Not verified", "Не проверено", "לא נבדק")
    };

    private static ActivityStep ActivityFromDiagnosticSection(string section) => section switch
    {
        "WAM_ENTRA" => ActivityStep.WamEntra,
        "OFFICE_LICENSE" => ActivityStep.OfficeLicensing,
        "TPM_BITLOCKER" => ActivityStep.TpmBitLocker,
        "NETWORK_PREFLIGHT" => ActivityStep.NetworkPreflight,
        "TPM_WMI" => ActivityStep.TpmWmi,
        "BITLOCKER_WMI" => ActivityStep.BitLockerWmi,
        "BITLOCKER_CONVERSION" => ActivityStep.BitLockerConversion,
        "THIRD_PARTY_FDE" => ActivityStep.ThirdPartyFde,
        "DEVICE_GUARD_WMI" => ActivityStep.DeviceGuardWmi,
        "DOMAIN_RWDC" => ActivityStep.DomainDc,
        "TPM_LOCKOUT" => ActivityStep.TpmLockout,
        "DPAPI_ATTESTATION" => ActivityStep.DpapiAttestation,
        "SECURE_BOOT" => ActivityStep.SecureBoot,
        "VIRTUAL_MACHINE" => ActivityStep.VirtualMachine,
        _ => ActivityStep.ReadOnlyCheck
    };

    private void ShowDiagnosticProgress()
    {
        var seconds = Math.Max(0, (int)(DateTimeOffset.UtcNow - diagnosticStartedUtc).TotalSeconds);
        var last = diagnosticLastCompleted.Length == 0 ? "" :
            " · " + DiagnosticSectionLabel(diagnosticLastCompleted);
        ShowStatus(UpdateUiText.T(
            $"Read-only sign-in check: {diagnosticCompletedCount}/4 finished, {seconds}s{last}. Apps stay open.",
            $"Проверка без изменений: {diagnosticCompletedCount}/4 завершено, {seconds} с{last}. Приложения не закрываются.",
            $"בדיקה לקריאה בלבד: {diagnosticCompletedCount}/4 הושלמו, {seconds} שניות{last}. האפליקציות נשארות פתוחות."),
            UiGlyph.Working);
    }

    private async Task RunDiagnoseAsync()
    {
        if (busy) return;
        lastRawDiagnostic = null;
        lastOfficeLicense = null;
        lastOfficeActivation = null;
        lastSmartDecision = null;
        lastPreflight = null;
        lastDeviceProtection = null;
        deviceSecurityCard.ClearResult();
        activityLink.Visible = false;
        recommendedHelpKey = null;
        officeLicenseSummary.Visible = false;
        officeVolumeLink.Visible = false;
        smartPanel.Visible = activationDetail.Visible = verificationPanel.Visible = false;
        diagnosticsCollapsed = false;
        technicalWasOpen = false;
        detail.Visible = false;
        diagnosticToggle.Visible = false;
        diagnosticResult.ClearResult();
        SetWamResultsVisible(false);
        statusBanner!.Visible = true;
        // The TPM/BitLocker area must not remain blank while other checks run.
        var liveDevice = TpmBitLockerSnapshot.Unknown;
        lastDeviceProtection = liveDevice;
        deviceSecurityCard.ShowResult(liveDevice, inProgress: true);
        activityLink.Visible = true;
        SetActivityVisible(true);

        // Bound the entire UI operation. The native Windows provider may hang;
        // a later diagnostic reuses any in-flight machine query instead of
        // starting more background WMI calls.
        using var request = new CancellationTokenSource();
        diagnosticCancellation = request;
        activityJournal.Add(ActivityStep.ReadOnlyCheck, ActivityOutcome.Started);
        activityJournal.Add(ActivityStep.WamEntra, ActivityOutcome.Started);
        activityJournal.Add(ActivityStep.OfficeLicensing, ActivityOutcome.Started);
        activityJournal.Add(ActivityStep.TpmBitLocker, ActivityOutcome.Started);
        activityJournal.Add(ActivityStep.NetworkPreflight, ActivityOutcome.Started);
        diagnosticStartedUtc = DateTimeOffset.UtcNow;
        diagnosticCompletedCount = 0;
        diagnosticLastCompleted = string.Empty;
        SetBusy(true);
        ShowDiagnosticProgress();
        diagnosticTimer.Start();
        try
        {
            var ownAccount = !elevated || desktopUser.SameAsProcess;
            var recordedSections = new HashSet<ActivityStep>();
            // Progress<T> posts callbacks asynchronously to WinForms. Do
            // not let a queued partial update overwrite final results after
            // the await has returned.
            bool acceptingProgress = true;
            var deviceProgress = new Progress<DeviceProbeProgress>(update =>
            {
                if (!acceptingProgress ||
                    !ReferenceEquals(diagnosticCancellation, request) ||
                    request.IsCancellationRequested || IsDisposed || Disposing)
                    return;
                var section = ActivityFromDiagnosticSection(update.Part);
                if (section == ActivityStep.ReadOnlyCheck) return;
                if (update.State == "STARTED")
                {
                    activityJournal.Add(section, ActivityOutcome.Started);
                    return;
                }
                if (update.Snapshot is not null)
                {
                    liveDevice = TpmBitLockerProbe.MergePart(
                        liveDevice, update.Part, update.Snapshot);
                    lastDeviceProtection = liveDevice;
                    deviceSecurityCard.ShowResult(liveDevice, inProgress: true);
                }
                if (!recordedSections.Add(section)) return;
                var result = update.State switch
                {
                    "FINISHED" => ActivityOutcome.Finished,
                    "TIME_LIMIT" => ActivityOutcome.TimeLimit,
                    _ => ActivityOutcome.NotVerified
                };
                activityJournal.Add(section, result);
            });
            var sectionProgress = new Progress<DiagnosticSectionProgress>(update =>
            {
                if (!acceptingProgress ||
                    !ReferenceEquals(diagnosticCancellation, request) ||
                    request.IsCancellationRequested || IsDisposed || Disposing)
                    return;
                diagnosticCompletedCount = update.Completed;
                diagnosticLastCompleted = update.LastCompleted;
                var section = ActivityFromDiagnosticSection(update.LastCompleted);
                if (section != ActivityStep.ReadOnlyCheck &&
                    recordedSections.Add(section))
                    activityJournal.Add(section, update.Verified
                        ? ActivityOutcome.Finished : ActivityOutcome.NotVerified);
                ShowDiagnosticProgress();
            });
            var data = await ReadOnlyDiagnosticScan.RunAsync(
                ownAccount, sectionProgress, request.Token, deviceProgress);
            acceptingProgress = false;
            if (IsDisposed || Disposing || request.IsCancellationRequested) return;

            // A Progress<T> callback may still be queued when the awaited scan
            // completes. Reconcile all stages from the final typed result,
            // rather than leaving any Activity entry stuck on "Started".
            var unverified = new HashSet<string>(data.UnverifiedSections,
                StringComparer.Ordinal);
            foreach (var part in TpmBitLockerProbe.PartNames)
            {
                var step = ActivityFromDiagnosticSection(part);
                if (recordedSections.Add(step))
                    activityJournal.Add(step, unverified.Contains(part) ||
                        unverified.Contains("TPM_BITLOCKER")
                            ? ActivityOutcome.NotVerified : ActivityOutcome.Finished);
            }
            foreach (var part in new[] {
                "WAM_ENTRA", "OFFICE_LICENSE", "TPM_BITLOCKER",
                "NETWORK_PREFLIGHT"
            })
            {
                var step = ActivityFromDiagnosticSection(part);
                if (!recordedSections.Add(step)) continue;
                bool incomplete = unverified.Contains(part) ||
                    (part == "TPM_BITLOCKER" &&
                     TpmBitLockerProbe.PartNames.Any(unverified.Contains));
                activityJournal.Add(step, incomplete ?
                    ActivityOutcome.NotVerified : ActivityOutcome.Finished);
            }

            lastRawDiagnostic = data.WamReport;
            lastOfficeLicense = data.OfficeLicense;
            lastOfficeActivation = data.OfficeActivation;
            lastPreflight = data.Preflight;
            lastDeviceProtection = data.DeviceProtection;
            detail.Visible = false;
            DisplayDiagnosis(data.WamReport);

            // Never claim success when any section timed out or could not be
            // verified. Show partial evidence rather than an endless spinner.
            if (data.UnverifiedSections.Length > 0)
            {
                // A partial scan must not offer a one-click repair based on
                // WAM alone when device/Office evidence was not obtained.
                applyRecommendation.Visible = false;
                string missing = string.Join(", ",
                    data.UnverifiedSections.Select(DiagnosticSectionLabel));
                // Not-verified sections are real diagnostic outcomes. UI-only
                // statuses do not contain identifiers or provider output.
                foreach (var section in data.UnverifiedSections)
                {
                    var step = ActivityFromDiagnosticSection(section);
                    if (step != ActivityStep.ReadOnlyCheck &&
                        recordedSections.Add(step))
                        activityJournal.Add(step, ActivityOutcome.NotVerified);
                }
                activityJournal.Add(ActivityStep.ReadOnlyCheck,
                    data.ReachedDeadline ? ActivityOutcome.TimeLimit : ActivityOutcome.Partial);
                detail.Text += Environment.NewLine + Environment.NewLine +
                    UpdateUiText.T("Unverified sections: ", "Непроверенные разделы: ",
                        "מקטעים שלא אומתו: ") + missing;
                ShowStatus(UpdateUiText.T(
                    "Partial read-only results. Not verified: " + missing +
                    ". Unknown does not mean healthy or defective.",
                    "Частичный результат. Не проверено: " + missing +
                    ". Неизвестный статус не означает исправность или поломку.",
                    "תוצאה חלקית. לא אומת: " + missing +
                    ". סטטוס לא ידוע אינו הוכחה לתקינות או לתקלה."), UiGlyph.Info);
            }
            else
            {
                activityJournal.Add(ActivityStep.ReadOnlyCheck, ActivityOutcome.Finished);
            }
            // Make WAM/Office findings immediately discoverable after completion.
            // TPM/BitLocker remains directly below and retains its own details.
            scrollViewport?.ScrollControlIntoView(diagnosticResult);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (IsDisposed || Disposing) return;
            // Keep completed read-only evidence and mark unfinished values unknown.
            deviceSecurityCard.ShowResult(liveDevice);
            activityJournal.Add(ActivityStep.ReadOnlyCheck, ActivityOutcome.Cancelled);
            ShowStatus(UpdateUiText.T(
                "Read-only check cancelled. No Office or Windows security state was changed.",
                "Проверка отменена. Office и параметры безопасности Windows не изменены.",
                "הבדיקה בוטלה. Office והגדרות האבטחה לא שונו."), UiGlyph.Info);
        }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            deviceSecurityCard.ShowResult(liveDevice);
            activityJournal.Add(ActivityStep.ReadOnlyCheck, ActivityOutcome.Failed);
            lastRawDiagnostic = null;
            lastOfficeLicense = null;
            lastOfficeActivation = null;
            lastSmartDecision = null;
            recommendedHelpKey = null;
            officeLicenseSummary.Visible = false;
            officeVolumeLink.Visible = false;
            smartPanel.Visible = activationDetail.Visible = verificationPanel.Visible = false;
            diagnosticToggle.Visible = false;
            diagnosticResult.ClearResult();
            statusBanner!.Visible = true;
            ShowStatus(UpdateUiText.T(
                "Check could not complete", "Проверка не завершена", "הבדיקה לא הושלמה"), UiGlyph.Error);
            detail.Text = ex.GetType().Name;
            detail.Visible = false;
            technicalToggle.Visible = true;
            UpdateTechnicalLink();
        }
        finally
        {
            if (ReferenceEquals(diagnosticCancellation, request))
                diagnosticCancellation = null;
            diagnosticTimer.Stop();
            activityJournal.SetCancellationAvailable(false);
            if (!IsDisposed && !Disposing)
            {
                SetActivityVisible(false);
                SetBusy(false);
            }
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (busy || IsDisposed || Disposing) return;
        try
        {
            // Stable by default; preview remains opt-in inside Update Center.
            availableUpdate = await UpdaterIntegration.FindAsync(
                withPreview: UpdaterIntegration.InstalledIsPreview);
            if (IsDisposed || Disposing) return;
            ApplyText();
        }
        catch
        {
            // A transient GitHub failure must not block sign-in repair.
            // Retain a previously confirmed update, if any.
        }
    }

}
