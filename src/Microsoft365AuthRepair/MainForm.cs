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
    private readonly Label officeLicenseSummary = UiTheme.Caption(string.Empty);
    private readonly LinkLabel officeVolumeLink = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(7, 4, 0, 8)
    };
    private OfficeLicenseSnapshot? lastOfficeLicense;
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
    private TableLayoutPanel? mainLayout;
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(20), AutoScroll = true,
            ColumnCount = 1, RowCount = 13,
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas
        };
        mainLayout = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 12; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Only the canvas receives unused space on a maximized window.
        // Never stretch a read-only textbox into an empty grey rectangle.
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
        allUsersSettingsSummary.MaximumSize = new Size(850, 0);
        root.Controls.Add(allUsersSettingsSummary, 0, 5);

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
        root.Controls.Add(banner, 0, 6);

        // Immediately visible diagnosis, before any optional raw details.
        root.Controls.Add(diagnosticResult, 0, 7);
        officeLicenseSummary.Visible = false;
        officeLicenseSummary.Margin = new Padding(4, 8, 0, 4);
        root.Controls.Add(officeLicenseSummary, 0, 8);
        root.Controls.Add(officeVolumeLink, 0, 9);
        root.Controls.Add(technicalToggle, 0, 10);
        root.Controls.Add(detail, 0, 11);
        Controls.Add(ResponsiveLayout.Center(this, root, 1340));
        Controls.Add(progress);
        progress.BringToFront();

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(repair, UiGlyph.Repair, primary: true);
        UiIcons.Assign(reset, UiGlyph.Reset, destructive: true);
        UiIcons.Assign(diagnose, UiGlyph.Search);
        UiIcons.Assign(diagnostics, UiGlyph.Reports);
        diagnostics.UseMnemonic = false; // show literal & instead of treating it as accelerator
        UiIcons.Assign(updates, UiGlyph.Updates);
        UiIcons.TrackDpi(this);
        technicalToggle.LinkClicked += (_, _) =>
        {
            detail.Visible = !detail.Visible;
            technicalWasOpen = detail.Visible;
            UpdateTechnicalLink();
            if (detail.Visible) mainLayout?.ScrollControlIntoView(detail);
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
        hints.SetToolTip(diagnose, "Read-only health check with plain-language findings and next steps. It does not prove Office sign-in works.");
        hints.SetToolTip(signInErrors, "Offline Microsoft 365 error-code guide: no report is sent and no account is changed.");
        hints.SetToolTip(diagnostics, "Generate a sanitized report, review the exact JSON and explicitly choose whether to send.");
        hints.SetToolTip(updates, "Check Stable / Preview releases and safely update with rollback.");
        hints.SetToolTip(scope, "Current user never requires UAC. All users needs UAC and per-user tasks.");
        hints.SetToolTip(settings, "Configure all-users repair tasks and review old scheduled workers.");

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
        repair.Click += async (_, _) => await RunRepairAsync(false);
        reset.Click += async (_, _) => await RunRepairAsync(true);
        diagnose.Click += async (_, _) => await RunDiagnoseAsync();
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
                settings.Text.Length > 3;
            scope.SelectedIndex = 0;
            UpdateScopeExplanation();
            return bulk && scope.SelectedIndex == 0 &&
                repair.Parent is not null && reset.Parent is not null;
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
        settings.AccessibleDescription = UpdateUiText.T(
            "Open the all-users task wizard and review protected worker cleanup",
            "Открыть мастер заданий всех пользователей и очистку worker-файлов",
            "פתח אשף משימות לכל המשתמשים וניקוי קובצי worker");
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
        foreach (Control button in new Control[] { repair, reset, diagnose, diagnostics, updates, settings })
            button.AccessibleName = button.Text;
        UpdateScopeExplanation();
        if (string.IsNullOrWhiteSpace(status.Text)) ShowStatus(UiStrings.T("statusReady"), UiGlyph.Info);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        if (lastRawDiagnostic is not null) DisplayDiagnosis(lastRawDiagnostic);
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
            officeLicenseSummary.Text = OfficeLicenseDiagnostics.Summary(lastOfficeLicense);
            detail.Text += Environment.NewLine + Environment.NewLine +
                OfficeLicenseDiagnostics.Details(lastOfficeLicense);
        }
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
            technicalToggle.Visible = false;
            detail.Visible = false;
        }
        else
        {
            diagnosticResult.Visible = true;
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
    }

    private void SetDiagnosticsCollapsed(bool collapsed)
    {
        if (lastRawDiagnostic is null) return;
        diagnosticsCollapsed = collapsed;
        if (collapsed) technicalWasOpen = detail.Visible;
        DisplayDiagnosis(lastRawDiagnostic);
        if (!collapsed) mainLayout?.ScrollControlIntoView(diagnosticResult);
    }

    private void UpdateDiagnosticsToggle()
    {
        diagnosticToggle.Text = diagnosticsCollapsed
            ? UpdateUiText.T("Expand results", "Развернуть результаты", "הצג תוצאות")
            : UpdateUiText.T("Collapse results", "Свернуть результаты", "הסתר תוצאות");
        diagnosticToggle.AccessibleName = diagnosticToggle.Text;
        diagnosticToggle.Visible = lastRawDiagnostic is not null;
    }

    internal bool CanCollapseDiagnostics => diagnosticToggle.Visible;
    internal bool AreDiagnosticsCollapsed => diagnosticsCollapsed;

    private void ShowStatus(string message, UiGlyph glyph)
    {
        status.Text = message;
        UiIcons.SetStatus(statusGlyph, glyph, message);
    }

    private void SetBusy(bool isBusy)
    {
        busy = isBusy;
        repair.Enabled = reset.Enabled = diagnose.Enabled = diagnostics.Enabled = updates.Enabled = settings.Enabled = !isBusy;
        scope.Enabled = !isBusy;
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
                (allUsersScheduleOnly ? "at next sign-in" : "now for online users") + " · Run or adjust in All users setup",
            $"Все пользователи: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell (только WAM)" : "EXE")} · " +
                (allUsersScheduleOnly ? "при следующем входе" : "сейчас для активных") + " · Запустить или изменить в мастере",
            $"כל המשתמשים: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE")} · " +
                (allUsersScheduleOnly ? "בכניסה הבאה" : "כעת למשתמשים מחוברים") + " · הפעל או עדכן באשף");
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
        lastRawDiagnostic = null;
        lastOfficeLicense = null;
        recommendedHelpKey = null;
        officeLicenseSummary.Visible = false;
        officeVolumeLink.Visible = false;
        diagnosticsCollapsed = false;
        technicalWasOpen = false;
        diagnosticToggle.Visible = false;
        diagnosticResult.ClearResult();
        statusBanner!.Visible = true;
        SetBusy(true);
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
            // The local user has already finished repair synchronously.
            // A scheduled all-users / elevated worker has NOT necessarily
            // finished: never auto-claim a queued task as a successful repair.
            verifyCurrentUserAfterSuccess = !failed && !everyone && !elevated;
            ShowStatus(failed ? UiStrings.T("failed") : elevated
                ? UpdateUiText.T("Per-user tasks queued; check user logs for completion.",
                    "Задачи пользователей назначены. Проверяйте результат в их журналах.",
                    "המשימות תוזמנו. בדוק את יומני המשתמשים.")
                : UiStrings.T("done"), failed ? UiGlyph.Error : UiGlyph.Success);

        }
        catch (Exception ex)
        {
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

    private async Task RunDiagnoseAsync()
    {
        if (busy) return;
        lastRawDiagnostic = null;
        lastOfficeLicense = null;
        recommendedHelpKey = null;
        officeLicenseSummary.Visible = false;
        officeVolumeLink.Visible = false;
        diagnosticsCollapsed = false;
        technicalWasOpen = false;
        detail.Visible = false;
        diagnosticToggle.Visible = false;
        diagnosticResult.ClearResult();
        statusBanner!.Visible = true;
        SetBusy(true);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            // dsregcmd / WAM signals are account and session specific.
            // Never show another administrator's results as the desktop user's.
            var data = await Task.Run(() =>
            {
                var raw = Repair.DiagnoseReport();
                OfficeLicenseSnapshot? license = null;
                try
                {
                    license = OfficeLicenseDiagnostics.Capture(
                        !elevated || desktopUser.SameAsProcess);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException or InvalidOperationException)
                {
                    // Licensing is a separate optional read-only probe. WAM
                    // results must remain usable even if Office is absent.
                }
                return (raw, license);
            });
            if (IsDisposed || Disposing) return;
            lastRawDiagnostic = data.raw;
            lastOfficeLicense = data.license;
            detail.Visible = false;
            DisplayDiagnosis(data.raw);
            // Make the summary visible rather than scrolling to a textbox.
            mainLayout?.ScrollControlIntoView(diagnosticResult);
        }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            lastRawDiagnostic = null;
            lastOfficeLicense = null;
            recommendedHelpKey = null;
            officeLicenseSummary.Visible = false;
            officeVolumeLink.Visible = false;
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
        finally { if (!IsDisposed && !Disposing) SetBusy(false); }
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
