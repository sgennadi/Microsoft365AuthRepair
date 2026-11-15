using System.Diagnostics;
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
    private readonly Label repairExplanation = UiTheme.Caption(string.Empty);
    private readonly Label resetExplanation = UiTheme.Caption(string.Empty);
    private readonly Label allUsersSettingsSummary = UiTheme.Caption(string.Empty);
    private readonly ComboBox scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, Margin = new Padding(0, 4, 0, 8) };
    // Options are edited together in Settings; UAC continues to receive only
    // the two existing, validated command-line flags.
    private ScheduledWorkerMode allUsersTaskMethod = ScheduledWorkerMode.Exe;
    private bool allUsersScheduleOnly;
    private bool cleanupAfterCurrentRepair;
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, Margin = new Padding(10, 2, 0, 0) };
    private readonly Button repair = UiTheme.CreateActionButton(string.Empty, primary: true);
    private readonly Button reset = UiTheme.CreateActionButton(string.Empty, destructive: true);
    private readonly Button diagnose = UiTheme.CreateActionButton(string.Empty);
    private readonly Button updates = UiTheme.CreateActionButton(string.Empty);
    private readonly Button diagnostics = UiTheme.CreateActionButton(string.Empty);
    private readonly Button logs = UiTheme.CreateActionButton(string.Empty);
    private readonly Button settings = UiTheme.CreateActionButton(string.Empty);
    private readonly TextBox detail = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
        WordWrap = true, Margin = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 10f),
        MinimumSize = new Size(0, 170)
    };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 5, Visible = false, Style = ProgressBarStyle.Marquee };
    private TableLayoutPanel? mainLayout;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private bool busy;
    private UniversalUpdater.UpdateRelease? availableUpdate;

    internal MainForm(bool preferAllUsers = false,
        bool usePowerShellTasks = false, bool scheduleOnlyAtLogon = false)
    {
        allUsersTaskMethod = usePowerShellTasks ? ScheduledWorkerMode.PowerShell : ScheduledWorkerMode.Exe;
        allUsersScheduleOnly = scheduleOnlyAtLogon;
        Text = $"Microsoft 365 Auth Repair v{Repair.Version}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(785, 520);
        Size = new Size(1030, 715);
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(Size.Width, area.Width), Math.Min(Size.Height, area.Height));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true,
            ColumnCount = 1, RowCount = 8
        };
        mainLayout = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Fill, AutoSize = true };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(UiIcons.CreateBrand(), 0, 0);
        title.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.Add(title, 1, 0);
        language.Items.AddRange(["English", "Русский", "עברית"]);
        language.SelectedIndex = UiStrings.Language switch { "ru" => 1, "he" => 2, _ => 0 };
        top.Controls.Add(settings, 2, 0);
        top.Controls.Add(language, 3, 0);
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
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
        actions.Controls.Add(safeActions, 0, 1);
        actions.Controls.Add(resetExplanation, 0, 2);
        var resetActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        resetActions.Controls.Add(reset);
        actions.Controls.Add(resetActions, 0, 3);
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        tools.Controls.Add(diagnostics);
        tools.Controls.Add(updates);
        tools.Controls.Add(logs);
        actions.Controls.Add(tools, 0, 4);
        root.Controls.Add(actions, 0, 4);

        // All-users task choices are summarized here instead of occupying
        // several rows; their controls live in the Settings dialog.
        allUsersSettingsSummary.MaximumSize = new Size(850, 0);
        root.Controls.Add(allUsersSettingsSummary, 0, 5);

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
        diagnostics.UseMnemonic = false; // show literal & instead of treating it as accelerator
        UiIcons.Assign(updates, UiGlyph.Updates);
        UiIcons.Assign(logs, UiGlyph.Logs);
        UiIcons.Assign(settings, UiGlyph.Settings);
        UiIcons.TrackDpi(this);
        var hints = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500, ReshowDelay = 200 };
        hints.SetToolTip(repair, "Re-register Microsoft.AAD.BrokerPlugin for the selected user. No credentials or sign-in caches are deleted; apps may briefly close.");
        hints.SetToolTip(reset, "Destructive: remove Office/WAM identity and sign-in caches for the selected users. Re-authentication required; explicit confirmation.");
        hints.SetToolTip(diagnose, "Read-only health check with plain-language findings and next steps. It does not prove Office sign-in works.");
        hints.SetToolTip(diagnostics, "Review a sanitized diagnostic report before saving it locally.");
        hints.SetToolTip(updates, "Check Stable / Preview releases and safely update with rollback.");
        hints.SetToolTip(logs, "Open local repair logs without sending any data.");
        hints.SetToolTip(scope, "Current user never requires UAC. All users needs UAC and per-user tasks.");
        hints.SetToolTip(settings, "All-users task mode, optional portable cleanup and diagnostic receiver.");

        scope.SelectedIndexChanged += (_, _) => UpdateScopeExplanation();
        settings.Click += (_, _) =>
        {
            using var window = new SettingsForm(
                allUsersTaskMethod, allUsersScheduleOnly, cleanupAfterCurrentRepair,
                scope.SelectedIndex == 0 && PortableSelfCleanup.IsEligible(),
                scope.SelectedIndex == 1 ? UiStrings.T("all") : desktopUser.Name,
                elevated);
            if (window.ShowDialog(this) != DialogResult.OK) return;
            allUsersTaskMethod = window.TaskMethod;
            allUsersScheduleOnly = window.ScheduleOnly;
            cleanupAfterCurrentRepair = window.CleanupAfterRepair;
            UpdateScopeExplanation();
        };
        repair.Click += async (_, _) => await RunRepairAsync(false);
        reset.Click += async (_, _) => await RunRepairAsync(true);
        diagnose.Click += async (_, _) => await RunDiagnoseAsync();
        diagnostics.Click += (_, _) => new DiagnosticsCenter().ShowDialog(this);
        updates.Click += async (_, _) =>
        {
            using var window = new UpdateCenter();
            window.ShowDialog(this);
            await CheckForUpdatesAsync();
        };
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
        var lastUpdate = UpdateHandoff.ConsumeResult();
        if (lastUpdate is not null) ShowStatus(lastUpdate, lastUpdate.StartsWith("Updated successfully", StringComparison.Ordinal)
            ? UiGlyph.Success : UiGlyph.Error);
    }

    private void ApplyText()
    {
        title.Text = $"{UiStrings.T("title")}  v{Repair.Version}";
        subtitle.Text = UiStrings.T("subtitle");
        account.Text = UpdateUiText.T(
            $"Desktop user: {desktopUser.Name}  |  Session {Identity.SessionId()}  |  {(elevated ? "Administrator" : "Standard")}",
            $"Пользователь сеанса: {desktopUser.Name}  |  Сеанс {Identity.SessionId()}  |  {(elevated ? "Администратор" : "Обычный доступ")}",
            $"משתמש פעיל: {desktopUser.Name}  |  הפעלה {Identity.SessionId()}  |  {(elevated ? "מנהל" : "משתמש רגיל")}");
        scopeLabel.Text = UiStrings.T("scope") + ": ";
        var previous = scope.SelectedIndex < 0 ? 0 : scope.SelectedIndex;
        scope.Items.Clear();
        scope.Items.Add(CurrentUserOption());
        scope.Items.Add(UiStrings.T("all"));
        scope.SelectedIndex = Math.Min(previous, scope.Items.Count - 1);
        settings.Text = UpdateUiText.T("Settings", "Настройки", "הגדרות");
        repair.Text = UiStrings.T("repair");
        reset.Text = UiStrings.T("full");
        repairExplanation.Text = UpdateUiText.T(
            "1. Check Entra/WAM: read-only. 2. Re-register WAM: restores the broker registration without deleting cached sign-in data.",
            "1. Проверить Entra/WAM: без изменений. 2. Перерегистрировать WAM: восстановить регистрацию без удаления данных входа.",
            "1. בדיקת Entra/WAM: ללא שינויים. 2. רישום WAM מחדש: ללא מחיקת נתוני התחברות.");
        resetExplanation.Text = UpdateUiText.T(
            "3. If sign-in still fails: reset Office/WAM sign-in caches. This signs users out and requires explicit confirmation.",
            "3. Если вход всё ещё не работает: сбросить кеши входа Office/WAM. Потребуется повторный вход и подтверждение.",
            "3. אם הכניסה עדיין נכשלת: איפוס מטמון ההתחברות של Office/WAM. נדרש אישור וכניסה מחדש.");
        diagnose.Text = UpdateUiText.T("Check & Diagnose", "Проверить и объяснить", "בדוק ואבחן");
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
        logs.Text = UiStrings.T("logs");
        foreach (var button in new[] { repair, reset, diagnose, diagnostics, updates, logs, settings })
            button.AccessibleName = button.Text;
        UpdateScopeExplanation();
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
        repair.Text = everyone && allUsersScheduleOnly
            ? UpdateUiText.T("Schedule WAM re-registration", "Назначить регистрацию WAM", "תזמן רישום WAM מחדש")
            : UiStrings.T("repair");
        reset.Text = everyone && allUsersScheduleOnly
            ? UpdateUiText.T("Schedule full reset", "Назначить полный сброс", "תזמן איפוס מלא")
            : UiStrings.T("full");
        repair.AccessibleName = repair.Text;
        reset.AccessibleName = reset.Text;
        allUsersSettingsSummary.Visible = everyone;
        allUsersSettingsSummary.Text = UpdateUiText.T(
            $"All-users tasks: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell (WAM only)" : "EXE")} · " +
                (allUsersScheduleOnly ? "next sign-in" : "run now if online") + " · Change in Settings",
            $"Для всех: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell (только WAM)" : "EXE")} · " +
                (allUsersScheduleOnly ? "при следующем входе" : "сразу у активных") + " · Изменить в настройках",
            $"משימות לכולם: {(allUsersTaskMethod == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE")} · " +
                (allUsersScheduleOnly ? "בכניסה הבאה" : "כעת למשתמשים מחוברים") + " · שינוי בהגדרות");
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
                selectedMode == ScheduledWorkerMode.PowerShell, onlySchedule, out var reason))
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
                prompt += Environment.NewLine + UpdateUiText.T(
                    $"Method: {selectedMode}; " + (onlySchedule ? "next sign-in only" : "run now where online, otherwise next sign-in"),
                    $"Способ: {(selectedMode == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE")}; " +
                        (onlySchedule ? "только при следующем входе" : "сразу у активных, у остальных при входе"),
                    $"שיטה: {selectedMode}; " + (onlySchedule ? "רק בכניסה הבאה" : "כעת למשתמשים פעילים"));
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
        SetBusy(true);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            var result = await Task.Run(() =>
            {
                if (!elevated) return full ? Repair.Run(quiet: true) == 0 ? "SUCCESS" : "FAILED" :
                    Repair.ReRegister(quiet: true) == 0 ? "SUCCESS" : "FAILED";
                return ScheduleAdminRepair(everyone, full, selectedMode, onlySchedule);
            });
            detail.AppendText(result + Environment.NewLine);
            var failed = result.Contains("FAILED", StringComparison.Ordinal);
            ShowStatus(failed ? UiStrings.T("failed") : elevated
                ? UpdateUiText.T("Per-user tasks queued; check user logs for completion.",
                    "Задачи пользователей назначены. Проверяйте результат в их журналах.",
                    "המשימות תוזמנו. בדוק את יומני המשתמשים.")
                : UiStrings.T("done"), failed ? UiGlyph.Error : UiGlyph.Success);
            if (!failed && !everyone && !elevated && cleanupAfterCurrentRepair &&
                PortableSelfCleanup.IsEligible())
            {
                PortableSelfCleanup.Queue();
                Close(); // Cleaner waits for this process to exit before deleting EXE.
            }
        }
        catch (Exception ex)
        {
            ShowStatus(UiStrings.T("failed"), UiGlyph.Error);
            detail.AppendText(ex.GetType().Name + ": " + ex.Message + Environment.NewLine);
        }
        finally { SetBusy(false); }
    }

    private static string ScheduleAdminRepair(bool everyone, bool full,
        ScheduledWorkerMode mode, bool onlySchedule)
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
        SetBusy(true);
        ShowStatus(UiStrings.T("running"), UiGlyph.Working);
        try
        {
            // dsregcmd/WAM information is account- and session-sensitive.
            // Never label an administrator's token as another user's healthy login.
            var raw = await Task.Run(Repair.DiagnoseReport);
            var report = WamHealthAnalysis.Evaluate(raw, !elevated || desktopUser.SameAsProcess);
            if (scope.SelectedIndex == 1)
                report = report with
                {
                    Details = report.Details + Environment.NewLine + Environment.NewLine +
                        UpdateUiText.T(
                            "Note: this check reflects only the current Windows session, NOT all user profiles.",
                            "Примечание: проверен только текущий сеанс Windows, а НЕ все пользовательские профили.",
                            "הערה: הבדיקה מתייחסת רק להפעלה הנוכחית, ולא לכל הפרופילים.")
                };
            detail.Text = report.Details;
            ShowStatus(report.Summary, report.Level switch
            {
                WamHealthLevel.Healthy => UiGlyph.Success,
                WamHealthLevel.RepairRecommended => UiGlyph.Error,
                _ => UiGlyph.Info
            });
            // Short screens must still reveal the result without manual scrolling.
            mainLayout?.ScrollControlIntoView(detail);
            detail.SelectionStart = 0;
            detail.ScrollToCaret();
        }
        catch (Exception ex)
        {
            ShowStatus(UiStrings.T("failed"), UiGlyph.Error);
            detail.Text = UpdateUiText.T(
                "Check could not complete: ", "Проверка не завершена: ", "הבדיקה לא הושלמה: ") +
                ex.GetType().Name;
            mainLayout?.ScrollControlIntoView(detail);
        }
        finally { SetBusy(false); }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (busy || IsDisposed || Disposing) return;
        try
        {
            // Stable by default; preview remains opt-in inside Update Center.
            availableUpdate = await UpdaterIntegration.FindAsync(withPreview: false);
            if (IsDisposed || Disposing) return;
            ApplyText();
        }
        catch
        {
            // A transient GitHub failure must not block sign-in repair.
            // Retain a previously confirmed update, if any.
        }
    }

    private static void OpenLogs()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft365AuthRepair", "Logs");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}
