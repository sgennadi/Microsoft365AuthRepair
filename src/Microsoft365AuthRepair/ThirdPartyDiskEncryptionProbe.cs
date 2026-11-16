using Microsoft.Win32;

namespace Microsoft365AuthRepair;

// Read-only software/driver inventory, not a disk-encryption or recovery-key
// query. A detected product NEVER proves that the operating-system volume is
// encrypted, mounted or protected. Public diagnostics do not include these
// locally displayed classifications.
internal static class ThirdPartyDiskEncryptionProbe
{
    private sealed record Product(string Code, string Label, string[] Prefixes);

    // Only hard-coded product classes cross the local isolated-worker IPC.
    // Do not transmit raw uninstall entries, service names or installation
    // paths: those could contain identifying or arbitrary untrusted text.
    private static readonly Product[] Products =
    [
        new("BESTCRYPT", "BestCrypt",
            ["BestCrypt", "Jetico BestCrypt"]),
        new("BITDEFENDER", "Bitdefender Full Disk Encryption",
            ["Bitdefender Full Disk Encryption"]),
        new("CHECKPOINT", "Check Point Full Disk Encryption",
            ["Check Point Full Disk Encryption"]),
        new("DELL", "Dell Encryption",
            ["Dell Encryption", "Dell Data Protection | Encryption",
                "Dell Data Security Encryption"]),
        new("DISKCRYPTOR", "DiskCryptor",
            ["DiskCryptor"]),
        new("ESET", "ESET Full Disk Encryption",
            ["ESET Full Disk Encryption", "ESET Endpoint Encryption"]),
        new("KASPERSKY", "Kaspersky Disk Encryption",
            ["Kaspersky Full Disk Encryption", "Kaspersky Disk Encryption"]),
        new("MCAFEE", "McAfee / Trellix Drive Encryption",
            ["McAfee Drive Encryption", "Trellix Drive Encryption",
                "McAfee Endpoint Encryption"]),
        new("SOPHOS", "Sophos SafeGuard / Device Encryption",
            ["Sophos SafeGuard", "SafeGuard Enterprise", "Sophos Device Encryption"]),
        new("SYMANTEC", "Symantec Endpoint Encryption",
            ["Symantec Endpoint Encryption", "Symantec Encryption Desktop",
                "Broadcom Endpoint Encryption"]),
        new("TRENDMICRO", "Trend Micro Endpoint Encryption",
            ["Trend Micro Endpoint Encryption"]),
        new("TRUECRYPT", "TrueCrypt (legacy)",
            ["TrueCrypt"]),
        new("VERACRYPT", "VeraCrypt",
            ["VeraCrypt"]),
        new("WINMAGIC", "WinMagic SecureDoc",
            ["WinMagic SecureDoc", "SecureDoc Full Disk Encryption"])
    ];

    private static readonly HashSet<string> AllowedCodes =
        Products.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);

    // A starts-with match is applied only to known software names, not generic
    // "encryption" text. "Security Agent" and antivirus-only packages do not
    // constitute evidence of a full-disk-encryption product.
    internal static string? ClassifyInstalledName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 512)
            return null;
        var cleaned = displayName.Trim();
        foreach (var product in Products)
            if (product.Prefixes.Any(prefix =>
                cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                return product.Code;
        return null;
    }

    private static string? DriverProduct(string? serviceName) =>
        serviceName?.ToUpperInvariant() switch
        {
            // These exact driver/service names can be present for mounted
            // containers too, so they only indicate a product is present.
            "VERACRYPT" => "VERACRYPT",
            "TRUECRYPT" => "TRUECRYPT",
            "DCRYPT" => "DISKCRYPTOR",
            _ => null
        };

    private static bool ReadUninstall(RegistryHive hive, RegistryView view,
        HashSet<string> detected)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var uninstall = root.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: false);
        if (uninstall is null) return true; // No entries in this registry view
        var names = uninstall.GetSubKeyNames();
        foreach (var name in names.Take(4096))
        {
            // An uninstall key name is not itself evidence of a product.
            using var app = uninstall.OpenSubKey(name, writable: false);
            var product = ClassifyInstalledName(app?.GetValue("DisplayName") as string);
            if (product is not null) detected.Add(product);
        }
        return true;
    }

    private static bool ReadDriverRegistrations(HashSet<string> detected)
    {
        using var root = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services", writable: false);
        if (root is null) return false;
        foreach (var serviceName in root.GetSubKeyNames())
        {
            var code = DriverProduct(serviceName);
            if (code is not null) detected.Add(code);
        }
        return true;
    }

    // Child runs with the existing 10-second deadline, never opens a device
    // handle, invokes vendor commands or reads encrypted sectors. The result
    // is software-presence evidence only.
    internal static string Read()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        bool atLeastOneSource = false;
        foreach (var source in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default)
        })
        {
            try
            {
                atLeastOneSource |= ReadUninstall(source.Item1, source.Item2, codes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or System.Security.SecurityException or ArgumentException)
            { /* Ignore inaccessible views; no guesses or raw errors. */ }
        }

        try { atLeastOneSource |= ReadDriverRegistrations(codes); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException or ArgumentException)
        { /* No raw registry/service names in Activity. */ }

        if (codes.Count > 0)
            return string.Join(",", codes.OrderBy(x => x, StringComparer.Ordinal));
        return atLeastOneSource ? "NONE_DETECTED" : "UNKNOWN";
    }

    // Validate and canonicalize strictly before accepting results from the
    // out-of-process worker. Prevent arbitrary registry values entering the UI.
    internal static string Validate(string? raw)
    {
        if (raw is "UNKNOWN" or "NONE_DETECTED") return raw;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 196) return "UNKNOWN";
        var codes = raw.Split(',');
        if (codes.Length is < 1 or > 14 ||
            codes.Any(x => !AllowedCodes.Contains(x)) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Length ||
            !codes.SequenceEqual(codes.OrderBy(x => x, StringComparer.Ordinal)))
            return "UNKNOWN";
        return raw;
    }

    internal static bool IsDetected(string? codes) =>
        Validate(codes) is not ("UNKNOWN" or "NONE_DETECTED");

    internal static string DisplayNames(string? raw)
    {
        var codes = Validate(raw);
        if (!IsDetected(codes)) return "";
        return string.Join(", ", codes.Split(',').Select(code =>
            Products.Single(p => p.Code == code).Label));
    }

    internal static bool SelfTest() =>
        ClassifyInstalledName("VeraCrypt 1.26") == "VERACRYPT" &&
        ClassifyInstalledName("DiskCryptor") == "DISKCRYPTOR" &&
        ClassifyInstalledName("Sophos SafeGuard Enterprise") == "SOPHOS" &&
        ClassifyInstalledName("Symantec Endpoint Encryption Client") == "SYMANTEC" &&
        ClassifyInstalledName("Trellix Drive Encryption") == "MCAFEE" &&
        ClassifyInstalledName("McAfee Endpoint Encryption Agent") == "MCAFEE" &&
        ClassifyInstalledName("WinMagic SecureDoc") == "WINMAGIC" &&
        ClassifyInstalledName("Unrelated Security Suite") is null &&
        ClassifyInstalledName("Windows Defender Antivirus") is null &&
        ClassifyInstalledName("Microsoft Office Encryption") is null &&
        DriverProduct("veracrypt") == "VERACRYPT" &&
        DriverProduct("dcrypt") == "DISKCRYPTOR" &&
        DriverProduct("cloudfs") is null &&
        Validate("SOPHOS,VERACRYPT") == "SOPHOS,VERACRYPT" &&
        Validate("VERACRYPT,SOPHOS") == "UNKNOWN" &&
        Validate("VERACRYPT,VERACRYPT") == "UNKNOWN" &&
        Validate("SOPHOS;" + "secret") == "UNKNOWN" &&
        Validate("NONE_DETECTED") == "NONE_DETECTED" &&
        IsDetected("VERACRYPT") &&
        !IsDetected("NONE_DETECTED") &&
        DisplayNames("BESTCRYPT,VERACRYPT").Contains("BestCrypt", StringComparison.Ordinal) &&
        DisplayNames("BESTCRYPT,VERACRYPT").Contains("VeraCrypt", StringComparison.Ordinal);
}
