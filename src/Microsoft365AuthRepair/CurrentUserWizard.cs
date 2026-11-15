using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Single-screen setup for the desktop user's own identity.
// It never executes a repair, removes an EXE, or changes configuration until
// MainForm receives an explicit OK and independently confirms the operation.
internal sealed class CurrentUserWizard : Form
{
    private readonly RadioButton saveOnly = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 5, 0, 8),
        AccessibleName = "Save setup without running anything"
    };
    private readonly RadioButton wam = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 5, 0, 8),
        AccessibleName = "Continue to safe WAM registration confirmation"
    };
    private readonly RadioButton reset = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 5, 0, 8),
        AccessibleName = "Continue to destructive Office sign-in reset confirmation"
    };
    private readonly CheckBox cleanup = new()
    {
        AutoSize = true, Margin = new Padding(0, 8, 0, 6),
        AccessibleName = "Delete only my portable EXE after successful current-user repair"
    };
    private readonly Label cleanupHint = UiTheme.Caption(string.Empty);
    private readonly Button finish = UiTheme.CreateActionButton(
        UpdateUiText.T("Save & continue", "Сохранить и продолжить", "שמור והמשך"), primary: true);
    private readonly Button cancel = UiTheme.CreateActionButton(
        UpdateUiText.T("Cancel", "Отмена", "ביטול"));

    private readonly bool cleanupEligible;

    internal AllUsersWizardAction SelectedAction => reset.Checked
        ? AllUsersWizardAction.ResetOffice
        : wam.Checked ? AllUsersWizardAction.RepairWam : AllUsersWizardAction.SaveOnly;
    internal bool CleanupAfterRepair => cleanupEligible && cleanup.Checked;
    internal bool NeverRunsWithoutMainConfirmation => true;

    internal CurrentUserWizard(bool initialCleanup, bool canCleanup, bool elevated)
    {
        cleanupEligible = canCleanup;
        Text = UpdateUiText.T("Current user — setup",
            "Текущий пользователь — мастер", "משתמש נוכחי — אשף");
        StartPosition = FormStartPosition.CenterParent;
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        Size = new Size(760, 490);
        MinimumSize = new Size(580, 400);
        ResponsiveLayout.FitInitialWindow(this, new Size(760, 490),
            new Size(980, 670), widthFraction: 0.70, heightFraction: 0.58);

        var root = new TableLayoutPanel
        {
            AutoScroll = true, Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 7, Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 7; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = UiTheme.Caption(UpdateUiText.T(
            "Current user · Choose the action",
            "Текущий пользователь · Выбор действия",
            "משתמש נוכחי · בחירת פעולה"), heading: true);
        var explain = UiTheme.Caption(UpdateUiText.T(
            "Setup does not repair anything by itself. A chosen repair requires a separate confirmation in the main window.",
            "Сам мастер ничего не исправляет. Для выбранного восстановления потребуется отдельное подтверждение в главном окне.",
            "אשף ההגדרות לא מבצע תיקון. פעולה שנבחרה מחייבת אישור נפרד."));
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(explain, 0, 1);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            ColumnCount = 1, Padding = new Padding(5, 10, 5, 8)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        saveOnly.Text = UpdateUiText.T("Save options only",
            "Только сохранить параметры", "שמור אפשרויות בלבד");
        wam.Text = UpdateUiText.T("Re-register WAM (safe first repair)",
            "Восстановить регистрацию WAM (без очистки кешей)", "רישום WAM מחדש");
        reset.Text = UpdateUiText.T("Reset Office sign-in (last resort)",
            "Сбросить вход Office (крайняя мера)", "איפוס כניסת Office");
        saveOnly.Checked = true;
        foreach (var option in new[] { saveOnly, wam, reset })
        {
            actions.RowCount++;
            actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actions.Controls.Add(option, 0, actions.RowCount - 1);
        }
        root.Controls.Add(actions, 0, 2);

        var separator = new Label
        {
            Text = UpdateUiText.T("Optional cleanup",
                "Необязательная очистка", "ניקוי אופציונלי"),
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 4),
            Font = UiTheme.BodyFont()
        };
        root.Controls.Add(separator, 0, 3);

        cleanup.Text = UpdateUiText.T(
            "After successful repair, delete my own portable EXE when this app closes",
            "После успешного восстановления удалить мой переносной EXE при закрытии программы",
            "לאחר תיקון מוצלח, מחק את עותק ה-EXE הנייד שלי בסגירת האפליקציה");
        cleanup.Checked = canCleanup && initialCleanup;
        cleanup.Enabled = canCleanup;
        root.Controls.Add(cleanup, 0, 4);

        cleanupHint.Text = canCleanup
            ? UpdateUiText.T(
                "Off by default. Never deletes an installed app, protected worker, another user's files, or Office sign-in data.",
                "По умолчанию выключено. Не удаляет установленную программу, защищённый worker, чужие файлы или данные входа Office.",
                "כבוי כברירת מחדל. אינו מוחק תוכנה מותקנת או קבצים של משתמשים אחרים.")
            : UpdateUiText.T(
                elevated
                    ? "Portable cleanup is unavailable in an elevated window. Use your own EXE as the desktop user."
                    : "Only a standard user's own portable EXE can be deleted. Network/shared/installed copies are protected.",
                elevated
                    ? "Очистка переносной копии недоступна в окне администратора. Запустите собственный EXE обычным пользователем."
                    : "Удалить можно только собственный переносной EXE обычного пользователя. Сетевые и установленные копии защищены.",
                "ניקוי עותק נייד זמין רק למשתמש רגיל עבור הקובץ האישי שלו.");
        root.Controls.Add(cleanupHint, 0, 5);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 15, 0, 0)
        };
        finish.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(finish);
        footer.Controls.Add(cancel);
        root.Controls.Add(footer, 0, 6);

        Controls.Add(ResponsiveLayout.Center(this, root, 940));
        AcceptButton = finish;
        CancelButton = cancel;
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(finish, UiGlyph.Save, primary: true);
        UiIcons.TrackDpi(this);
        root.SizeChanged += (_, _) =>
        {
            int width = Math.Max(160, root.ClientSize.Width - root.Padding.Horizontal - 30);
            explain.MaximumSize = cleanupHint.MaximumSize = cleanup.MaximumSize =
                new Size(width, 0);
        };
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    internal static bool SelfTest()
    {
        using var wizard = new CurrentUserWizard(
            initialCleanup: true, canCleanup: true, elevated: false);
        if (!wizard.NeverRunsWithoutMainConfirmation ||
            wizard.SelectedAction != AllUsersWizardAction.SaveOnly ||
            !wizard.CleanupAfterRepair ||
            !UiTheme.VerifyButtons(wizard, 2)) return false;
        wizard.wam.Checked = true;
        if (wizard.SelectedAction != AllUsersWizardAction.RepairWam) return false;
        wizard.reset.Checked = true;
        if (wizard.SelectedAction != AllUsersWizardAction.ResetOffice) return false;

        using var ineligible = new CurrentUserWizard(
            initialCleanup: true, canCleanup: false, elevated: true);
        return !ineligible.cleanup.Enabled && !ineligible.CleanupAfterRepair &&
            ineligible.SelectedAction == AllUsersWizardAction.SaveOnly;
    }
}
