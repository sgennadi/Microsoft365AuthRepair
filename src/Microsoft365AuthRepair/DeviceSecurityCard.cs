using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Separate device-security evidence from WAM/Entra app sign-in health.
// Only categories already collected read-only by Check & Diagnose are shown.
// Never put recovery keys, TPM authorization, device identifiers, provider
// output, registry paths or other raw sensitive material in this control.
internal sealed class DeviceSecurityCard : TableLayoutPanel
{
    private enum Status { Good, Review, Neutral, Unknown }

    private sealed class StatusTile : TableLayoutPanel
    {
        internal readonly Label Heading = UiTheme.Caption(string.Empty);
        internal readonly Label Value = UiTheme.Caption(string.Empty);

        internal StatusTile()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Dock = DockStyle.Fill;
            ColumnCount = 1;
            RowCount = 2;
            Margin = new Padding(3, 3, 6, 3);
            Padding = new Padding(10, 7, 10, 7);
            BackColor = SystemInformation.HighContrast ? SystemColors.Window
                : Color.FromArgb(247, 249, 252);
            BorderStyle = BorderStyle.FixedSingle;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Heading.Margin = new Padding(0, 0, 0, 3);
            Heading.ForeColor = SystemInformation.HighContrast
                ? SystemColors.WindowText : UiTheme.Muted;
            Value.Margin = Padding.Empty;
            Value.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            Controls.Add(Heading, 0, 0);
            Controls.Add(Value, 0, 1);
        }

        internal void Set(string heading, string value, Status state)
        {
            Heading.Text = heading;
            Value.Text = (state switch
            {
                Status.Good => "✓ ",
                Status.Review => "! ",
                Status.Neutral => "• ",
                _ => "? "
            }) + value;
            Value.ForeColor = SystemInformation.HighContrast
                ? SystemColors.WindowText
                : state switch
                {
                    Status.Good => UiTheme.Positive,
                    Status.Review => Color.FromArgb(158, 48, 60),
                    _ => UiTheme.Muted
                };
            AccessibleName = heading + ": " + value;
            AccessibleDescription = state switch
            {
                Status.Good => "Reported positive state",
                Status.Review => "Needs administrator review, no automatic fix",
                Status.Neutral => "Device policy dependent, not necessarily a fault",
                _ => "No verified status available"
            };
        }
    }

    private readonly Label title = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label explanation = UiTheme.Caption(string.Empty);
    private readonly Label caution = UiTheme.Caption(string.Empty);
    private readonly LinkLabel detailsLink = new()
    {
        AutoSize = true,
        TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(10, 8, 3, 5)
    };
    private readonly TableLayoutPanel header = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        ColumnCount = 2, RowCount = 1,
        Margin = Padding.Empty
    };
    private readonly TableLayoutPanel signalGrid = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0, 8, 0, 6)
    };
    private readonly StatusTile[] tiles =
        Enumerable.Range(0, 14).Select(_ => new StatusTile()).ToArray();
    private bool reshaping;
    private int displayedColumns;
    private TpmBitLockerSnapshot? current;
    private bool collecting;

    internal event EventHandler? DetailsRequested;

    internal DeviceSecurityCard()
    {
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 1;
        RowCount = 4;
        Margin = new Padding(0, 9, 0, 5);
        Padding = new Padding(14, 10, 14, 11);
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = SystemInformation.HighContrast
            ? SystemColors.Window : Color.White;
        Visible = false;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            RowStyles.Add(new RowStyle(SizeType.AutoSize));

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        title.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        title.ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Navy;
        title.Margin = new Padding(2, 4, 8, 5);
        explanation.ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Muted;
        explanation.Margin = new Padding(3, 5, 2, 5);
        caution.ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Muted;
        caution.Margin = new Padding(3, 7, 3, 2);
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(detailsLink, 1, 0);
        Controls.Add(header, 0, 0);
        Controls.Add(explanation, 0, 1);
        Controls.Add(signalGrid, 0, 2);
        Controls.Add(caution, 0, 3);

        detailsLink.LinkClicked += (_, _) =>
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        SizeChanged += (_, _) => UpdateGeometry();
        signalGrid.SizeChanged += (_, _) => UpdateGeometry();
        UpdateLanguage();
    }

    private static string T(string en, string ru, string he) =>
        UpdateUiText.T(en, ru, he);

    internal void UpdateLanguage()
    {
        title.Text = T("TPM & BITLOCKER · DEVICE SECURITY",
            "TPM И BITLOCKER · ЗАЩИТА УСТРОЙСТВА",
            "TPM ו-BitLocker · אבטחת מכשיר");
        detailsLink.Text = T("View full details", "Подробнее", "הצג פרטים מלאים");
        detailsLink.AccessibleName = T("Open TPM and BitLocker read-only details",
            "Открыть подробности TPM и BitLocker (только чтение)",
            "פתח פרטי TPM ו-BitLocker לקריאה בלבד");
        explanation.Text = collecting ? T(
            "Checking device security… results appear as providers finish.",
            "Проверяем защиту устройства… результаты появляются по мере завершения этапов.",
            "בדיקת אבטחת המכשיר… התוצאות מופיעות עם השלמת השלבים.") : T(
            "Separate read-only Windows security results; these are not Office sign-in tests.",
            "Отдельные результаты проверки защиты Windows — это не проверка входа в Office.",
            "תוצאות אבטחת Windows לקריאה בלבד; אינן בודקות כניסה ל-Office.");
        caution.Text = collecting ? T(
            "Pending values are not yet verified. This check does not change TPM, BitLocker or Office.",
            "Ожидающие данные ещё не проверены. Проверка не изменяет TPM, BitLocker или Office.",
            "ערכים ממתינים טרם אומתו. הבדיקה אינה משנה TPM, BitLocker או Office.") : T(
            "Unknown means not verified. Third-party FDE software detection does NOT prove disk encryption. No recovery keys are read.",
            "Не проверено — не значит отключено. Обнаружение стороннего шифратора НЕ доказывает шифрование диска. Ключи восстановления не считываются.",
            "לא אומת אינו כבוי. זיהוי מוצר הצפנה חיצוני אינו מוכיח שהכונן מוצפן. מפתחות שחזור אינם נקראים.");
        if (current is not null) UpdateValues(current);
        UpdateGeometry();
    }

    private string Unknown => collecting ?
        T("Checking…", "Проверяется…", "בבדיקה…") :
        T("Not verified", "Не проверено", "לא אומת");

    private void UpdateValues(TpmBitLockerSnapshot x)
    {
        tiles[0].Set(T("TPM detected", "TPM обнаружен", "זוהה TPM"),
            x.TpmPresent == "YES" ? T("Present", "Обнаружен", "קיים") :
            x.TpmPresent == "NO" ? T("Not present", "Отсутствует", "לא קיים") : Unknown,
            x.TpmPresent == "YES" ? Status.Good :
            x.TpmPresent == "NO" ? Status.Neutral : Status.Unknown);

        tiles[1].Set(T("TPM readiness", "Готовность TPM", "מוכנות TPM"),
            x.TpmPresent == "NO" ? T("Not applicable", "Не применяется", "לא חל") :
            x.TpmReady == true ? T("Ready", "Готов", "מוכן") :
            x.TpmReady == false ? T("Not ready", "Не готов", "לא מוכן") : Unknown,
            x.TpmPresent == "NO" ? Status.Neutral :
            x.TpmReady == true ? Status.Good :
            x.TpmReady == false && x.TpmPresent == "YES" ? Status.Review :
            Status.Unknown);

        tiles[2].Set(T("TPM lockout", "Блокировка TPM", "נעילת TPM"),
            x.TpmPresent == "NO" || x.TpmLockedOut == "NOT_APPLICABLE"
                ? T("Not applicable", "Не применяется", "לא חל") :
            x.TpmLockedOut == "NO" ? T("Not locked", "Не заблокирован", "לא נעול") :
            x.TpmLockedOut == "YES" ? T("Locked out", "Заблокирован", "נעול") : Unknown,
            x.TpmPresent == "NO" || x.TpmLockedOut == "NOT_APPLICABLE"
                ? Status.Neutral :
            x.TpmLockedOut == "NO" ? Status.Good :
            x.TpmLockedOut == "YES" ? Status.Review : Status.Unknown);

        var encryptedWithoutProtection =
            x.OsVolumeProtection == "OFF" &&
            x.OsVolumeConversion == "FULLY_ENCRYPTED";
        tiles[3].Set(T("OS BitLocker protection", "Защита BitLocker системного диска",
                "הגנת BitLocker לכונן מערכת"),
            x.OsVolumeProtection == "ON" ? T("On", "Включена", "פועלת") :
            x.OsVolumeProtection == "OFF" ? T("Off", "Выключена", "כבויה") : Unknown,
            x.OsVolumeProtection == "ON" ? Status.Good :
            encryptedWithoutProtection ? Status.Review :
            x.OsVolumeProtection == "OFF" ? Status.Neutral : Status.Unknown);

        var conversion = x.OsVolumeConversion switch
        {
            "FULLY_ENCRYPTED" => T("Fully encrypted", "Зашифрован", "מוצפן לחלוטין"),
            "FULLY_DECRYPTED" => T("Not encrypted", "Не зашифрован", "לא מוצפן"),
            "ENCRYPTING" => T("Encrypting", "Шифруется", "בהצפנה"),
            "DECRYPTING" => T("Decrypting", "Расшифровывается", "בפענוח"),
            "ENCRYPTION_PAUSED" => T("Encryption paused", "Шифрование приостановлено", "ההצפנה מושהית"),
            "DECRYPTION_PAUSED" => T("Decryption paused", "Расшифровка приостановлена", "הפענוח מושהה"),
            _ => Unknown
        };
        tiles[4].Set(T("OS BitLocker encryption", "Шифрование BitLocker системного диска",
                "הצפנת BitLocker בכונן המערכת"),
            conversion,
            x.OsVolumeConversion == "FULLY_ENCRYPTED" ? Status.Good :
            x.OsVolumeConversion == "UNKNOWN" ? Status.Unknown : Status.Neutral);

        var integrity = x.MemoryIntegrityRuntime switch
        {
            "RUNNING" => x.MemoryIntegrityConfigured switch
            {
                "ON" => T("Running (enabled)", "Работает (включено)", "פועל (מופעל)"),
                "OFF" => T("Running (policy off)", "Работает (политика выключена)", "פועל (מדיניות כבויה)"),
                _ => T("Running", "Работает", "פועל")
            },
            "NOT_RUNNING" => x.MemoryIntegrityConfigured switch
            {
                "ON" => T("Enabled, not running", "Включено, но не работает", "מופעל אך לא פועל"),
                "OFF" => T("Off", "Выключено", "כבוי"),
                "NOT_SET" => T("Not configured", "Не настроено", "לא הוגדר"),
                _ => T("Not running", "Не работает", "לא פועל")
            },
            _ => x.MemoryIntegrityConfigured switch
            {
                "ON" => T("Enabled; runtime unknown", "Включено; работа не проверена", "מופעל; מצב הפעלה לא ידוע"),
                "OFF" => T("Disabled; runtime unknown", "Отключено; работа не проверена", "כבוי; מצב הפעלה לא ידוע"),
                _ => Unknown
            }
        };
        tiles[5].Set(T("Memory Integrity / HVCI", "Целостность памяти / HVCI",
                "שלמות זיכרון / HVCI"),
            integrity,
            x.MemoryIntegrityRuntime == "RUNNING" ? Status.Good :
            x.MemoryIntegrityRuntime == "NOT_RUNNING" &&
                x.MemoryIntegrityConfigured == "ON" ? Status.Review :
            x.MemoryIntegrityRuntime == "NOT_RUNNING" ? Status.Neutral :
            Status.Unknown);

        tiles[6].Set(T("DPAPI policy", "Политика DPAPI", "מדיניות DPAPI"),
            x.DpapiProtectionPolicy switch
            {
                "NOT_SET" => T("Not set", "Не задана", "לא הוגדר"),
                "ZERO" => "0",
                "ONE" => "1",
                "OTHER" => T("Other value", "Другое значение", "ערך אחר"),
                _ => Unknown
            },
            x.DpapiProtectionPolicy == "UNKNOWN" ? Status.Unknown : Status.Neutral);

        tiles[7].Set(T("Writable domain controller", "Доступность RWDC",
                "בקר תחום לכתיבה"),
            x.DomainJoinStatus == "NO" || x.WritableDcStatus == "NOT_APPLICABLE"
                ? T("Not applicable", "Не применяется", "לא חל") :
            x.WritableDcStatus == "AVAILABLE" ? T("Available", "Доступен", "זמין") :
            x.WritableDcStatus == "NOT_FOUND" ? T("Not found", "Не найден", "לא נמצא") : Unknown,
            x.DomainJoinStatus == "NO" || x.WritableDcStatus == "NOT_APPLICABLE"
                ? Status.Neutral :
            x.WritableDcStatus == "AVAILABLE" ? Status.Good :
            x.WritableDcStatus == "NOT_FOUND" ? Status.Review : Status.Unknown);

        tiles[8].Set(T("TPM version", "Версия TPM", "גרסת TPM"),
            x.TpmVersion switch
            {
                "2.0" => "2.0",
                "1.2" => "1.2",
                _ => Unknown
            },
            x.TpmVersion == "2.0" ? Status.Good :
            x.TpmVersion == "1.2" ? Status.Neutral : Status.Unknown);

        tiles[9].Set(T("Security processor", "Процессор безопасности",
                "מעבד אבטחה"),
            x.TpmPresent == "NO" ? T("Not detected", "Не обнаружен", "לא זוהה") :
            x.TpmPresent == "YES" && x.TpmReady == true ?
                T("Available", "Доступен", "זמין") :
            x.TpmPresent == "YES" && x.TpmReady == false ?
                T("Not ready", "Не готов", "לא מוכן") :
            x.TpmPresent == "YES" ? T("Detected; readiness unknown",
                "Обнаружен; готовность неизвестна", "זוהה; מוכנות לא ידועה") : Unknown,
            x.TpmPresent == "YES" && x.TpmReady == true ? Status.Good :
            x.TpmPresent == "YES" && x.TpmReady == false ? Status.Review :
            x.TpmPresent == "NO" ? Status.Neutral : Status.Unknown);

        tiles[10].Set(T("Secure Boot", "Безопасная загрузка (Secure Boot)",
                "אתחול מאובטח"),
            x.SecureBootState switch
            {
                "ON" => T("On", "Включена", "פעיל"),
                "OFF" => T("Off", "Выключена", "כבוי"),
                "UNSUPPORTED" => T("Legacy BIOS", "Legacy BIOS", "BIOS ישן"),
                _ => Unknown
            },
            x.SecureBootState == "ON" ? Status.Good :
            x.SecureBootState is "OFF" or "UNSUPPORTED" ? Status.Neutral :
            Status.Unknown);

        var virtualPlatform = x.VirtualMachinePlatform switch
        {
            "VMWARE" => "VMware",
            "HYPER_V" => "Hyper-V / Azure",
            "KVM_QEMU" => "KVM / QEMU (incl. Proxmox)",
            "VIRTUALBOX" => "VirtualBox",
            "AWS_EC2" => "Amazon EC2",
            "GOOGLE_CLOUD" => "Google Cloud",
            "NUTANIX" => "Nutanix",
            "XEN" => "Xen",
            "PARALLELS" => "Parallels",
            "OTHER_VM" => T("Other VM", "Другая VM", "מכונה וירטואלית אחרת"),
            _ => T("VM guest", "Гостевая VM", "מכונה וירטואלית")
        };
        tiles[11].Set(T("Windows running in a VM", "Windows запущена в VM",
                "Windows פועל במכונה וירטואלית"),
            x.VirtualMachineGuest == "YES" ? virtualPlatform :
            x.VirtualMachineGuest == "NO" ?
                T("No VM indicators (OEM)", "Без признаков VM (OEM)", "ללא סימני VM לפי OEM") :
            Unknown,
            x.VirtualMachineGuest == "YES" ? Status.Neutral :
            x.VirtualMachineGuest == "NO" ? Status.Neutral : Status.Unknown);

        // Third-party full-disk encryption is NOT exposed by BitLocker WMI.
        // Installed software/service evidence is shown separately from
        // volume encryption, which stays unverified without vendor evidence.
        var vendor = ThirdPartyDiskEncryptionProbe.Validate(x.ThirdPartyEncryptionProducts);
        var products = ThirdPartyDiskEncryptionProbe.DisplayNames(vendor);
        var vendorCodes = ThirdPartyDiskEncryptionProbe.IsDetected(vendor)
            ? vendor.Split(',') : [];
        var shortNames = vendorCodes.Length > 0
            ? string.Join(", ", vendorCodes.Take(2).Select(code =>
                ThirdPartyDiskEncryptionProbe.DisplayNames(code)))
            : string.Empty;
        var more = vendorCodes.Length > 2
            ? " +" + (vendorCodes.Length - 2).ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : "";
        tiles[12].Set(T("Third-party encryption software",
                "Сторонние программы шифрования",
                "תוכנות הצפנה חיצוניות"),
            vendorCodes.Length > 0
                ? T("Detected: ", "Обнаружено: ", "זוהו: ") + shortNames + more
                : vendor == "NONE_DETECTED"
                    ? T("No known product detected", "Известные продукты не найдены",
                        "לא זוהתה תוכנה מוכרת")
                    : Unknown,
            vendorCodes.Length > 0 ? Status.Neutral :
            vendor == "NONE_DETECTED" ? Status.Neutral : Status.Unknown);
        tiles[13].Set(T("Third-party OS encryption",
                "Шифрование системного диска сторонними средствами",
                "הצפנת כונן מערכת חיצונית"),
            vendorCodes.Length > 0
                ? T("Vendor verification required", "Нужна проверка производителя",
                    "נדרשת בדיקת יצרן")
                : Unknown,
            Status.Unknown);
    }

    internal void ShowResult(TpmBitLockerSnapshot? snapshot, bool inProgress = false)
    {
        current = snapshot ?? TpmBitLockerSnapshot.Unknown;
        collecting = inProgress;
        UpdateLanguage();
        Visible = true;
        UpdateValues(current);
        UpdateGeometry();
    }

    internal void ClearResult()
    {
        current = null;
        collecting = false;
        Visible = false;
    }

    private void UpdateGeometry()
    {
        if (reshaping) return;
        reshaping = true;
        try
        {
            var available = Math.Max(170, ClientSize.Width - Padding.Horizontal - 20);
            var maxLabel = new Size(Math.Max(140, available - 12), 0);
            title.MaximumSize = maxLabel;
            explanation.MaximumSize = maxLabel;
            caution.MaximumSize = maxLabel;
            var columns = available >= 620 * Math.Max(96, DeviceDpi) / 96 ? 2 : 1;
            if (columns != displayedColumns)
            {
                displayedColumns = columns;
                signalGrid.SuspendLayout();
                signalGrid.Controls.Clear();
                signalGrid.ColumnStyles.Clear();
                signalGrid.RowStyles.Clear();
                signalGrid.ColumnCount = columns;
                signalGrid.RowCount = (tiles.Length + columns - 1) / columns;
                for (int i = 0; i < columns; i++)
                    signalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                for (int i = 0; i < signalGrid.RowCount; i++)
                    signalGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (int i = 0; i < tiles.Length; i++)
                    signalGrid.Controls.Add(tiles[i], i % columns, i / columns);
                signalGrid.ResumeLayout(true);
            }
            var valueWidth = Math.Max(120, available / columns - 28);
            foreach (var tile in tiles)
            {
                var max = new Size(valueWidth, 0);
                tile.Heading.MaximumSize = max;
                tile.Value.MaximumSize = max;
            }
        }
        finally { reshaping = false; }
    }

    internal bool HasReadOnlyDetailsLink => detailsLink.Parent == header &&
        !string.IsNullOrWhiteSpace(detailsLink.Text);

    internal static bool SelfTest()
    {
        using var card = new DeviceSecurityCard();
        var sample = TpmBitLockerSnapshot.Unknown with
        {
            TpmPresent = "YES", TpmVersion = "2.0",
            TpmReady = true, TpmLockedOut = "NO",
            SecureBootState = "ON", FirmwareType = "UEFI",
            VirtualMachineGuest = "YES", VirtualMachinePlatform = "KVM_QEMU",
            OsVolumeProtection = "ON", OsVolumeConversion = "FULLY_ENCRYPTED",
            ThirdPartyEncryptionProducts = "SOPHOS,VERACRYPT",
            MemoryIntegrityRuntime = "RUNNING", MemoryIntegrityConfigured = "ON",
            DpapiProtectionPolicy = "NOT_SET", DomainJoinStatus = "YES",
            WritableDcStatus = "AVAILABLE"
        };
        card.ShowResult(TpmBitLockerSnapshot.Unknown, inProgress: true);
        if (!card.collecting || card.Unknown !=
            T("Checking…", "Проверяется…", "בבדיקה…"))
            return false;
        card.ShowResult(sample);
        if (!card.HasReadOnlyDetailsLink || card.tiles.Length != 14 ||
            card.signalGrid.Controls.Count != 14 ||
            !card.tiles[0].AccessibleDescription.Contains("positive", StringComparison.OrdinalIgnoreCase) ||
            !card.tiles[3].AccessibleDescription.Contains("positive", StringComparison.OrdinalIgnoreCase) ||
            !card.tiles[8].Value.Text.Contains("2.0", StringComparison.Ordinal) ||
            !card.tiles[10].AccessibleDescription.Contains("positive", StringComparison.OrdinalIgnoreCase) ||
            !card.tiles[11].Value.Text.Contains("QEMU", StringComparison.Ordinal) ||
            !card.tiles[12].Value.Text.Contains("VeraCrypt", StringComparison.Ordinal) ||
            !card.tiles[13].Value.Text.Contains(
                T("Vendor verification", "Нужна проверка", "נדרשת בדיקת"),
                StringComparison.Ordinal))
            return false;
        card.ShowResult(sample with
        {
            TpmLockedOut = "YES", OsVolumeProtection = "OFF"
        });
        if (!card.tiles[2].AccessibleDescription.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            !card.tiles[3].AccessibleDescription.Contains("review", StringComparison.OrdinalIgnoreCase))
            return false;
        card.ShowResult(TpmBitLockerSnapshot.Unknown);
        return card.tiles.All(t =>
            t.AccessibleDescription.Contains("No verified status", StringComparison.Ordinal)) &&
            card.Controls.Count == 4;
    }
}
