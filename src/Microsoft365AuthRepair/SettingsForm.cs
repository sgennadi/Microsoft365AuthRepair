using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Session-only options. Opening Settings never starts a repair, changes an
// identity, registers a task, removes an EXE, or sends a diagnostic report.
internal sealed class SettingsForm : Form
{
    private readonly ComboBox taskMethod = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Top,
        AccessibleName = "All-users scheduled task method"
    };
    private readonly CheckBox scheduleOnly = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 8)
    };
    private readonly CheckBox cleanup = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 8)
    };
    private readonly Button save = UiTheme.CreateActionButton(
        UpdateUiText.T("Save settings", "Сохранить настройки", "שמור הגדרות"), primary: true);
    private readonly Button cancel = UiTheme.CreateActionButton(
        UpdateUiText.T("Cancel", "Отмена", "ביטול"));
    private readonly bool cleanupEligible;

    internal ScheduledWorkerMode TaskMethod =>
        taskMethod.SelectedIndex == 1 ? ScheduledWorkerMode.PowerShell : ScheduledWorkerMode.Exe;

    internal bool ScheduleOnly => scheduleOnly.Checked;

    internal bool CleanupAfterRepair => cleanupEligible && cleanup.Checked;

    internal SettingsForm(ScheduledWorkerMode initialMethod, bool initialScheduleOnly,
        bool initialCleanup, bool canCleanup, string target, bool elevated)
    {
        cleanupEligible = canCleanup;
        Text = UpdateUiText.T("Microsoft 365 Auth Repair - Settings",
            "Microsoft 365 Auth Repair - Настройки",
            "Microsoft 365 Auth Repair - הגדרות");
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiTheme.BodyFont();
        Size = new Size(880, 590);
        MinimumSize = new Size(640, 450);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(880, 590),
            new Size(1060, 770), widthFraction: 0.74, heightFraction: 0.68);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(UiTheme.Caption(
            UpdateUiText.T("Settings", "Настройки", "הגדרות"), heading: true), 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 8) };
        var tasksPage = NewPage(UpdateUiText.T("All users", "Все пользователи", "כל המשתמשים"));
        var cleanupPage = NewPage(UpdateUiText.T("Cleanup", "Очистка", "ניקוי"));
        tabs.TabPages.AddRange([tasksPage, cleanupPage]);

        var tasksBody = PageBody(tasksPage);
        tasksBody.Controls.Add(Paragraph(UpdateUiText.T(
            $"Target: {target}. These settings apply only when User scope is All users on this PC. No task is created until you confirm a repair on the main window.",
            $"Выбрано: {target}. Эти параметры действуют только для режима «Все пользователи». До подтверждения на главном экране ничего не запускается.",
            $"יעד: {target}. ההגדרות חלות רק על כל המשתמשים. משימות ייווצרו רק לאחר אישור.")));
        tasksBody.Controls.Add(Paragraph(UpdateUiText.T(
            "Scheduled task method", "Способ выполнения задания", "שיטת הפעלת המשימה")));
        taskMethod.Items.AddRange([
            UpdateUiText.T("EXE — WAM + full reset",
                "EXE — WAM и полный сброс",
                "EXE — רישום WAM ואיפוס מלא"),
            UpdateUiText.T("PowerShell — WAM only",
                "PowerShell — только WAM",
                "PowerShell — WAM בלבד")
        ]);
        taskMethod.SelectedIndex = initialMethod == ScheduledWorkerMode.PowerShell ? 1 : 0;
        tasksBody.Controls.Add(taskMethod);
        scheduleOnly.Text = UpdateUiText.T(
            "Create tasks only; run when each user next signs in",
            "Только создать задания; запускать при следующем входе каждого пользователя",
            "ליצור משימות בלבד ולהפעיל בכניסה הבאה של כל משתמש");
        scheduleOnly.Checked = initialScheduleOnly;
        tasksBody.Controls.Add(scheduleOnly);
        tasksBody.Controls.Add(Paragraph(UpdateUiText.T(
            "Without this option, signed-in users can run immediately; offline users run at their next sign-in. PowerShell does not support Reset Office sign-in.",
            "Без этой опции у вошедших пользователей выполнение начинается сразу, у остальных — при входе. PowerShell не поддерживает сброс Office.",
            "ללא האפשרות, משתמשים מחוברים יכולים לפעול מיד. PowerShell אינו תומך באיפוס כניסת Office.")));

        var cleanupBody = PageBody(cleanupPage);
        void FitPageLabels(TabPage page, TableLayoutPanel body)
        {
            int width = Math.Max(190, page.ClientSize.Width - 64);
            foreach (Control item in body.Controls)
                if (item is Label or CheckBox)
                    item.MaximumSize = new Size(width, 0);
        }
        tasksPage.SizeChanged += (_, _) => FitPageLabels(tasksPage, tasksBody);
        cleanupPage.SizeChanged += (_, _) => FitPageLabels(cleanupPage, cleanupBody);
        cleanupBody.Controls.Add(Paragraph(UpdateUiText.T(
            "Optional cleanup of this user's portable program copy. Never deletes the protected all-users worker, installed applications, or another user's EXE.",
            "Необязательная очистка переносной копии программы текущего пользователя. Защищённый EXE для всех пользователей, установленная программа и чужие файлы не удаляются.",
            "ניקוי אופציונלי של עותק נייד למשתמש הנוכחי בלבד, ללא פגיעה בעותק המוגן.")));
        cleanup.Text = UpdateUiText.T(
            "Delete my portable EXE after successful current-user repair",
            "Удалить мой переносной EXE после успешного восстановления текущего пользователя",
            "מחק את עותק ה-EXE הנייד לאחר תיקון מוצלח למשתמש הנוכחי");
        cleanup.Checked = canCleanup && initialCleanup;
        cleanup.Enabled = canCleanup;
        cleanupBody.Controls.Add(cleanup);
        cleanupBody.Controls.Add(Paragraph(canCleanup
            ? UpdateUiText.T(
                "Off by default. The EXE is removed only after a confirmed successful current-user operation, after the program closes.",
                "По умолчанию выключено. EXE удаляется только после успешной операции для текущего пользователя и закрытия программы.",
                "כבוי כברירת מחדל. הקובץ יימחק רק לאחר תיקון מוצלח וסגירת התוכנה.")
            : UpdateUiText.T(
                elevated
                    ? "Unavailable in an administrator window. Run as a standard user from a portable EXE in your profile."
                    : "Unavailable for All users or for an installed, protected, or shared EXE. Select Current user and use your own portable copy.",
                elevated
                    ? "Недоступно в окне администратора. Запустите переносной EXE из своего профиля как обычный пользователь."
                    : "Недоступно для всех пользователей, установленного или защищённого EXE. Выберите текущего пользователя и переносную копию.",
                "אפשרות זו זמינה רק למשתמש רגיל המפעיל עותק נייד משלו.")));

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        save.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(save);
        footer.Controls.Add(cancel);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(ResponsiveLayout.Center(this, root, 1010));
        AcceptButton = save;
        CancelButton = cancel;

        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(save, UiGlyph.Save, primary: true);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    private static TabPage NewPage(string title) =>
        new(title) { AutoScroll = true, BackColor = SystemColors.Window };

    private static TableLayoutPanel PageBody(TabPage page)
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(body);
        return body;
    }

    private static Label Paragraph(string text)
    {
        var paragraph = UiTheme.Caption(text);
        paragraph.MaximumSize = new Size(640, 0);
        paragraph.Margin = new Padding(0, 8, 0, 8);
        return paragraph;
    }

}
