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
    private readonly Label advancedExplanation = UiTheme.Caption(string.Empty);
    private readonly ComboBox scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, Margin = new Padding(0, 4, 0, 8) };
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
    private UniversalUpdater.UpdateRelease? availableUpdate;

    internal MainForm(bool preferAllUsers = false)
    {
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
        // Standard users may SELECT All users. Only executing that scope
        // requires a consented UAC restart; current-user operations never do.
        scope.Items.AddRange([CurrentUserOption(), UiStrings.T("all")]);
        scope.SelectedIndex = preferAllUsers && elevated ? 1 : 0;
        scope.Enabled = true;
        target.Controls.Add(privilege);
        root.Controls.Add(target, 0, 3);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        // Keep the reporting action visible beside the primary repair action.
        actions.Controls.Add(repair);
        actions.Controls.Add(diagnostics);
        actions.Controls.Add(diagnose);
        actions.Controls.Add(updates);
        actions.Controls.Add(logs);
        root.Controls.Add(actions, 0, 4);

        var advanced = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        advanced.Controls.Add(showAdvanced, 0, 0);
        var advancedInner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2
        };
        advancedInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        advancedInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        advancedInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        advancedInner.Controls.Add(reset, 0, 0);
        advancedExplanation.MaximumSize = new Size(725, 0);
        advancedExplanation.UseMnemonic = false;
        advancedInner.Controls.Add(advancedExplanation, 0, 1);
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
        diagnostics.UseMnemonic = false; // show literal & instead of treating it as accelerator
        UiIcons.Assign(updates, UiGlyph.Updates);
        UiIcons.Assign(logs, UiGlyph.Logs);
        UiIcons.TrackDpi(this);
        var hints = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500, ReshowDelay = 200 };
        hints.SetToolTip(repair, "Re-register WAM / AAD Broker Plugin without clearing user tokens.");
        hints.SetToolTip(reset, "Advanced: remove Office and WAM identity caches for the selected user scope.");
        hints.SetToolTip(diagnose, "Check Hybrid Join, device authentication, PRT and WAM status.");
        hints.SetToolTip(diagnostics, "Review a sanitized diagnostic report before saving it locally.");
        hints.SetToolTip(updates, "Check Stable / Preview releases and safely update with rollback.");
        hints.SetToolTip(logs, "Open local repair logs without sending any data.");
        hints.SetToolTip(scope, "Current user never requires UAC. All users on this PC requires administrator consent and uses per-user tasks.");

        showAdvanced.CheckedChanged += (_, _) =>
        {
            advancedPanel.Visible = showAdvanced.Checked;
            UpdateScopeExplanation();
        };
        scope.SelectedIndexChanged += (_, _) => UpdateScopeExplanation();
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
        showAdvanced.Text = UpdateUiText.T("Advanced", "Дополнительно", "מתקדם");
        repair.Text = UiStrings.T("repair");
        reset.Text = UiStrings.T("full");
        diagnose.Text = UiStrings.T("diagnose");
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
        foreach (var button in new[] { repair, reset, diagnose, diagnostics, updates, logs })
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
        repair.Enabled = reset.Enabled = diagnose.Enabled = diagnostics.Enabled = updates.Enabled = !isBusy;
        scope.Enabled = !isBusy;
        progress.Visible = isBusy;
    }

    private string CurrentUserOption() =>
        $"{UiStrings.T("current")} — {desktopUser.Name}";

    private void UpdateScopeExplanation()
    {
        if (scope.SelectedIndex < 0) return;
        bool everyone = scope.SelectedIndex == 1;
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

        var target = everyone
            ? UpdateUiText.T("All users with profiles on this PC (local and domain users)",
                "Все пользователи с профилями на этом ПК (локальные и доменные)",
                "כל המשתמשים עם פרופיל במחשב זה (מקומיים ודומיין)")
            : desktopUser.Name;
        privilege.Text = admin;
        advancedExplanation.Text = UpdateUiText.T(
            $"Target: {target}. Full reset clears Office/WAM sign-in caches; affected users may need to sign in again.",
            $"Для кого: {target}. Полный сброс очистит кеши Office/WAM; потребуется повторный вход.",
            $"יעד: {target}. איפוס מלא מוחק מטמון כניסה של Office/WAM; ייתכן שתידרש התחברות מחדש.");
        advancedExplanation.AccessibleDescription = admin;
        reset.AccessibleDescription = $"Full identity reset, target: {target}. {admin}";
        repair.AccessibleDescription = $"Re-register WAM, target: {target}. {admin}";
    }

    private async Task RunRepairAsync(bool full)
    {
        if (busy) return;
        var everyone = scope.SelectedIndex == 1;

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
            if (UserScopeSupport.TryElevateForAll(out var reason))
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

        if (everyone || full)
        {
            var target = everyone ? UiStrings.T("confirmAll") : UiStrings.T("confirmCurrent");
            var prompt = everyone && !full
                ? UpdateUiText.T(
                    "Run WAM re-registration for ALL existing user profiles on this PC? Online users may have Office apps interrupted; offline users will run at their next sign-in.",
                    "Запустить восстановление WAM у ВСЕХ пользователей с профилями на этом ПК? У активных пользователей могут закрыться приложения Office; у остальных — при следующем входе.",
                    "להריץ תיקון WAM לכל המשתמשים? ייתכן שיישומי Office ייסגרו.")
                : target;
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
                ? UpdateUiText.T("Per-user tasks queued; check user logs for completion.",
                    "Задачи пользователей назначены. Проверяйте результат в их журналах.",
                    "המשימות תוזמנו. בדוק את יומני המשתמשים.")
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
