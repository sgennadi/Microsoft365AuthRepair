using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Native, read-only vNext/Shared Computer Activation indicators.
// We do not execute ospp.vbs, vnextdiag.ps1, license reset, or external tools.
// Microsoft considers vnextdiag.ps1 -action list authoritative for detailed
// subscription license state: these non-secret local hints are NOT equivalent.
internal sealed record OfficeLicenseSnapshot(
    bool Installed, string ProductFamily, string OfficeVersion,
    bool? SharedComputerActivation, bool? DeviceBasedLicensing,
    bool UserLicenseScopeChecked, bool? VNextSelected, int? VNextLicenseFiles,
    int? SharedLicenseFiles, bool MicrosoftVNextDiagAvailable)
{
    internal bool HasLocalLicensingHints =>
        VNextSelected == true || VNextLicenseFiles > 0 || SharedLicenseFiles > 0;
}

internal static class OfficeLicenseDiagnostics
{
    private const string Inventory = @"SOFTWARE\Microsoft\Office\ClickToRun\Inventory\Office\16.0";
    private const string Configuration = @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration";
    private const string Next = @"Software\Microsoft\Office\16.0\Common\Licensing\LicensingNext";

    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);

    private static string? ReadMachine(string path, string name)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(path, writable: false);
                var value = key?.GetValue(name);
                if (value is not null) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException
                or UnauthorizedAccessException or IOException or ArgumentException) { /* no data */ }
        }
        return null;
    }

    private static bool? Flag(string? value) => value switch
    {
        "1" => true,
        "0" => false,
        _ => null
    };

    private static bool? VNextMarker()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Next, writable: false);
            if (key is null) return false;
            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name);
                if ((value is int i && i == 2) ||
                    (value is string text && text == "2"))
                    return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
            or UnauthorizedAccessException or IOException or ArgumentException) { return null; }
    }

    private static int? CountTokens(string root, bool subfolders)
    {
        // Only count filenames: do not read file contents, names, user IDs or
        // license blobs. Never recurse into reparse points/roaming links.
        try
        {
            if (!Directory.Exists(root)) return 0;
            var folders = new List<string> { root };
            if (subfolders)
                foreach (var folder in Directory.EnumerateDirectories(root).Take(64))
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0)
                        folders.Add(folder);
                }
            int count = 0;
            foreach (var folder in folders)
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    return null;
                count += Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                    .Take(129).Count();
                if (count > 128) return 128; // cap for UI, never enumerate entire profile
            }
            return count;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or IOException or System.Security.SecurityException) { return null; }
    }

    private static bool MicrosoftToolPresent()
    {
        foreach (var pf in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var suffix in new[]
            {
                @"Microsoft Office\Office16\vnextdiag.ps1",
                @"Microsoft Office\root\Office16\vnextdiag.ps1"
            })
            {
                if (File.Exists(Path.Combine(pf, suffix))) return true;
            }
        }
        return false;
    }

    private static string VersionOrUnknown(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Regex.IsMatch(value, @"^\d+(\.\d+){1,3}$", RegexOptions.CultureInvariant)
            ? value : "Unknown";

    private static string Family(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return "Unidentified Click-to-Run edition";
        if (ids.Contains("O365", StringComparison.OrdinalIgnoreCase) ||
            ids.Contains("M365", StringComparison.OrdinalIgnoreCase))
            return "Microsoft 365 Apps subscription";
        if (ids.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
            ids.Contains("LTSC", StringComparison.OrdinalIgnoreCase))
            return "Office volume / LTSC";
        if (ids.Contains("Retail", StringComparison.OrdinalIgnoreCase))
            return "Office / Project / Visio Retail";
        return "Unidentified Click-to-Run edition";
    }

    internal static OfficeLicenseSnapshot Capture(bool ownDesktopSession)
    {
        // Microsoft's installed-product inventory, not VersionToReport
        // (which Microsoft documents as an internal telemetry field).
        var version = ReadMachine(Inventory, "OfficePackageVersion");
        var ids = ReadMachine(Inventory, "OfficeProductReleaseIds");
        var sca = Flag(ReadMachine(Configuration, "SharedComputerLicensing"));
        var device = Flag(ReadMachine(Configuration, "O365ProPlusRetail.DeviceBasedLicensing"));
        var installed = !string.IsNullOrWhiteSpace(version) || !string.IsNullOrWhiteSpace(ids);
        bool? vnext = null;
        int? vnextFiles = null, sharedFiles = null;

        // Admin launched via UAC may not be the affected user's account.
        // Never display that administrator's HKCU / LocalAppData as the user's.
        if (ownDesktopSession)
        {
            vnext = VNextMarker();
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local))
            {
                vnextFiles = CountTokens(Path.Combine(local, "Microsoft", "Office", "Licenses"), true);
                sharedFiles = CountTokens(Path.Combine(local, "Microsoft", "Office", "16.0", "Licensing"), false);
            }
        }

        return new OfficeLicenseSnapshot(installed, Family(ids), VersionOrUnknown(version),
            sca, device, ownDesktopSession, vnext, vnextFiles, sharedFiles, MicrosoftToolPresent());
    }

    private static string YesNo(bool? value) => value switch
    {
        true => T("Enabled", "Включено", "מופעל"),
        false => T("Disabled", "Выключено", "מושבת"),
        _ => T("Unknown", "Неизвестно", "לא ידוע")
    };

    internal static string Summary(OfficeLicenseSnapshot info)
    {
        if (!info.Installed)
            return T("Office licensing: Click-to-Run installation not verified",
                "Лицензирование Office: установка Click-to-Run не подтверждена",
                "רישוי Office: התקנת Click-to-Run לא אומתה");
        if (!info.UserLicenseScopeChecked)
            return T("Office licensing: system setup checked; user license skipped (different administrator)",
                "Лицензирование Office: конфигурация ПК проверена; лицензия пользователя не проверялась (другой администратор)",
                "רישוי Office: נבדקה תצורת מחשב; רישיון משתמש לא נבדק כי מדובר במנהל אחר");
        return info.HasLocalLicensingHints
            ? T("Office licensing: local vNext/SCA indicators found — activation NOT verified",
                "Лицензия Office: обнаружены признаки vNext/SCA — активация НЕ подтверждена",
                "רישוי Office: נמצאו סימני vNext/SCA אך ההפעלה לא אומתה")
            : T("Office licensing: no local vNext/SCA indicators found — investigate, not proof of no license",
                "Лицензия Office: локальные признаки vNext/SCA не найдены — нужна проверка, это не доказательство отсутствия лицензии",
                "רישוי Office: לא נמצאו סימני vNext/SCA; נדרשת בדיקה נוספת");
    }

    internal static string Details(OfficeLicenseSnapshot info)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine(T("OFFICE LICENSING — READ-ONLY", "ЛИЦЕНЗИРОВАНИЕ OFFICE — ТОЛЬКО ЧТЕНИЕ", "רישוי Office — קריאה בלבד"));
        text.AppendLine(T("Installed product class: ", "Класс продукта: ", "סוג מוצר: ") + info.ProductFamily);
        text.AppendLine(T("Office build (inventory): ", "Сборка Office (инвентаризация): ", "גרסת Office: ") + info.OfficeVersion);
        text.AppendLine("SharedComputerLicensing: " + YesNo(info.SharedComputerActivation));
        text.AppendLine("DeviceBasedLicensing: " + YesNo(info.DeviceBasedLicensing));
        if (info.UserLicenseScopeChecked)
        {
            text.AppendLine("LicensingNext vNext selected: " + YesNo(info.VNextSelected));
            text.AppendLine("vNext local token file count: " + (info.VNextLicenseFiles?.ToString() ?? "Unknown"));
            text.AppendLine("Shared activation local token file count: " + (info.SharedLicenseFiles?.ToString() ?? "Unknown"));
        }
        else
            text.AppendLine(T("Current-user licensing: SKIPPED (different desktop user / administrator)",
                "Лицензия пользователя: ПРОПУЩЕНА (другой пользователь/администратор)",
                "רישוי משתמש: דולג בגלל חשבון מנהל שונה"));
        text.AppendLine("Microsoft vnextdiag.ps1 available: " + YesNo(info.MicrosoftVNextDiagAvailable));
        text.AppendLine(T(
            "These are local indicators only; Office activation, renewal expiry, account license assignment and entitlement are NOT verified. No VBS/PS1 was executed and no license contents were read.",
            "Это только локальные признаки. Активация Office, срок действия, назначение лицензии и права пользователя НЕ проверены. VBS/PS1 не запускались, содержимое лицензий не читалось.",
            "אלו סימנים מקומיים בלבד. סטטוס הפעלה ורישיון משתמש לא אומתו. לא הורצו סקריפטים."));
        return text.ToString();
    }

    internal static bool SelfTest()
    {
        var good = new OfficeLicenseSnapshot(true, "Microsoft 365 Apps subscription", "16.0.19725.20170",
            true, false, true, true, 3, 2, true);
        var otherAdmin = good with { UserLicenseScopeChecked = false, VNextSelected = null,
            VNextLicenseFiles = null, SharedLicenseFiles = null };
        var missing = good with { VNextSelected = false, VNextLicenseFiles = 0, SharedLicenseFiles = 0 };
        return good.HasLocalLicensingHints && !otherAdmin.HasLocalLicensingHints &&
            !missing.HasLocalLicensingHints &&
            Summary(good).Length > 25 && Summary(good).Contains("NOT verified", StringComparison.OrdinalIgnoreCase) &&
            Summary(missing).Length > 20 &&
            Details(otherAdmin).Contains("LicensingNext vNext selected:", StringComparison.Ordinal) == false &&
            VersionOrUnknown("16.0.19725.20170") == "16.0.19725.20170" &&
            VersionOrUnknown("name@example.org") == "Unknown" &&
            Family("O365ProPlusRetail") == "Microsoft 365 Apps subscription" &&
            Flag("1") == true && Flag("0") == false && Flag("other") is null;
    }
}
