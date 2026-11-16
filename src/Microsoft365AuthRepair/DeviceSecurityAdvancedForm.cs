using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Deliberately separate from the read-only TPM/BitLocker inspector. Neither
// regular Check & Diagnose nor viewing a result opens this panel. Every risky
// operation requires administrator rights, an opt-in checkbox, typed intent
// and a second explicit Windows confirmation. No action is ever automatic.
internal sealed class DeviceSecurityAdvancedForm : Form
{
    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);
    private readonly Label title = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label warning = UiTheme.Caption(string.Empty);
    private readonly Label rights = UiTheme.Caption(string.Empty);
    private readonly Label instruction = UiTheme.Caption(string.Empty);
    private readonly Label result = UiTheme.Caption(string.Empty);
    private readonly CheckBox approved = new()
    {
        AutoSize = true, Margin = new Padding(5, 12, 5, 12),
        AccessibleName = "I understand the risk and have a safe recovery-key backup"
    };
    private readonly TextBox confirmation = new()
    {
        Width = 260, Margin = new Padding(4, 2, 4, 12),
        AccessibleName = "Type the exact action name to confirm"
    };
    private readonly Button export = UiTheme.CreateActionButton(string.Empty);
    private readonly Button suspend = UiTheme.CreateActionButton(string.Empty);
    private readonly Button resume = UiTheme.CreateActionButton(string.Empty);
    private readonly Button decrypt = UiTheme.CreateActionButton(string.Empty, destructive: true);
    // Launching the native TPM console is not itself a TPM Clear. Preserve
    // the full confirmation gate but avoid a misleading destructive red button.
    private readonly Button tpm = UiTheme.CreateActionButton(string.Empty);
    private readonly Button bitlocker = UiTheme.CreateActionButton(string.Empty);
    private readonly Button close = UiTheme.CreateActionButton(string.Empty);
    private bool working;

    internal DeviceSecurityAdvancedForm()
    {
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(710, 570);
        Size = new Size(980, 770);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        Text = "Microsoft 365 Auth Repair — Advanced device security";
        ResponsiveLayout.FitInitialWindow(this, new Size(980, 770),
            new Size(1280, 950), 0.86, 0.82);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            Padding = new Padding(22, 16, 22, 16),
            Margin = Padding.Empty, ColumnCount = 1, RowCount = 12
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 11; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(warning, 0, 1);
        root.Controls.Add(rights, 0, 2);
        root.Controls.Add(approved, 0, 3);
        root.Controls.Add(instruction, 0, 4);
        root.Controls.Add(confirmation, 0, 5);

        var exportRow = ActionRow(export, T(
            "Export OS-volume numerical recovery password(s) to a NEW local NTFS .txt file. Plaintext secrets: store offline and never share.",
            "Экспортировать 48-значные пароли восстановления системного диска в НОВЫЙ локальный NTFS-файл .txt. Это открытые секреты — храните офлайн.",
            "ייצא סיסמאות שחזור לקובץ טקסט חדש בכונן NTFS מקומי. יש לשמור במצב לא מקוון."));
        var suspendRow = ActionRow(suspend, T(
            "Temporarily suspend OS-drive BitLocker key protection until one reboot. The drive may be exposed while suspended.",
            "Временно приостановить защиту ключа BitLocker системного диска на одну перезагрузку. Пока защита отключена, данные менее защищены.",
            "השהה הגנת BitLocker עד לאתחול הבא; ההגנה מופחתת."));
        var resumeRow = ActionRow(resume, T(
            "Resume existing OS-drive BitLocker key protectors. Does not start encryption.",
            "Возобновить действие защитников ключа BitLocker. Не запускает шифрование.",
            "חדש מגיני BitLocker קיימים ללא התחלת הצפנה."));
        var decryptRow = ActionRow(decrypt, T(
            "Start system-drive decryption and permanently turn BitLocker OFF. Key protectors will be removed after decryption completes.",
            "Начать расшифровку системного диска и отключение BitLocker. После завершения защитники ключа будут удалены.",
            "התחל פענוח כונן המערכת וכיבוי BitLocker; מגיני המפתח יוסרו בסיום."));
        var tpmRow = ActionRow(tpm, T(
            "Open the Windows TPM management console for a MANUAL Clear TPM. Clearing can invalidate Windows Hello, device keys and BitLocker recovery paths.",
            "Открыть системную консоль TPM для РУЧНОГО Clear TPM. Очистка может сделать недоступными Windows Hello, ключи устройства и шифрование.",
            "פתח את ניהול TPM של Windows לניקוי ידני בלבד. ניקוי עלול לבטל מפתחות ונתוני כניסה."));
        root.Controls.Add(exportRow, 0, 6);
        root.Controls.Add(suspendRow, 0, 7);
        root.Controls.Add(resumeRow, 0, 8);
        root.Controls.Add(decryptRow, 0, 9);
        root.Controls.Add(tpmRow, 0, 10);

        var bottom = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 15, 0, 0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var links = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill
        };
        links.Controls.Add(bitlocker);
        links.Controls.Add(close);
        bottom.Controls.Add(links, 0, 0);
        result.MaximumSize = new Size(800, 0);
        bottom.Controls.Add(result, 0, 1);
        root.Controls.Add(bottom, 0, 11);
        Controls.Add(ResponsiveLayout.Center(this, root, 1100));

        var rows = new[] { exportRow, suspendRow, resumeRow, decryptRow, tpmRow };
        root.SizeChanged += (_, _) =>
        {
            int width = Math.Max(300, root.ClientSize.Width - root.Padding.Horizontal - 20);
            title.MaximumSize = new Size(width, 0);
            warning.MaximumSize = new Size(width, 0);
            rights.MaximumSize = new Size(width, 0);
            instruction.MaximumSize = new Size(width, 0);
            result.MaximumSize = new Size(width, 0);
            foreach (var row in rows)
                if (row.Controls[1] is Label label)
                    label.MaximumSize = new Size(Math.Max(170, width - 350), 0);
        };
        SetText();
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(export, UiGlyph.Save);
        UiIcons.Assign(suspend, UiGlyph.Reset);
        UiIcons.Assign(resume, UiGlyph.Repair);
        UiIcons.Assign(decrypt, UiGlyph.Error, destructive: true);
        UiIcons.Assign(tpm, UiGlyph.Settings);
        UiIcons.Assign(bitlocker, UiGlyph.Settings);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        CancelButton = close;

        FormClosing += (_, args) =>
        {
            if (working) args.Cancel = true; // Never hide an active sensitive action
        };
        close.Click += (_, _) => Close();
        bitlocker.Click += (_, _) => OpenBitLocker();
        export.Click += async (_, _) => await ExportAsync();
        suspend.Click += async (_, _) => await RunCommandAsync(
            "SUSPEND", BitLockerAdminAction.SuspendOneRestart);
        resume.Click += async (_, _) => await RunCommandAsync(
            "RESUME", BitLockerAdminAction.Resume);
        decrypt.Click += async (_, _) => await RunCommandAsync(
            "DECRYPT", BitLockerAdminAction.Decrypt);
        tpm.Click += (_, _) => OpenTpm();
        UpdateAvailability();
    }

    private static TableLayoutPanel ActionRow(Button action, string description)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0, 7, 0, 7)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        action.Margin = new Padding(3, 4, 14, 4);
        var label = UiTheme.Caption(description);
        label.MaximumSize = new Size(540, 0);
        label.Margin = new Padding(0, 6, 4, 6);
        row.Controls.Add(action, 0, 0);
        row.Controls.Add(label, 1, 0);
        return row;
    }

    private void SetText()
    {
        title.Text = T("Advanced device security", "Расширенные операции защиты",
            "פעולות אבטחה מתקדמות");
        warning.Text = T(
            "DANGER — these are optional administrator operations, unrelated to fixing an expired Microsoft 365 token. A TPM clear, BitLocker decryption or unprotected key may cause data loss or loss of recovery access. Check backups and your organization's policy first.",
            "ОПАСНО — это дополнительные действия администратора, не предназначенные для исправления истёкшего токена Microsoft 365. Очистка TPM и изменение BitLocker могут привести к потере данных и доступа. Сначала проверьте резервные копии и политику организации.",
            "אזהרה — פעולות מנהל שאינן מתקנות אסימון Microsoft 365 שפג תוקפו. שינוי TPM או BitLocker עלול לגרום לאובדן נתונים.");
        rights.Text = Identity.IsElevated()
            ? T("Administrator enabled · only the Windows system volume is targeted.",
                "Права администратора есть · действия относятся только к системному диску.",
                "הרשאות מנהל פעילות · רק כונן המערכת נבחר.")
            : T("Administrator rights required for actions. Reopen this application as administrator only when authorized.",
                "Для операций нужны права администратора. Запустите программу от администратора только с разрешения IT.",
                "נדרשות הרשאות מנהל. הפעל מחדש כמנהל רק באישור.");
        approved.Text = T("I understand the risk and have verified safe recovery-key backup / access.",
            "Понимаю риски и проверил наличие доступных резервных ключей восстановления.",
            "אני מבין את הסיכונים ווידאתי גיבוי מאובטח של מפתחות שחזור.");
        instruction.Text = T(
            "To authorize an action, check the box and type exactly: EXPORT, SUSPEND, RESUME, DECRYPT or CLEAR TPM. A second confirmation will appear.",
            "Для действия поставьте галочку и введите: EXPORT, SUSPEND, RESUME, DECRYPT или CLEAR TPM. Затем появится дополнительное подтверждение.",
            "סמן אישור והקלד EXPORT, SUSPEND, RESUME, DECRYPT או CLEAR TPM. יופיע אישור נוסף.");
        export.Text = T("Export recovery key", "Экспорт ключа восстановления", "ייצוא מפתח שחזור");
        suspend.Text = T("Suspend (1 reboot)", "Приостановить (1 запуск)", "השהה לאתחול אחד");
        resume.Text = T("Resume protection", "Возобновить защиту", "חדש הגנה");
        decrypt.Text = T("Decrypt / Turn off", "Расшифровать / Отключить", "פענח / כבה");
        tpm.Text = T("Open TPM management", "Открыть управление TPM",
            "פתח ניהול TPM");
        bitlocker.Text = T("Windows BitLocker settings", "Настройки BitLocker в Windows",
            "הגדרות BitLocker");
        close.Text = T("Close", "Закрыть", "סגור");
        result.Text = T("No advanced action has been started.",
            "Расширенные действия ещё не выполнялись.",
            "לא בוצעה פעולה מתקדמת.");
    }

    private void UpdateAvailability()
    {
        bool allowed = Identity.IsElevated() && !working;
        export.Enabled = suspend.Enabled = resume.Enabled =
            decrypt.Enabled = tpm.Enabled = allowed;
        bitlocker.Enabled = close.Enabled = !working;
        approved.Enabled = confirmation.Enabled = !working;
    }

    private bool Confirm(string phrase, string operation)
    {
        if (working || !Identity.IsElevated()) return false;
        if (!approved.Checked || !confirmation.Text.Equals(phrase,
                StringComparison.Ordinal))
        {
            MessageBox.Show(this,
                T("Check the risk acknowledgment and type exactly: " + phrase,
                    "Подтвердите риск галочкой и точно введите: " + phrase,
                    "יש לאשר סיכונים ולהקליד: " + phrase),
                T("Explicit confirmation required", "Требуется подтверждение",
                    "נדרש אישור"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return MessageBox.Show(this, operation, T(
            "Final confirmation — device security change",
            "Последнее подтверждение — изменение защиты",
            "אישור אחרון לשינוי אבטחה"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private async Task ExportAsync()
    {
        if (!Confirm("EXPORT", T(
            "A plaintext 48-digit BitLocker recovery password can unlock this drive. Export only to a safe local NTFS file; do not send or sync it. Continue?",
            "Пароль восстановления BitLocker позволяет открыть диск. Файл содержит ОТКРЫТЫЙ СЕКРЕТ. Только локальный NTFS, не отправляйте и не синхронизируйте. Продолжить?",
            "ייצוא סיסמת שחזור לקובץ טקסט רגיש בכונן מקומי בלבד. להמשיך?"))) return;
        using var picker = new SaveFileDialog
        {
            Title = T("Create NEW local recovery-password backup",
                "Создать НОВУЮ локальную копию recovery password",
                "צור קובץ גיבוי שחזור מקומי חדש"),
            FileName = "BitLocker-Recovery.txt",
            Filter = "Text file (*.txt)|*.txt",
            DefaultExt = "txt",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = false
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        working = true;
        UpdateAvailability();
        try
        {
            int count = await Task.Run(() =>
                DeviceSecurityAdvancedActions.ExportSystemRecoveryPasswords(picker.FileName));
            result.Text = T($"Exported {count} password(s) to a restricted local file. Keep it offline.",
                $"Сохранено ключей восстановления: {count}. Храните файл офлайн.",
                $"נשמרו {count} סיסמאות שחזור לקובץ מקומי.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Text = T("Export unavailable. The file was not confirmed. Verify BitLocker access and administrator rights.",
                "Не удалось подтвердить экспорт. Проверьте права администратора и состояние BitLocker.",
                "לא ניתן לאשר ייצוא. בדוק הרשאות ואת מצב BitLocker.");
        }
        finally
        {
            confirmation.Clear();
            working = false;
            if (!IsDisposed && !Disposing) UpdateAvailability();
        }
    }

    private async Task RunCommandAsync(string phrase, BitLockerAdminAction action)
    {
        string message = action switch
        {
            BitLockerAdminAction.Decrypt => T(
                "START FULL OS VOLUME DECRYPTION? This turns off BitLocker and removes protectors after decryption finishes. Confirm separate recovery backups and organization approval.",
                "НАЧАТЬ ПОЛНУЮ РАСШИФРОВКУ системного диска? BitLocker отключится, защитники удалятся после завершения. Подтвердите наличие копий и разрешение IT.",
                "להתחיל פענוח מלא של כונן המערכת? BitLocker יכובה ומגנים יוסרו."),
            BitLockerAdminAction.SuspendOneRestart => T(
                "Suspend system-drive protection for one reboot? The unlock key will be stored unprotected temporarily.",
                "Приостановить защиту системного диска на одну перезагрузку? Ключ временно будет незащищён.",
                "להשהות הגנה לאתחול אחד? המפתח יישמר ללא הגנה זמנית."),
            _ => T("Resume existing BitLocker key protection on the system drive?",
                "Возобновить действие защитников ключа BitLocker системного диска?",
                "לחדש את הגנת המפתח בכונן המערכת?")
        };
        if (!Confirm(phrase, message)) return;
        working = true;
        UpdateAvailability();
        try
        {
            bool accepted = await DeviceSecurityAdvancedActions.RunBitLockerActionAsync(action);
            result.Text = accepted
                ? T("Windows accepted the request. Recheck the actual encryption/protection state; decryption can continue after this dialog closes.",
                    "Windows приняла команду. Повторно проверьте состояние защиты; расшифровка может продолжаться после закрытия окна.",
                    "Windows קיבל את הפעולה. בדוק שוב את מצב ההצפנה וההגנה.")
                : T("Windows did not accept the request. No completion is claimed; check BitLocker settings.",
                    "Windows не приняла команду. Успех не подтверждён; проверьте настройки BitLocker.",
                    "הפעולה לא אושרה. בדוק את הגדרות BitLocker.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Text = T("Command result unknown or blocked. Recheck the device status before doing anything else.",
                "Команда заблокирована или результат неизвестен. Сначала проверьте фактическое состояние диска.",
                "תוצאת הפעולה אינה ידועה. בדוק את מצב הכונן.");
        }
        finally
        {
            confirmation.Clear();
            working = false;
            if (!IsDisposed && !Disposing) UpdateAvailability();
        }
    }

    private void OpenTpm()
    {
        if (!Confirm("CLEAR TPM", T(
            "DANGER: Clearing the TPM can invalidate keys, Windows Hello and access to encrypted drives. Verify BitLocker recovery escrow, backups and IT approval. This application will ONLY open the Windows TPM console; any Clear must be performed manually there.",
            "ОПАСНО: Clear TPM может уничтожить ключи и доступ к зашифрованным дискам. Проверьте восстановление BitLocker, резервные копии и разрешение IT. Программа ТОЛЬКО откроет системную консоль; очистку выполняют вручную в Windows.",
            "סכנה: ניקוי TPM עלול לשלול גישה למפתחות ונתונים. ייפתח רק ממשק Windows, ללא ניקוי אוטומטי."))) return;
        try { DeviceSecurityAdvancedActions.OpenTpmManagement(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Text = T("Windows TPM console unavailable.", "Консоль TPM недоступна.",
                "קונסולת TPM אינה זמינה.");
        }
        confirmation.Clear();
    }

    private void OpenBitLocker()
    {
        try { DeviceSecurityAdvancedActions.OpenBitLockerManagement(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result.Text = T("Windows BitLocker settings unavailable.",
                "Настройки BitLocker недоступны.", "הגדרות BitLocker אינן זמינות.");
        }
    }

    internal bool SelfTest() =>
        UiTheme.VerifyButtons(this, 7) &&
        export.Parent is TableLayoutPanel &&
        decrypt.Parent is TableLayoutPanel &&
        tpm.Parent is TableLayoutPanel &&
        !approved.Checked &&
        confirmation.Text.Length == 0;
}
