using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Three short steps: how, when, and what to do after finishing.
// The wizard only returns options. MainForm owns UAC and explicit confirmation.
internal enum AllUsersWizardAction { SaveOnly, RepairWam, ResetOffice }

internal sealed class AllUsersWizard : Form
{
    private readonly RadioButton exe = new()
    {
        AutoSize = true,
        AccessibleName = "EXE worker (recommended)",
        Margin = new Padding(0, 8, 0, 3)
    };
    private readonly RadioButton powershell = new()
    {
        AutoSize = true,
        AccessibleName = "PowerShell worker (WAM only)",
        Margin = new Padding(0, 10, 0, 3)
    };
    private readonly RadioButton runNow = new()
    {
        AutoSize = true,
        AccessibleName = "Run for signed-in users now",
        Margin = new Padding(0, 8, 0, 3)
    };
    private readonly RadioButton nextSignIn = new()
    {
        AutoSize = true,
        AccessibleName = "Run only at next sign-in",
        Margin = new Padding(0, 10, 0, 3)
    };

    private readonly RadioButton saveOnly = new()
    {
        AutoSize = true, AccessibleName = "Save settings without action",
        Margin = new Padding(0, 8, 0, 3)
    };
    private readonly RadioButton repairWam = new()
    {
        AutoSize = true, AccessibleName = "Continue to WAM repair confirmation",
        Margin = new Padding(0, 10, 0, 3)
    };
    private readonly RadioButton resetOffice = new()
    {
        AutoSize = true, AccessibleName = "Continue to Office sign-in reset confirmation",
        Margin = new Padding(0, 10, 0, 3)
    };
    private readonly Label heading = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label introduction = UiTheme.Caption(string.Empty);
    private readonly Label summary = UiTheme.Caption(string.Empty);
    private readonly Label safety = UiTheme.Caption(string.Empty);
    private readonly LinkLabel maintenanceLink = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 12, 0, 8)
    };
    private readonly Label maintenanceStatus = UiTheme.Caption(string.Empty);
    private readonly LinkLabel taskStatusLink = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 12, 0, 5)
    };
    private readonly CheckBox removeUnusedWorkers = new()
    {
        AutoSize = true, Checked = false, Visible = false,
        Margin = new Padding(0, 8, 0, 6)
    };
    private readonly Button cleanCompletedTasks = UiTheme.CreateActionButton(
        UpdateUiText.T("Clean old tasks", "Очистить старые задания", "נקה משימות ישנות"));
    private ScheduledWorkerSnapshot? maintenanceSnapshot;
    private bool maintenanceBusy;
    private readonly Button back = UiTheme.CreateActionButton(
        UpdateUiText.T("Back", "Назад", "הקודם"));
    private readonly Button next = UiTheme.CreateActionButton(
        UpdateUiText.T("Next", "Далее", "הבא"), primary: true);
    private readonly Button cancel = UiTheme.CreateActionButton(
        UpdateUiText.T("Cancel", "Отмена", "ביטול"));
    private readonly TableLayoutPanel methodPage = NewPage();
    private readonly TableLayoutPanel timingPage = NewPage();
    private readonly TableLayoutPanel actionPage = NewPage();
    private readonly Panel viewport = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = SystemColors.Window,
        Padding = new Padding(15),
        Margin = new Padding(0, 9, 0, 6)
    };
    private readonly List<Label> wrappingLabels = new();
    private readonly bool elevated;
    private int step;

    internal ScheduledWorkerMode TaskMethod =>
        powershell.Checked ? ScheduledWorkerMode.PowerShell : ScheduledWorkerMode.Exe;
    internal bool ScheduleOnly => nextSignIn.Checked;
    internal AllUsersWizardAction SelectedAction =>
        resetOffice.Enabled && resetOffice.Checked ? AllUsersWizardAction.ResetOffice :
        repairWam.Checked ? AllUsersWizardAction.RepairWam : AllUsersWizardAction.SaveOnly;
    internal int CurrentStep => step;
    internal bool IsReadOnlyUntilConfirmed => true;

    internal AllUsersWizard(ScheduledWorkerMode initialMethod, bool initialScheduleOnly, bool elevated)
    {
        this.elevated = elevated;
        Text = UpdateUiText.T("All users — setup guide", "Все пользователи — мастер настройки",
            "כל המשתמשים — אשף הגדרה");
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(850, 590);
        MinimumSize = new Size(610, 460);
        ResponsiveLayout.FitInitialWindow(this, new Size(850, 590),
            new Size(1060, 770), widthFraction: 0.72, heightFraction: 0.70);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(18), AutoScroll = true,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(heading, 0, 0);

        introduction.Text = UpdateUiText.T(
            "This guide configures existing local and domain Windows profiles on this PC. New profiles that have never signed in are not included.",
            "Мастер настраивает существующие локальные и доменные профили Windows на этом компьютере. Пользователи, которые ещё ни разу не входили, не включаются.",
            "האשף מגדיר פרופילי Windows מקומיים וארגוניים קיימים במחשב. פרופילים שטרם התחברו אינם נכללים.");
        introduction.Margin = new Padding(0, 7, 0, 7);
        layout.Controls.Add(introduction, 0, 1);

        exe.Text = UpdateUiText.T("EXE — recommended", "EXE — рекомендуется", "EXE — מומלץ");
        powershell.Text = UpdateUiText.T("PowerShell — WAM only", "PowerShell — только WAM", "PowerShell — WAM בלבד");
        runNow.Text = UpdateUiText.T("Now for signed-in users", "Сейчас для вошедших пользователей", "כעת למשתמשים מחוברים");
        nextSignIn.Text = UpdateUiText.T("Only at the next sign-in", "Только при следующем входе", "רק בכניסה הבאה");

        Add(methodPage, exe);
        Add(methodPage, Caption(UpdateUiText.T(
            "Supports Re-register WAM and Reset Office sign-in. Uses a protected EXE worker with each user's own Windows session.",
            "Поддерживает Re-register WAM и Reset Office sign-in. Защищённый EXE выполняется в сеансе самого пользователя.",
            "תומך ברישום WAM מחדש ובאיפוס Office. תוכנית EXE מוגנת רצה בהפעלה של כל משתמש.")));
        Add(methodPage, powershell);
        Add(methodPage, Caption(UpdateUiText.T(
            "Re-register WAM only. Requires PowerShell/AppX to be allowed by policy. Cannot perform full Office sign-in reset.",
            "Только Re-register WAM. Политики должны разрешать PowerShell/AppX. Полный сброс входа Office недоступен.",
            "רישום WAM מחדש בלבד. נדרשת הרשאה ל-PowerShell/AppX לפי מדיניות. איפוס Office מלא אינו נתמך.")));

        Add(timingPage, runNow);
        Add(timingPage, Caption(UpdateUiText.T(
            "Start tasks for currently signed-in users after you confirm the action. Offline users will run at their next sign-in. Microsoft apps may briefly close.",
            "После подтверждения действия задания запустятся у вошедших пользователей. У остальных — при следующем входе. Приложения Microsoft могут временно закрыться.",
            "לאחר אישור הפעולה, המשימות יחלו למשתמשים מחוברים. לאחרים — בכניסה הבאה. יישומי Microsoft עשויים להיסגר.")));
        Add(timingPage, nextSignIn);
        Add(timingPage, Caption(UpdateUiText.T(
            "Create tasks, but do not start them immediately—even for users already signed in. Each user's task runs when that user next signs in.",
            "Только создать задания, не запускать их сейчас даже для вошедших пользователей. Задание выполнится при следующем входе каждого пользователя.",
            "צור משימות בלבד, בלי להפעיל כעת גם עבור משתמשים מחוברים. המשימה תפעל בכניסה הבאה של כל משתמש.")));

        saveOnly.Text = UpdateUiText.T(
            "No action — keep choices for this session",
            "Без действий — оставить выбор для этого сеанса",
            "ללא פעולה — שמור בחירה להפעלה הנוכחית");
        repairWam.Text = UpdateUiText.T("Continue to Re-register WAM",
            "Перейти к восстановлению WAM", "המשך לרישום WAM מחדש");
        resetOffice.Text = UpdateUiText.T("Continue to Reset Office sign-in",
            "Перейти к сбросу входа Office", "המשך לאיפוס כניסת Office");
        Add(actionPage, saveOnly);
        Add(actionPage, Caption(UpdateUiText.T(
            "Return to the main window without creating any tasks.",
            "Вернуться в главное окно без создания заданий.",
            "חזור לחלון הראשי בלי ליצור משימות.")));
        Add(actionPage, repairWam);
        Add(actionPage, Caption(UpdateUiText.T(
            "For existing profiles. Restores AAD BrokerPlugin registration without clearing Office sign-in caches; requires another confirmation.",
            "Для существующих профилей. Восстанавливает регистрацию AAD BrokerPlugin без удаления кеша входа Office; потребуется подтверждение.",
            "לפרופילים קיימים. רישום AAD BrokerPlugin ללא מחיקת מטמון Office; נדרש אישור נוסף.")));
        Add(actionPage, resetOffice);
        Add(actionPage, Caption(UpdateUiText.T(
            "Last resort: deletes Office/WAM sign-in caches, requires signing in again, EXE only. Another warning appears before proceeding.",
            "Крайняя мера: удаляет кеши входа Office/WAM, требует повторного входа, только EXE. Перед запуском появится предупреждение.",
            "מוצא אחרון: מחיקת מטמון Office/WAM וכניסה מחדש; EXE בלבד. נדרשת אזהרה נוספת.")));

        taskStatusLink.Text = UpdateUiText.T(
            "View recent task statuses (read-only)",
            "Последние задания и их состояние (только просмотр)",
            "הצגת מצב משימות אחרונות (קריאה בלבד)");
        taskStatusLink.AccessibleName = taskStatusLink.Text;
        taskStatusLink.Enabled = elevated;
        Add(actionPage, taskStatusLink);
        // Administrator-only, explicitly requested housekeeping. No cleanup
        // occurs when "Finish" is clicked. Pending or running tasks remain.
        maintenanceLink.Text = UpdateUiText.T(
            "Review scheduled tasks & worker cleanup",
            "Проверить очистку заданий и worker-файлов",
            "בדוק ניקוי משימות וקבצי worker");
        maintenanceLink.AccessibleName = maintenanceLink.Text;
        Add(actionPage, maintenanceLink);
        maintenanceStatus.Text = elevated
            ? UpdateUiText.T(
                "Optional: inspect completed/expired tasks without stopping pending users.",
                "Дополнительно: проверить завершённые и просроченные задания, не прерывая ожидающих пользователей.",
                "בדיקה אופציונלית של משימות שהושלמו או שפג תוקפן.")
            : UpdateUiText.T(
                "Administrator rights are needed for all-users maintenance. No cleanup will run.",
                "Для обслуживания заданий всех пользователей нужны права администратора. Очистка не запустится.",
                "נדרשות הרשאות מנהל עבור ניקוי משימות.");
        Add(actionPage, maintenanceStatus);
        removeUnusedWorkers.Text = UpdateUiText.T(
            "Also remove shared PS1/EXE ONLY if no tasks remain",
            "Также удалить общий PS1/EXE, ТОЛЬКО если заданий больше нет",
            "מחק גם PS1/EXE משותפים רק אם לא נשארו משימות");
        Add(actionPage, removeUnusedWorkers);
        cleanCompletedTasks.Visible = false;
        Add(actionPage, cleanCompletedTasks);

        exe.Checked = initialMethod != ScheduledWorkerMode.PowerShell;
        powershell.Checked = !exe.Checked;
        runNow.Checked = !initialScheduleOnly;
        nextSignIn.Checked = initialScheduleOnly;
        saveOnly.Checked = true; // Destructive choices must never be preselected.
        resetOffice.Enabled = exe.Checked;
        viewport.Controls.Add(methodPage);
        viewport.Controls.Add(timingPage);
        viewport.Controls.Add(actionPage);
        timingPage.Visible = false;
        actionPage.Visible = false;
        layout.Controls.Add(viewport, 0, 2);

        summary.Margin = new Padding(0, 5, 0, 2);
        safety.Margin = new Padding(0, 4, 0, 6);
        safety.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : UiTheme.Navy;
        safety.Text = elevated
            ? UpdateUiText.T(
                "Finish never starts a task by itself. If you select a repair, a separate confirmation is required.",
                "«Готово» само ничего не запускает. Если выбрано восстановление, потребуется отдельное подтверждение.",
                "סיום לא מתחיל משימה. תיקון שנבחר דורש אישור נוסף.")
            : UpdateUiText.T(
                "Starting a repair also requires UAC. The elevated application will not automatically execute an action.",
                "Для запуска восстановления также потребуется UAC. Программа после повышения прав не выполнит действие автоматически.",
                "נדרשת גם הרשאת מנהל. האפליקציה לא תפעיל פעולה אוטומטית לאחר הרשאת מנהל.");
        var notes = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 0, 0, 5)
        };
        notes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notes.Controls.Add(summary, 0, 0);
        notes.Controls.Add(safety, 0, 1);
        layout.Controls.Add(notes, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 6, 0, 0)
        };
        footer.Controls.Add(next);
        footer.Controls.Add(back);
        footer.Controls.Add(cancel);
        layout.Controls.Add(footer, 0, 4);
        Controls.Add(ResponsiveLayout.Center(this, layout, 1000));

        AcceptButton = next;
        CancelButton = cancel;
        cancel.DialogResult = DialogResult.Cancel;
        next.Click += (_, _) => Advance();
        back.Click += (_, _) => { if (step > 0) { step--; UpdateStep(); } };
        exe.CheckedChanged += (_, _) => SyncMethodAndAction();
        powershell.CheckedChanged += (_, _) => SyncMethodAndAction();
        runNow.CheckedChanged += (_, _) => RefreshSummary();
        nextSignIn.CheckedChanged += (_, _) => RefreshSummary();
        saveOnly.CheckedChanged += (_, _) => RefreshSummary();
        repairWam.CheckedChanged += (_, _) => RefreshSummary();
        resetOffice.CheckedChanged += (_, _) => RefreshSummary();
        maintenanceLink.LinkClicked += async (_, _) => await InspectMaintenanceAsync();
        taskStatusLink.LinkClicked += (_, _) =>
        {
            if (maintenanceBusy || !elevated || !Identity.IsElevated()) return;
            using var viewer = new AllUsersTaskStatusForm();
            viewer.ShowDialog(this);
        };
        cleanCompletedTasks.Click += async (_, _) => await CleanMaintenanceAsync();
        viewport.SizeChanged += (_, _) => WrapDescriptions();
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(next, UiGlyph.Save, primary: true);
        UiIcons.Assign(back, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        UpdateStep();
    }

    private static TableLayoutPanel NewPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Padding = new Padding(6), Margin = Padding.Empty
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return page;
    }

    private Label Caption(string text)
    {
        var value = UiTheme.Caption(text);
        value.Margin = new Padding(14, 3, 0, 8);
        wrappingLabels.Add(value);
        return value;
    }

    private static void Add(TableLayoutPanel page, Control control)
    {
        page.RowCount++;
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(control, 0, page.RowCount - 1);
    }

    private void WrapDescriptions()
    {
        int width = Math.Max(160, viewport.ClientSize.Width - viewport.Padding.Horizontal - 32);
        foreach (var item in wrappingLabels)
            item.MaximumSize = new Size(width, 0);
        introduction.MaximumSize = new Size(width, 0);
        summary.MaximumSize = new Size(width, 0);
        safety.MaximumSize = new Size(width, 0);
    }

    private async Task InspectMaintenanceAsync()
    {
        if (maintenanceBusy || !elevated || !Identity.IsElevated()) return;
        maintenanceBusy = true;
        maintenanceLink.Enabled = false;
        cleanCompletedTasks.Enabled = false;
        maintenanceStatus.Text = UpdateUiText.T(
            "Checking our scheduled tasks and protected workers…",
            "Проверяю задания и защищённые worker-файлы…",
            "בודק משימות וקבצי worker…");
        try
        {
            var result = await Task.Run(ScheduledWorkerMaintenance.Inspect);
            if (IsDisposed || Disposing) return;
            maintenanceSnapshot = result;
            maintenanceStatus.Text = UpdateUiText.T(
                $"Tasks: {result.Total}; queued: {result.Pending}; running: {result.Running}; completed: {result.Completed}; expired: {result.Expired}; attention: {result.Attention}; unrelated: {result.Foreign}.",
                $"Заданий: {result.Total}; ожидают: {result.Pending}; выполняются: {result.Running}; завершены: {result.Completed}; просрочены: {result.Expired}; требуют проверки: {result.Attention}; другие: {result.Foreign}.",
                $"משימות: {result.Total}; ממתינות: {result.Pending}; פעילות: {result.Running}; הושלמו: {result.Completed}; פגו: {result.Expired}; דורשות בדיקה: {result.Attention}; אחרות: {result.Foreign}.");
            removeUnusedWorkers.Visible = result.PowerShellWorkerExists || result.ExeWorkerExists;
            removeUnusedWorkers.Checked = false;
            removeUnusedWorkers.Enabled = result.Total == 0;
            cleanCompletedTasks.Visible = result.Removable > 0 ||
                (result.Total == 0 && removeUnusedWorkers.Visible);
            cleanCompletedTasks.Enabled = cleanCompletedTasks.Visible;
            if (result.Total > 0 && removeUnusedWorkers.Visible)
                maintenanceStatus.Text += Environment.NewLine + UpdateUiText.T(
                    "Protected PS1/EXE are retained while ANY task exists.",
                    "Защищённые PS1/EXE сохраняются, пока существует ЛЮБОЕ задание.",
                    "קובצי PS1/EXE נשמרים כל עוד קיימת משימה.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                maintenanceStatus.Text = UpdateUiText.T(
                    "Could not inspect scheduled tasks: ",
                    "Не удалось проверить задания: ",
                    "לא ניתן לבדוק משימות: ") + ex.GetType().Name;
        }
        finally
        {
            maintenanceBusy = false;
            if (!IsDisposed && !Disposing) maintenanceLink.Enabled = true;
        }
        WrapDescriptions();
    }

    private async Task CleanMaintenanceAsync()
    {
        if (maintenanceBusy || maintenanceSnapshot is null ||
            !elevated || !Identity.IsElevated()) return;
        bool removeStale = maintenanceSnapshot.Removable > 0;
        bool removeFiles = removeUnusedWorkers.Enabled && removeUnusedWorkers.Checked;
        if (!removeStale && !removeFiles) return;
        var message = UpdateUiText.T(
            $"Delete {maintenanceSnapshot.Removable} completed/expired tasks?" +
            (removeFiles ? " Also delete shared workers ONLY if no tasks remain." : "") +
            " Waiting and running tasks are never removed. Continue?",
            $"Удалить {maintenanceSnapshot.Removable} завершённых/просроченных заданий?" +
            (removeFiles ? " Также удалить общие worker-файлы, если заданий не останется." : "") +
            " Ожидающие и работающие задания не удаляются. Продолжить?",
            $"למחוק {maintenanceSnapshot.Removable} משימות שהושלמו או שפגו?" +
            (removeFiles ? " למחוק גם קבצי worker כשלא נשארות משימות." : "") +
            " משימות ממתינות ופעילות לא יימחקו. להמשיך?");
        if (MessageBox.Show(this, message,
                UpdateUiText.T("Confirm worker cleanup", "Подтвердите очистку worker",
                    "אשר ניקוי worker"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        maintenanceBusy = true;
        cleanCompletedTasks.Enabled = false;
        maintenanceLink.Enabled = false;
        try
        {
            var result = await Task.Run(() =>
                ScheduledWorkerMaintenance.Clean(removeStale, removeFiles));
            if (IsDisposed || Disposing) return;
            maintenanceStatus.Text = result;
            maintenanceSnapshot = null; // Must recheck before a second cleanup.
            removeUnusedWorkers.Checked = false;
            removeUnusedWorkers.Visible = false;
            cleanCompletedTasks.Visible = false;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                maintenanceStatus.Text = UpdateUiText.T(
                    "Cleanup failed; refresh inventory: ",
                    "Ошибка очистки, обновите список: ",
                    "ניקוי נכשל: ") + ex.GetType().Name;
        }
        finally
        {
            maintenanceBusy = false;
            if (!IsDisposed && !Disposing) maintenanceLink.Enabled = true;
        }
        WrapDescriptions();
    }

    private void SyncMethodAndAction()
    {
        resetOffice.Enabled = exe.Checked;
        if (!exe.Checked && resetOffice.Checked)
            saveOnly.Checked = true;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var action = SelectedAction;
        var actionEn = action switch
        {
            AllUsersWizardAction.RepairWam => "WAM registration",
            AllUsersWizardAction.ResetOffice => "Office sign-in reset",
            _ => "no action"
        };
        var actionRu = action switch
        {
            AllUsersWizardAction.RepairWam => "регистрация WAM",
            AllUsersWizardAction.ResetOffice => "сброс входа Office",
            _ => "без действий"
        };
        var actionHe = action switch
        {
            AllUsersWizardAction.RepairWam => "רישום WAM",
            AllUsersWizardAction.ResetOffice => "איפוס כניסת Office",
            _ => "ללא פעולה"
        };
        summary.Text = UpdateUiText.T(
            $"Method: {(TaskMethod == ScheduledWorkerMode.Exe ? "EXE" : "PowerShell")} · " +
                (ScheduleOnly ? "next sign-in only" : "online now / others at sign-in") +
                " · Action: " + actionEn,
            $"Способ: {(TaskMethod == ScheduledWorkerMode.Exe ? "EXE" : "PowerShell")} · " +
                (ScheduleOnly ? "при следующем входе" : "вошедшие сейчас / остальные при входе") +
                " · Действие: " + actionRu,
            $"שיטה: {(TaskMethod == ScheduledWorkerMode.Exe ? "EXE" : "PowerShell")} · " +
                (ScheduleOnly ? "בכניסה הבאה" : "מחוברים כעת / האחרים בכניסה") +
                " · פעולה: " + actionHe);
        if (step == 2)
        {
            next.Text = action == AllUsersWizardAction.SaveOnly
                ? UpdateUiText.T("Done", "Готово", "סיום")
                : UpdateUiText.T("Review & continue", "Проверить и продолжить",
                    "בדוק והמשך");
            next.AccessibleName = next.Text;
        }
    }

    private void UpdateStep()
    {
        methodPage.Visible = step == 0;
        timingPage.Visible = step == 1;
        actionPage.Visible = step == 2;
        heading.Text = step switch
        {
            0 => UpdateUiText.T("All users · 1 of 3 — How to run",
                "Все пользователи · 1 из 3 — Способ выполнения",
                "כל המשתמשים · 1 מתוך 3 — אופן הפעלה"),
            1 => UpdateUiText.T("All users · 2 of 3 — When to run",
                "Все пользователи · 2 из 3 — Когда выполнять",
                "כל המשתמשים · 2 מתוך 3 — מועד ההפעלה"),
            _ => UpdateUiText.T("All users · 3 of 3 — Choose action",
                "Все пользователи · 3 из 3 — Выбор действия",
                "כל המשתמשים · 3 מתוך 3 — בחירת פעולה")
        };
        next.Text = step == 2
            ? UpdateUiText.T("Finish", "Готово", "סיום")
            : UpdateUiText.T("Next", "Далее", "הבא");
        next.AccessibleName = next.Text;
        back.Visible = step > 0;
        RefreshSummary();
        WrapDescriptions();
    }

    private void Advance()
    {
        if (step < 2)
        {
            step++;
            UpdateStep();
            return;
        }
        if (SelectedAction == AllUsersWizardAction.ResetOffice && TaskMethod != ScheduledWorkerMode.Exe)
            return;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal static bool SelfTest()
    {
        using var wizard = new AllUsersWizard(ScheduledWorkerMode.Exe,
            initialScheduleOnly: false, elevated: false);
        if (!wizard.IsReadOnlyUntilConfirmed || wizard.TaskMethod != ScheduledWorkerMode.Exe ||
            wizard.ScheduleOnly || wizard.CurrentStep != 0 ||
            wizard.SelectedAction != AllUsersWizardAction.SaveOnly)
            return false;
        wizard.Advance();
        wizard.Advance();
        if (wizard.CurrentStep != 2 || wizard.next.Text.Length == 0)
            return false;
        wizard.resetOffice.Checked = true;
        if (wizard.SelectedAction != AllUsersWizardAction.ResetOffice)
            return false;
        wizard.powershell.Checked = true;
        wizard.nextSignIn.Checked = true;
        if (wizard.resetOffice.Enabled || wizard.SelectedAction != AllUsersWizardAction.SaveOnly)
            return false;
        wizard.repairWam.Checked = true;
        return wizard.TaskMethod == ScheduledWorkerMode.PowerShell &&
            wizard.ScheduleOnly && wizard.SelectedAction == AllUsersWizardAction.RepairWam &&
            !wizard.cleanCompletedTasks.Visible &&
            !wizard.removeUnusedWorkers.Checked &&
            !wizard.taskStatusLink.Enabled &&
            wizard.summary.Text.Contains("PowerShell", StringComparison.Ordinal) &&
            UiTheme.VerifyButtons(wizard, 3);
    }
}
