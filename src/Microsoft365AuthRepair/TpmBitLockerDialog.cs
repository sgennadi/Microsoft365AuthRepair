using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Local, read-only inspector: never exposes recovery keys, protectors, TPM IDs
// or the raw Win32_Tpm/Win32_EncryptableVolume objects.
internal sealed class TpmBitLockerDialog : Form
{
    private readonly Label heading = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label safety = UiTheme.Caption(string.Empty);
    private readonly Label status = UiTheme.Caption(string.Empty);
    // Grouped, keyboard-accessible status rows instead of one enormous
    // selectable raw-text field. All evidence remains local and read-only.
    private readonly Panel detailsArea = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true,
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas
    };
    private readonly TableLayoutPanel sections = new()
    {
        Dock = DockStyle.Top, AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1, Margin = Padding.Empty
    };
    private readonly Dictionary<string, Label> statusValues = new(StringComparer.Ordinal);
    private readonly ComboBox errorCode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 265,
        AccessibleName = "Reported Office sign-in error code"
    };
    private readonly Label errorDetails = UiTheme.Caption(string.Empty);
    private readonly Button refresh = UiTheme.CreateActionButton(
        UpdateUiText.T("Refresh", "Обновить", "רענן"), primary: true);
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private bool running;

    internal TpmBitLockerDialog(TpmBitLockerSnapshot? initial = null)
    {
        Text = "Microsoft 365 Auth Repair — " +
            UpdateUiText.T("TPM & BitLocker (read-only)", "TPM и BitLocker (только чтение)",
                "TPM ו-BitLocker (קריאה בלבד)");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(630, 490);
        Size = new Size(820, 680);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(820, 680),
            new Size(990, 820), widthFraction: 0.56, heightFraction: 0.73);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            AutoScroll = true, RowCount = 7, ColumnCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        heading.Text = UpdateUiText.T("Device protection · read-only",
            "Защита устройства · только чтение", "הגנת מכשיר · קריאה בלבד");
        safety.Text = UpdateUiText.T(
            "Office WAM, DPAPI and TPM attestation are separate checks. This view is read-only and never reads recovery keys. Optional administrator-only device actions are separately confirmed in Advanced. UNKNOWN may mean restricted access.",
            "WAM, DPAPI и TPM Attestation — разные проверки. Это окно только для чтения и не получает recovery keys. Отдельные действия администратора в Advanced требуют подтверждения. UNKNOWN может означать ограниченные права.",
            "אין ניקוי TPM, ביטול BitLocker או מחיקת נתוני כניסה. מפתחות שחזור אינם נקראים. UNKNOWN עשוי להעיד על חוסר הרשאות.");
        status.Text = UpdateUiText.T("No device snapshot loaded.",
            "Данные устройства ещё не считаны.", "נתוני המכשיר טרם נטענו.");
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(safety, 0, 1);
        root.Controls.Add(status, 0, 2);
        BuildSections();
        detailsArea.Controls.Add(sections);
        root.Controls.Add(detailsArea, 0, 3);
        var errorRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            WrapContents = true, Margin = new Padding(0, 12, 0, 6)
        };
        errorCode.Items.AddRange([
            "No error selected", "0x80090016 / -2146893802",
            "0x80090030", "0x80090031", "0x80090034",
            "0x80090035", "0xC0090016",
            "0x80070490 (AIK)", "0x800705B4 (timeout)"
        ]);
        errorCode.SelectedIndex = 0;
        errorRow.Controls.Add(UiTheme.Caption(UpdateUiText.T(
            "Office error:", "Ошибка Office:", "שגיאת Office:")));
        errorRow.Controls.Add(errorCode);
        root.Controls.Add(errorRow, 0, 4);
        errorDetails.MaximumSize = new Size(1030, 0);
        errorDetails.Margin = new Padding(0, 5, 0, 9);
        root.Controls.Add(errorDetails, 0, 5);
        var footer = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true, Dock = DockStyle.Fill
        };
        footer.Controls.Add(close);
        footer.Controls.Add(refresh);
        root.Controls.Add(footer, 0, 6);
        Controls.Add(ResponsiveLayout.Center(this, root, 1170));
        root.SizeChanged += (_, _) =>
        {
            int w = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 24);
            heading.MaximumSize = new Size(w, 0);
            safety.MaximumSize = new Size(w, 0);
            status.MaximumSize = new Size(w, 0);
            errorDetails.MaximumSize = new Size(w, 0);
        };
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(refresh, UiGlyph.Search, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        AcceptButton = refresh;
        CancelButton = close;
        close.Click += (_, _) => Close();
        refresh.Click += async (_, _) => await RefreshAsync();
        errorCode.SelectedIndexChanged += (_, _) => DescribeSelectedCode();
        DescribeSelectedCode();
        if (initial is not null) Render(initial);
        else Shown += async (_, _) => await RefreshAsync();
    }

    private void DescribeSelectedCode()
    {
        string code = errorCode.SelectedIndex switch
        {
            1 => "0x80090016", 2 => "0x80090030",
            3 => "0x80090031", 4 => "0x80090034",
            5 => "0x80090035", 6 => "0xC0090016",
            7 => "0x80070490", 8 => "0x800705B4", _ => ""
        };
        var suffix = TpmBitLockerSnapshot.DescribeError(code);
        errorDetails.Text = UpdateUiText.T("Interpretation (not a device diagnosis): ",
            "Объяснение кода (не диагноз устройства): ",
            "פירוש הקוד (לא אבחון התקן): ") + suffix;
    }

    private static string YesNoUnknown(bool? x) =>
        x is null ? "UNKNOWN" : x.Value ? "YES" : "NO";

    private void AddGroup(string en, string ru, string he,
        params (string Key, string En, string Ru, string He)[] rows)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = rows.Length + 1,
            Dock = DockStyle.Fill, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 9), Padding = new Padding(14, 10, 14, 11),
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        for (int i = 0; i < panel.RowCount; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var head = UiTheme.Caption(UpdateUiText.T(en, ru, he), heading: true);
        head.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        head.Margin = new Padding(0, 0, 0, 8);
        panel.Controls.Add(head, 0, 0);
        panel.SetColumnSpan(head, 2);
        for (int i = 0; i < rows.Length; i++)
        {
            var item = rows[i];
            var label = UiTheme.Caption(UpdateUiText.T(item.En, item.Ru, item.He));
            label.Margin = new Padding(0, 5, 12, 5);
            label.MaximumSize = new Size(330, 0);
            label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            var value = UiTheme.Caption("—");
            value.Font = new Font("Segoe UI Semibold", 9.7f, FontStyle.Bold);
            value.ForeColor = SystemInformation.HighContrast
                ? SystemColors.WindowText : UiTheme.Navy;
            value.Margin = new Padding(0, 5, 0, 5);
            value.MaximumSize = new Size(390, 0);
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            statusValues.Add(item.Key, value);
            panel.Controls.Add(label, 0, i + 1);
            panel.Controls.Add(value, 1, i + 1);
        }
        sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sections.Controls.Add(panel, 0, sections.RowCount);
        sections.RowCount++;
    }

    private void BuildSections()
    {
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddGroup("TPM & SECURITY PROCESSOR", "TPM И ПРОЦЕССОР БЕЗОПАСНОСТИ",
            "TPM ומעבד אבטחה",
            ("tpm", "Detected", "Обнаружен", "זוהה"),
            ("spec", "TPM specification", "Версия TPM", "גרסת TPM"),
            ("ready", "Ready", "Готовность", "מוכן"),
            ("enabled", "Enabled", "Включён", "מופעל"),
            ("activated", "Activated", "Активирован", "הופעל"),
            ("owned", "Owned / provisioned", "Инициализирован", "בעלות ואספקה"),
            ("lockout", "TPM lockout", "Блокировка TPM", "נעילת TPM"),
            ("manufacturer", "Manufacturer category", "Производитель", "קטגוריית יצרן"),
            ("firmware", "TPM firmware (local only)", "Прошивка (локально)", "קושחה מקומית"));
        AddGroup("SYSTEM DRIVE ENCRYPTION", "ШИФРОВАНИЕ СИСТЕМНОГО ДИСКА",
            "הצפנת כונן המערכת",
            ("protection", "BitLocker protection", "Защита BitLocker", "הגנת BitLocker"),
            ("conversion", "BitLocker encryption", "Шифрование BitLocker", "הצפנת BitLocker"),
            ("thirdparty", "Third-party products detected", "Сторонние продукты", "מוצרי צד שלישי"),
            ("thirdpartydrive", "Third-party drive encryption", "Стороннее шифрование диска", "הצפנת כונן צד שלישי"));
        AddGroup("WINDOWS SECURITY & PLATFORM", "БЕЗОПАСНОСТЬ WINDOWS",
            "אבטחת Windows ופלטפורמה",
            ("secure", "Secure Boot", "Безопасная загрузка", "אתחול מאובטח"),
            ("bootmode", "Firmware mode", "Режим загрузки", "מצב קושחה"),
            ("hvci_configured", "Memory Integrity configured", "Целостность памяти настроена", "שלמות הזיכרון הוגדרה"),
            ("hvci_running", "Memory Integrity running", "Целостность памяти работает", "שלמות הזיכרון פועלת"),
            ("vm", "Virtual machine guest", "Виртуальная машина", "מכונה וירטואלית"),
            ("vm_platform", "Virtualization platform", "Платформа виртуализации", "פלטפורמת וירטואליזציה"),
            ("os", "Windows release category", "Версия Windows", "גרסת Windows"));
        AddGroup("IDENTITY, DPAPI & ATTESTATION", "ИДЕНТИФИКАЦИЯ И АТТЕСТАЦИЯ",
            "זהות ואימות",
            ("dpapi", "DPAPI policy", "Политика DPAPI", "מדיניות DPAPI"),
            ("domain", "Domain joined", "Компьютер в домене", "חבר בתחום"),
            ("dc", "Writable domain controller", "Доступен контроллер записи", "בקר תחום לכתיבה"),
            ("aik", "AIK enrollment result", "Результат регистрации AIK", "תוצאת רישום AIK"),
            ("ek", "EK certificate import", "Импорт сертификата EK", "ייבוא אישור EK"));
    }

    private static string HumanState(string value) => value switch
    {
        "YES" => UpdateUiText.T("Yes", "Да", "כן"),
        "NO" => UpdateUiText.T("No", "Нет", "לא"),
        "UNKNOWN" or "NOT_VERIFIED" => UpdateUiText.T(
            "Not verified", "Не проверено", "לא אומת"),
        "ON" => UpdateUiText.T("On", "Включено", "פועל"),
        "OFF" => UpdateUiText.T("Off", "Выключено", "כבוי"),
        "FULLY_DECRYPTED" => UpdateUiText.T("Not encrypted", "Не зашифрован",
            "לא מוצפן"),
        "FULLY_ENCRYPTED" => UpdateUiText.T("Fully encrypted", "Зашифрован",
            "מוצפן"),
        "NONE_DETECTED" => UpdateUiText.T("Not detected", "Не обнаружено", "לא זוהה"),
        "NONE" => UpdateUiText.T("None", "Нет", "אין"),
        "NOT_APPLICABLE" => UpdateUiText.T("Not applicable", "Не применяется",
            "לא חל"),
        _ => value.Replace('_', ' ')
    };

    private void Render(TpmBitLockerSnapshot x)
    {
        status.Text = x.NeedsTpmReview
            ? UpdateUiText.T("TPM needs IT review. Do not clear it automatically.",
                "TPM требует проверки IT. Не очищайте его автоматически.",
                "יש לבדוק את TPM ללא ניקוי אוטומטי.")
            : x.NeedsAttestationReview
                ? UpdateUiText.T("TPM attestation needs review; hardware failure is not proven.",
                    "Нужна проверка аттестации TPM, поломка не доказана.",
                    "יש לבדוק אימות TPM; כשל חומרה לא הוכח.")
                : x.NeedsBitLockerReview
                    ? UpdateUiText.T("Encrypted OS drive reports protection off.",
                        "Системный диск зашифрован, но защита выключена.",
                        "הכונן מוצפן אך ההגנה כבויה.")
                    : UpdateUiText.T(
                        "Read-only device evidence. Unknown is not a failure; Office sign-in is separate.",
                        "Проверка без изменений. Неизвестно не означает ошибку; вход Office проверяется отдельно.",
                        "נתוני מכשיר לקריאה בלבד. לא ידוע אינו כשל.");
        void Set(string key, string value)
        {
            var cell = statusValues[key];
            cell.Text = HumanState(value);
            cell.AccessibleName = key + ": " + cell.Text;
        }
        Set("tpm", x.TpmPresent);
        Set("spec", x.TpmVersion);
        Set("ready", x.TpmReady is null ? "UNKNOWN" : x.TpmReady.Value ? "YES" : "NO");
        Set("enabled", x.TpmEnabled is null ? "UNKNOWN" : x.TpmEnabled.Value ? "YES" : "NO");
        Set("activated", x.TpmActivated is null ? "UNKNOWN" : x.TpmActivated.Value ? "YES" : "NO");
        Set("owned", x.TpmOwned is null ? "UNKNOWN" : x.TpmOwned.Value ? "YES" : "NO");
        Set("lockout", x.TpmLockedOut);
        Set("manufacturer", x.TpmManufacturer);
        Set("firmware", x.TpmFirmwareVersion);
        Set("protection", x.OsVolumeProtection);
        Set("conversion", x.OsVolumeConversion);
        Set("thirdparty", ThirdPartyDiskEncryptionProbe.IsDetected(x.ThirdPartyEncryptionProducts)
            ? ThirdPartyDiskEncryptionProbe.DisplayNames(x.ThirdPartyEncryptionProducts)
            : x.ThirdPartyEncryptionProducts);
        Set("thirdpartydrive", "NOT_VERIFIED");
        Set("secure", x.SecureBootState);
        Set("bootmode", x.FirmwareType);
        Set("hvci_configured", x.MemoryIntegrityConfigured);
        Set("hvci_running", x.MemoryIntegrityRuntime);
        Set("vm", x.VirtualMachineGuest);
        Set("vm_platform", x.VirtualMachinePlatform);
        Set("os", x.WindowsRelease);
        Set("dpapi", x.DpapiProtectionPolicy);
        Set("domain", x.DomainJoinStatus);
        Set("dc", x.WritableDcStatus);
        Set("aik", x.AikEnrollmentError);
        Set("ek", x.EkCertStore);
    }

    private async Task RefreshAsync()
    {
        if (running || IsDisposed || Disposing) return;
        running = true;
        refresh.Enabled = false;
        status.Text = UpdateUiText.T("Reading Windows security state...",
            "Читаю состояние защиты Windows...", "קורא את מצב האבטחה...");
        try
        {
            var result = await IsolatedDeviceProbe.CaptureAsync();
            if (!IsDisposed && !Disposing)
            {
                Render(result.Snapshot);
                if (result.UnverifiedParts.Length > 0)
                    status.Text = UpdateUiText.T(
                        "Partial security state: some provider checks were unavailable.",
                        "Частичные сведения: некоторые проверки недоступны.",
                        "נתונים חלקיים: חלק מהבדיקות אינן זמינות.");
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                status.Text = UpdateUiText.T("Status unavailable: ",
                    "Состояние недоступно: ", "המצב אינו זמין: ") + ex.GetType().Name;
        }
        finally
        {
            running = false;
            if (!IsDisposed && !Disposing) refresh.Enabled = true;
        }
    }

    internal bool SelfTest() =>
        errorCode.Items.Count == 9 &&
        statusValues.Count == 25 && detailsArea.AutoScroll &&
        sections.Controls.Count == 4 &&
        !string.IsNullOrWhiteSpace(safety.Text) &&
        UiTheme.VerifyButtons(this, 2);
}
