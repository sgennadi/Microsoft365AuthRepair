using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Separate, explicitly gated administrator tools. The layout is responsive
// and the footer remains accessible when the content scrolls at 200% DPI.
// No diagnostic check invokes an action from this window automatically.
internal sealed class DeviceSecurityAdvancedForm : Form
{
    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);

    private readonly Label title = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label warning = UiTheme.Caption(string.Empty);
    private readonly Label rights = UiTheme.Caption(string.Empty);
    private readonly Label authorizationHeading = UiTheme.Caption(string.Empty);
    private readonly Label safeHeading = UiTheme.Caption(string.Empty);
    private readonly Label protectionHeading = UiTheme.Caption(string.Empty);
    private readonly Label dangerHeading = UiTheme.Caption(string.Empty);
    private readonly Label instruction = UiTheme.Caption(string.Empty);
    private readonly Label result = UiTheme.Caption(string.Empty);

    private readonly Label exportDescription = UiTheme.Caption(string.Empty);
    private readonly Label bitlockerDescription = UiTheme.Caption(string.Empty);
    private readonly Label tpmDescription = UiTheme.Caption(string.Empty);
    private readonly Label suspendDescription = UiTheme.Caption(string.Empty);
    private readonly Label resumeDescription = UiTheme.Caption(string.Empty);
    private readonly Label decryptDescription = UiTheme.Caption(string.Empty);

    // Never default this to checked. The exact phrase is never pre-filled.
    private readonly CheckBox approved = new()
    {
        AutoSize = true, Margin = new Padding(2, 7, 2, 6),
        AccessibleName = "I understand the risk and have a safe recovery-key backup"
    };
    private readonly TextBox confirmation = new()
    {
        Width = 286, CharacterCasing = CharacterCasing.Upper,
        RightToLeft = RightToLeft.No,
        Margin = new Padding(2, 3, 2, 4),
        AccessibleName = "Exact action phrase"
    };
    private readonly Button export = UiTheme.CreateActionButton(string.Empty);
    private readonly Button bitlocker = UiTheme.CreateActionButton(string.Empty);
    private readonly Button tpm = UiTheme.CreateActionButton(string.Empty);
    private readonly Button suspend = UiTheme.CreateActionButton(string.Empty);
    private readonly Button resume = UiTheme.CreateActionButton(string.Empty);
    private readonly Button decrypt = UiTheme.CreateActionButton(string.Empty, destructive: true);
    private readonly Button close = UiTheme.CreateActionButton(string.Empty);

    private readonly Panel scroll;
    private readonly TableLayoutPanel content;
    private readonly TableLayoutPanel footer;
    private readonly TableLayoutPanel authorization;
    private readonly TableLayoutPanel safeSection;
    private readonly TableLayoutPanel protectionSection;
    private readonly TableLayoutPanel dangerSection;
    private readonly List<TableLayoutPanel> actionRows = new();
    private bool adjustingLayout;
    private bool working;

    internal DeviceSecurityAdvancedForm()
    {
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(630, 480);
        Size = new Size(830, 700);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        Text = "Microsoft 365 Auth Repair — Advanced device security";
        ResponsiveLayout.FitInitialWindow(this, new Size(830, 690),
            new Size(870, 770), widthFraction: 0.44, heightFraction: 0.66);

        scroll = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty,
            Padding = new Padding(17, 13, 17, 14),
            BackColor = BackColor
        };
        content = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 7, Margin = Padding.Empty,
            BackColor = BackColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < content.RowCount; i++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        title.Margin = new Padding(0, 2, 0, 3);
        warning.Margin = new Padding(0, 4, 0, 5);
        rights.Margin = new Padding(0, 0, 0, 7);
        warning.ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Muted;

        authorization = CreateSection(authorizationHeading);
        authorization.RowCount = 4;
        authorization.RowStyles.Clear();
        for (int i = 0; i < 4; i++)
            authorization.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        instruction.Margin = new Padding(2, 0, 2, 3);
        authorization.Controls.Add(approved, 0, 1);
        authorization.Controls.Add(instruction, 0, 2);
        authorization.Controls.Add(confirmation, 0, 3);

        safeSection = CreateSection(safeHeading,
            ActionRow(export, exportDescription),
            ActionRow(bitlocker, bitlockerDescription),
            ActionRow(tpm, tpmDescription));
        protectionSection = CreateSection(protectionHeading,
            ActionRow(suspend, suspendDescription),
            ActionRow(resume, resumeDescription));
        dangerSection = CreateSection(dangerHeading, true,
            ActionRow(decrypt, decryptDescription));

        content.Controls.Add(title, 0, 0);
        content.Controls.Add(warning, 0, 1);
        content.Controls.Add(rights, 0, 2);
        content.Controls.Add(authorization, 0, 3);
        content.Controls.Add(safeSection, 0, 4);
        content.Controls.Add(protectionSection, 0, 5);
        content.Controls.Add(dangerSection, 0, 6);
        scroll.Controls.Add(content);

        // One anchored footer, instead of stretching the last action row or
        // burying Close in the scrollable operation list.
        footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
            Padding = new Padding(17, 7, 17, 9),
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.White
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        result.Margin = new Padding(0, 8, 14, 6);
        result.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        result.AccessibleName = "Last advanced device-security operation status";
        close.Margin = new Padding(6, 3, 0, 3);
        close.Anchor = AnchorStyles.Right;
        footer.Controls.Add(result, 0, 0);
        footer.Controls.Add(close, 1, 0);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, BackColor = BackColor
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(scroll, 0, 0);
        shell.Controls.Add(footer, 0, 1);
        Controls.Add(ResponsiveLayout.Center(this, shell, 870));

        SetText();
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        // Confirmation phrases always use Latin ASCII even in RTL layout.
        confirmation.RightToLeft = RightToLeft.No;
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(export, UiGlyph.Save);
        UiIcons.Assign(bitlocker, UiGlyph.Settings);
        UiIcons.Assign(tpm, UiGlyph.Settings);
        UiIcons.Assign(suspend, UiGlyph.Reset);
        UiIcons.Assign(resume, UiGlyph.Repair);
        UiIcons.Assign(decrypt, UiGlyph.Error, destructive: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        CancelButton = close;

        scroll.SizeChanged += (_, _) => AdjustGeometry();
        DpiChanged += (_, _) => AdjustGeometry();
        Shown += (_, _) => AdjustGeometry();
        FormClosing += (_, args) =>
        {
            if (working) args.Cancel = true; // Do not hide active sensitive work.
        };
        close.Click += (_, _) => Close();
        bitlocker.Click += (_, _) => OpenBitLocker();
        tpm.Click += (_, _) => OpenTpm();
        export.Click += async (_, _) => await ExportAsync();
        suspend.Click += async (_, _) => await RunCommandAsync(
            "SUSPEND", BitLockerAdminAction.SuspendOneRestart);
        resume.Click += async (_, _) => await RunCommandAsync(
            "RESUME", BitLockerAdminAction.Resume);
        decrypt.Click += async (_, _) => await RunCommandAsync(
            "DECRYPT", BitLockerAdminAction.Decrypt);
        AdjustGeometry();
        UpdateAvailability();
    }

    private static TableLayoutPanel CreateSection(Label heading,
        params TableLayoutPanel[] actionRows) => CreateSection(heading, false, actionRows);

    private static TableLayoutPanel CreateSection(Label heading,
        bool danger, params TableLayoutPanel[] rows)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = rows.Length + 1,
            Margin = new Padding(0, 9, 0, 0),
            Padding = new Padding(15, 8, 15, 10),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemInformation.HighContrast ? SystemColors.Window
                : danger ? Color.FromArgb(255, 249, 249) : Color.White
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i <= rows.Length; i++)
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        heading.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText
            : danger ? Color.FromArgb(155, 42, 49) : UiTheme.Navy;
        heading.Margin = new Padding(0, 1, 0, 5);
        section.Controls.Add(heading, 0, 0);
        for (int i = 0; i < rows.Length; i++)
            section.Controls.Add(rows[i], 0, i + 1);
        return section;
    }

    private TableLayoutPanel ActionRow(Button command, Label description)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0, 2, 0, 2)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        command.Margin = new Padding(0, 3, 13, 3);
        description.Margin = new Padding(0, 5, 0, 5);
        description.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        description.MaximumSize = new Size(440, 0);
        row.Controls.Add(command, 0, 0);
        row.Controls.Add(description, 1, 0);
        actionRows.Add(row);
        return row;
    }

    private static void SetStacked(TableLayoutPanel row, bool stacked)
    {
        if ((row.ColumnCount == 1) == stacked) return;
        var command = row.Controls.OfType<Button>().Single();
        var description = row.Controls.OfType<Label>().Single();
        row.SuspendLayout();
        row.Controls.Clear();
        row.ColumnStyles.Clear();
        row.RowStyles.Clear();
        row.ColumnCount = stacked ? 1 : 2;
        row.RowCount = stacked ? 2 : 1;
        row.ColumnStyles.Add(new ColumnStyle(
            stacked ? SizeType.Percent : SizeType.AutoSize, stacked ? 100f : 0f));
        if (!stacked)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (stacked) row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.Controls.Add(command, 0, 0);
        row.Controls.Add(description, stacked ? 0 : 1, stacked ? 1 : 0);
        row.ResumeLayout(performLayout: true);
    }

    // At high DPI/narrow widths, stack a description below its action instead
    // of clipping Russian/Hebrew text or introducing a horizontal scrollbar.
    private void AdjustGeometry()
    {
        if (adjustingLayout) return;
        int visible = scroll.ClientSize.Width - scroll.Padding.Horizontal;
        if (visible < 100) return;
        adjustingLayout = true;
        try
        {
            float scale = Math.Max(96, DeviceDpi) / 96f;
            int inner = Math.Max(200, visible - 34);
            var commands = new[] { export, bitlocker, tpm, suspend, resume, decrypt };
            int commandWidth = Math.Max((int)Math.Ceiling(225 * scale),
                commands.Max(button => button.GetPreferredSize(Size.Empty).Width));
            bool stacked = inner < (int)Math.Ceiling(640 * scale) ||
                inner - commandWidth < (int)Math.Ceiling(235 * scale);
            int height = Math.Max((int)Math.Ceiling(44 * scale),
                commands.Max(button => button.GetPreferredSize(Size.Empty).Height));
            foreach (var row in actionRows)
            {
                SetStacked(row, stacked);
                var command = row.Controls.OfType<Button>().Single();
                var description = row.Controls.OfType<Label>().Single();
                command.AutoSize = false;
                command.Size = new Size(Math.Min(commandWidth, inner - 8), height);
                command.Margin = stacked
                    ? new Padding(0, 3, 0, 2)
                    : new Padding(0, 3, 13, 3);
                description.MaximumSize = new Size(Math.Max(160,
                    stacked ? inner - 10 : inner - commandWidth - 22), 0);
                description.Margin = stacked
                    ? new Padding(0, 2, 0, 7)
                    : new Padding(0, 5, 0, 5);
            }
            int line = Math.Max(190, visible - 5);
            foreach (var label in new[] { title, warning, rights, authorizationHeading,
                instruction, safeHeading, protectionHeading, dangerHeading })
                label.MaximumSize = new Size(line, 0);
            approved.MaximumSize = new Size(Math.Max(185, visible - 38), 0);
            confirmation.Width = Math.Min((int)Math.Ceiling(300 * scale),
                Math.Max(185, visible - 40));
            result.MaximumSize = new Size(Math.Max(150,
                footer.ClientSize.Width - close.Width - footer.Padding.Horizontal - 30), 0);
        }
        finally { adjustingLayout = false; }
    }

    private void SetText()
    {
        title.Text = T("Advanced device security", "Расширенная защита устройства",
            "אבטחת מכשיר מתקדמת");
        warning.Text = T(
            "Optional administrator tools — not required to repair Microsoft 365 sign-in. Confirm recovery access and organizational approval before changing device protection.",
            "Дополнительные инструменты администратора, не предназначенные для исправления входа Microsoft 365. Перед изменениями проверьте доступ к ключам восстановления и разрешение IT.",
            "כלי מנהל אופציונליים, שאינם נדרשים לתיקון כניסה ל-Microsoft 365. ודא גישה לשחזור ואישור ארגוני.");
        rights.Text = Identity.IsElevated()
            ? T("Administrator active · operations target only the Windows system drive.",
                "Администратор · операции относятся только к системному диску Windows.",
                "מנהל פעיל · הפעולות מוגבלות לכונן המערכת של Windows.")
            : T("Read-only view · to use protected actions, reopen as an authorized administrator.",
                "Режим просмотра · для защищённых операций запустите с разрешения IT от администратора.",
                "תצוגה בלבד · להפעלת פעולות מוגנות יש לפתוח כמנהל מורשה.");

        authorizationHeading.Text = T("AUTHORIZATION", "ПОДТВЕРЖДЕНИЕ", "אישור פעולה");
        approved.Text = T(
            "I understand the risks and have verified a safe recovery-key backup or recovery access.",
            "Понимаю риски и проверил безопасную копию ключа восстановления или возможность восстановления.",
            "אני מבין את הסיכונים ווידאתי גיבוי בטוח של מפתח השחזור או גישת שחזור.");
        instruction.Text = T(
            "Type the phrase for your action: EXPORT, SUSPEND, RESUME, DECRYPT or CLEAR TPM. A final Windows-style confirmation follows.",
            "Введите фразу выбранного действия: EXPORT, SUSPEND, RESUME, DECRYPT или CLEAR TPM. Далее появится последнее подтверждение.",
            "הקלד את ביטוי הפעולה: EXPORT, SUSPEND, RESUME, DECRYPT או CLEAR TPM. לאחר מכן יופיע אישור נוסף.");
        confirmation.PlaceholderText = T("Exact action phrase", "Точная фраза действия",
            "ביטוי הפעולה המדויק");
        confirmation.AccessibleName = T("Exact action phrase", "Точная фраза подтверждения",
            "ביטוי האישור המדויק");

        safeHeading.Text = T("TOOLS & WINDOWS SETTINGS", "ИНСТРУМЕНТЫ И НАСТРОЙКИ",
            "כלים והגדרות Windows");
        protectionHeading.Text = T("PROTECTION CONTROLS", "УПРАВЛЕНИЕ ЗАЩИТОЙ",
            "ניהול הגנה");
        dangerHeading.Text = T("DANGER ZONE", "ОПАСНЫЕ ОПЕРАЦИИ", "אזור סיכון");

        export.Text = T("Export recovery key", "Экспорт ключа", "ייצוא מפתח שחזור");
        exportDescription.Text = T(
            "Create a new restricted local NTFS file with an OS recovery password. This is a secret: store it offline.",
            "Создать защищённый локальный NTFS-файл с паролем восстановления. Это секрет — храните офлайн.",
            "צור קובץ NTFS מקומי מוגן עם סיסמת שחזור. יש לשמור אותו במצב לא מקוון.");
        bitlocker.Text = T("Windows BitLocker settings", "Настройки BitLocker", "הגדרות BitLocker");
        bitlockerDescription.Text = T(
            "Open native Windows settings. No protection changes are made by this shortcut.",
            "Открыть системные настройки Windows без изменения защиты.",
            "פתח את הגדרות Windows ללא שינוי של ההגנה.");
        tpm.Text = T("Open TPM management", "Управление TPM", "פתח ניהול TPM");
        tpmDescription.Text = T(
            "Open the Windows TPM console. A manual Clear there can destroy device keys; this shortcut requires CLEAR TPM confirmation.",
            "Открыть консоль TPM. Ручная очистка там может удалить ключи; для открытия требуется подтверждение CLEAR TPM.",
            "פתח את מסוף TPM. ניקוי ידני עלול למחוק מפתחות; נדרש אישור CLEAR TPM.");

        suspend.Text = T("Suspend (1 reboot)", "Пауза (1 перезагрузка)",
            "השהה לאתחול אחד");
        suspendDescription.Text = T(
            "Temporarily suspend system-drive key protection for one reboot.",
            "Временно приостановить защиту системного диска на одну перезагрузку.",
            "השהה זמנית את הגנת הכונן לאתחול אחד.");
        resume.Text = T("Resume protection", "Возобновить защиту", "חדש הגנה");
        resumeDescription.Text = T(
            "Resume existing BitLocker key protectors. This does not begin encryption.",
            "Возобновить работу защитников ключа. Это не запускает шифрование.",
            "חדש מגיני מפתח קיימים; פעולה זו אינה מתחילה הצפנה.");

        decrypt.Text = T("Decrypt / Turn off", "Расшифровать / Отключить", "פענח / כבה");
        decryptDescription.Text = T(
            "Begin full OS-drive decryption and turn BitLocker off. Verify backup and IT authorization first.",
            "Начать полную расшифровку системного диска и выключить BitLocker. Сначала проверьте копию ключа и разрешение IT.",
            "התחל פענוח מלא וכבה את BitLocker. יש לוודא גיבוי ואישור IT מראש.");

        close.Text = T("Close", "Закрыть", "סגור");
        result.Text = T("Ready · no operation has been requested.",
            "Готово · операции не запускались.",
            "מוכן · טרם התבקשה פעולה.");
        foreach (var pair in new[] {
            (Action: export, Description: exportDescription),
            (Action: bitlocker, Description: bitlockerDescription),
            (Action: tpm, Description: tpmDescription),
            (Action: suspend, Description: suspendDescription),
            (Action: resume, Description: resumeDescription),
            (Action: decrypt, Description: decryptDescription)
        })
        {
            pair.Action.AccessibleName = pair.Action.Text;
            pair.Action.AccessibleDescription = pair.Description.Text;
        }
        close.AccessibleName = close.Text;
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
        scroll.AutoScroll && scroll.Dock == DockStyle.Fill &&
        content.Controls.Contains(authorization) &&
        content.Controls.Contains(safeSection) &&
        content.Controls.Contains(protectionSection) &&
        content.Controls.Contains(dangerSection) &&
        export.Parent is TableLayoutPanel exportRow && exportRow.Parent == safeSection &&
        bitlocker.Parent is TableLayoutPanel bitlockerRow && bitlockerRow.Parent == safeSection &&
        tpm.Parent is TableLayoutPanel tpmRow && tpmRow.Parent == safeSection &&
        suspend.Parent is TableLayoutPanel suspendRow && suspendRow.Parent == protectionSection &&
        resume.Parent is TableLayoutPanel resumeRow && resumeRow.Parent == protectionSection &&
        decrypt.Parent is TableLayoutPanel decryptRow && decryptRow.Parent == dangerSection &&
        footer.GetControlFromPosition(0, 0) == result &&
        footer.GetControlFromPosition(1, 0) == close &&
        authorization.Controls.Contains(approved) &&
        authorization.Controls.Contains(confirmation) &&
        actionRows.Count == 6 &&
        !approved.Checked && confirmation.Text.Length == 0 &&
        confirmation.CharacterCasing == CharacterCasing.Upper &&
        decrypt is ModernButton { Role: ModernButton.Kind.Destructive } &&
        tpm is ModernButton { Role: ModernButton.Kind.Secondary };
}
