using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

// All TPM/BitLocker operations are read-only; no recovery keys, protector
// identifiers, owner authorization, DeviceID, hostname, logs, serial numbers,
// or raw WMI output are gathered, stored or transmitted.
internal sealed record TpmBitLockerSnapshot(
    string TpmPresent, string TpmVersion, bool? TpmReady,
    bool? TpmEnabled, bool? TpmActivated, bool? TpmOwned,
    string OsVolumeProtection, string OsVolumeConversion,
    string MemoryIntegrityRuntime, string MemoryIntegrityConfigured,
    string DpapiProtectionPolicy)
{
    internal static TpmBitLockerSnapshot Unknown => new(
        "UNKNOWN", "UNKNOWN", null, null, null, null,
        "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN");

    internal bool NeedsTpmReview =>
        TpmPresent == "YES" &&
        (TpmReady == false || TpmEnabled == false ||
         TpmActivated == false || TpmOwned == false);

    internal bool NeedsBitLockerReview =>
        OsVolumeConversion == "FULLY_ENCRYPTED" && OsVolumeProtection == "OFF";

    internal string CompactSummary() =>
        "TPM " + TpmPresent +
        (TpmVersion == "UNKNOWN" ? "" : " (" + TpmVersion + ")") +
        " · System drive BitLocker " + OsVolumeProtection +
        " · DPAPI policy " + DpapiProtectionPolicy;

    internal static string DescribeError(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "0X80090016" or "80090016" or "-2146893802" => "0x80090016 / NTE_BAD_KEYSET: cryptographic keyset is missing or invalid. " +
            "Review the affected user's WAM/DPAPI and device registration. This is NOT proof of broken TPM hardware.",
        "0X80090030" or "80090030" => "0x80090030 / NTE_DEVICE_NOT_READY: the cryptographic device is not ready. " +
            "Review TPM provisioning, OS/firmware updates and Microsoft device registration. Do not clear TPM as the first step.",
        "0XC0090016" or "C0090016" => "0xC0090016: Office/WAM reported a cryptographic sign-in error. " +
            "Review WAM identity and Entra device registration; this code is not assumed to be 0x80090016.",
        "0X80090031" or "80090031" or "-2146893775" => "0x80090031 / NTE_AUTHENTICATION_IGNORED: may indicate a temporary TPM lockout. " +
            "Allow cooldown and review TPM state. Do not clear TPM automatically.",
        "0X80090034" => "0x80090034: a cryptographic provider error was reported. " +
            "Inspect Windows DPAPI/WAM events and account context; hardware failure is not established.",
        "0X80090035" => "0x80090035: Windows Hello policy may require a TPM that is not present. " +
            "Ask IT to inspect Hello for Business and device policy; avoid changing TPM or policy blindly.",
        _ => "No recognized code selected. TPM/BitLocker state does not by itself diagnose Office sign-in."
    };

    // Public diagnostic allowlist: only coarse hardware-protection states.
    // All values are locally constrained to the fixed options below.
    internal string ToSafeSummary() =>
        "TpmPresent: " + Safe(TpmPresent, ["YES", "NO", "UNKNOWN"]) +
        "; TpmVersion: " + Safe(TpmVersion, ["1.2", "2.0", "UNKNOWN"]) +
        "; TpmReady: " + Bool(TpmReady) +
        "; BitLockerProtection: " + Safe(OsVolumeProtection, ["ON", "OFF", "UNKNOWN"]) +
        "; BitLockerConversion: " + Safe(OsVolumeConversion, [
            "FULLY_DECRYPTED", "FULLY_ENCRYPTED", "ENCRYPTING", "DECRYPTING",
            "ENCRYPTION_PAUSED", "DECRYPTION_PAUSED", "UNKNOWN"]) +
        "; MemoryIntegrityRuntime: " + Safe(MemoryIntegrityRuntime, [
            "RUNNING", "NOT_RUNNING", "UNKNOWN"]) +
        "; DpapiProtectionPolicy: " + Safe(DpapiProtectionPolicy, [
            "NOT_SET", "ZERO", "ONE", "OTHER", "UNKNOWN"]);

    private static string Bool(bool? value) =>
        value is null ? "UNKNOWN" : value.Value ? "YES" : "NO";

    private static string Safe(string value, string[] allowed) =>
        allowed.Contains(value, StringComparer.Ordinal) ? value : "UNKNOWN";

    internal static bool SelfTest()
    {
        var unknown = Unknown;
        var healthy = unknown with
        {
            TpmPresent = "YES", TpmVersion = "2.0", TpmReady = true, TpmEnabled = true,
            TpmActivated = true, TpmOwned = true,
            OsVolumeProtection = "ON", OsVolumeConversion = "FULLY_ENCRYPTED"
        };
        return !unknown.NeedsTpmReview && !unknown.NeedsBitLockerReview &&
            !healthy.NeedsTpmReview && !healthy.NeedsBitLockerReview &&
            (healthy with { OsVolumeProtection = "OFF" }).NeedsBitLockerReview &&
            (healthy with { TpmEnabled = false }).NeedsTpmReview &&
            (healthy with { TpmReady = false }).NeedsTpmReview &&
            DescribeError("0x80090016").Contains("NOT proof", StringComparison.Ordinal) &&
            DescribeError("-2146893802") == DescribeError("0x80090016") &&
            DescribeError("0x80090030").Contains("NTE_DEVICE_NOT_READY", StringComparison.Ordinal) &&
            !DescribeError("0xC0090016").Contains("0x80090016", StringComparison.Ordinal) &&
            !DescribeError("random text").Contains("failed", StringComparison.OrdinalIgnoreCase) &&
            !healthy.CompactSummary().Contains("Recovery", StringComparison.OrdinalIgnoreCase) &&
            !healthy.ToSafeSummary().Contains("Recovery", StringComparison.OrdinalIgnoreCase) &&
            (healthy with { TpmVersion = "SECRET_DEVICE_NAME" }).ToSafeSummary()
                .Contains("TpmVersion: UNKNOWN", StringComparison.Ordinal);
    }
}

internal static class TpmBitLockerProbe
{
    private static bool WmiAccess(Exception ex) =>
        ex is ManagementException or UnauthorizedAccessException
            or System.Security.SecurityException or COMException
            or InvalidOperationException or PlatformNotSupportedException;

    private static bool? ReadBool(ManagementBaseObject item, string name) =>
        item[name] is bool b ? b : null;

    private static bool? LiveTpmFlag(ManagementObject item, string method,
        string resultName, bool? initial)
    {
        try
        {
            // IsEnabled / IsActivated / IsOwned: documented read-only WMI
            // queries. NEVER call TPM provisioning, Clear or SetPhysicalPresenceRequest.
            using var result = item.InvokeMethod(method, null, null);
            if (result is not null &&
                Convert.ToUInt32(result["ReturnValue"]) == 0u)
                return ReadBool(result, resultName) ?? initial;
        }
        catch (Exception ex) when (WmiAccess(ex) || ex is ArgumentException) { }
        return initial;
    }

    private static (string Present, string Version, bool? Ready, bool? Enabled,
        bool? Activated, bool? Owned) ReadTpm()
    {
        try
        {
            using var query = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftTpm",
                "SELECT IsEnabled_InitialValue, IsActivated_InitialValue, IsOwned_InitialValue, SpecVersion FROM Win32_Tpm");
            using var items = query.Get();
            foreach (ManagementObject item in items)
            {
                using (item)
                {
                    var value = item["SpecVersion"] as string ?? "";
                    var version = value.StartsWith("2.0", StringComparison.Ordinal)
                        ? "2.0" : value.StartsWith("1.2", StringComparison.Ordinal)
                            ? "1.2" : "UNKNOWN";
                    return ("YES", version,
                        LiveTpmFlag(item, "IsReady", "IsReady", null),
                        LiveTpmFlag(item, "IsEnabled", "IsEnabled",
                            ReadBool(item, "IsEnabled_InitialValue")),
                        LiveTpmFlag(item, "IsActivated", "IsActivated",
                            ReadBool(item, "IsActivated_InitialValue")),
                        LiveTpmFlag(item, "IsOwned", "IsOwned",
                            ReadBool(item, "IsOwned_InitialValue")));
                }
            }
            // A successful query with no instance normally means no TPM.
            return ("NO", "UNKNOWN", null, null, null, null);
        }
        catch (Exception ex) when (WmiAccess(ex))
        { return ("UNKNOWN", "UNKNOWN", null, null, null, null); }
    }

    private static string ProtectionState(object? value)
    {
        if (value is null) return "UNKNOWN";
        return Convert.ToUInt32(value) switch
        {
            0 => "OFF", 1 => "ON", _ => "UNKNOWN"
        };
    }

    private static string ConversionState(object? value)
    {
        if (value is null) return "UNKNOWN";
        return Convert.ToUInt32(value) switch
        {
            0 => "FULLY_DECRYPTED",
            1 => "FULLY_ENCRYPTED",
            2 => "ENCRYPTING",
            3 => "DECRYPTING",
            4 => "ENCRYPTION_PAUSED",
            5 => "DECRYPTION_PAUSED",
            _ => "UNKNOWN"
        };
    }

    private static (string Protection, string Conversion) ReadSystemVolume()
    {
        string drive = (Environment.GetEnvironmentVariable("SystemDrive") ??
            Path.GetPathRoot(Environment.SystemDirectory) ?? "").TrimEnd('\\', '/');
        if (drive.Length != 2 || !char.IsLetter(drive[0]) || drive[1] != ':')
            return ("UNKNOWN", "UNKNOWN");
        try
        {
            // Local, fixed WMI namespace. Only the OS drive; never enumerate
            // key protectors or call recovery password/escrow APIs.
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume");
            using var entries = searcher.Get();
            foreach (ManagementObject item in entries)
            {
                using (item)
                {
                    if (!string.Equals(item["DriveLetter"] as string, drive,
                        StringComparison.OrdinalIgnoreCase)) continue;
                    var protection = ProtectionState(item["ProtectionStatus"]);
                    string conversion = "UNKNOWN";
                    try
                    {
                        using var input = item.GetMethodParameters("GetConversionStatus");
                        if (input is not null)
                            input["PrecisionFactor"] = 0u;
                        using var output = item.InvokeMethod(
                            "GetConversionStatus", input, null);
                        if (output is not null &&
                            Convert.ToUInt32(output["ReturnValue"]) == 0u)
                            conversion = ConversionState(output["ConversionStatus"]);
                    }
                    catch (Exception ex) when (WmiAccess(ex) ||
                        ex is ArgumentException or FormatException) { }
                    return (protection, conversion);
                }
            }
            return ("UNKNOWN", "UNKNOWN");
        }
        catch (Exception ex) when (WmiAccess(ex))
        { return ("UNKNOWN", "UNKNOWN"); }
    }

    private static string ReadDpapiPolicy()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography\Protect\Providers\df9d8cd0-1501-11d1-8c7a-00c04fc297eb",
                writable: false);
            var value = key?.GetValue("ProtectionPolicy");
            return value switch
            {
                null => "NOT_SET",
                int i when i == 0 => "ZERO",
                int i when i == 1 => "ONE",
                int => "OTHER",
                _ => "UNKNOWN"
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or System.Security.SecurityException or IOException or ArgumentException)
        { return "UNKNOWN"; }
    }

    private static string ReadHVCIConfigured()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                writable: false);
            return key?.GetValue("Enabled") switch
            {
                int 1 => "ON", int 0 => "OFF",
                null => "NOT_SET", _ => "UNKNOWN"
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or System.Security.SecurityException or IOException or ArgumentException)
        { return "UNKNOWN"; }
    }

    private static string ReadHVCIRunning()
    {
        try
        {
            using var search = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\DeviceGuard",
                "SELECT SecurityServicesRunning FROM Win32_DeviceGuard");
            using var instances = search.Get();
            foreach (ManagementObject item in instances)
            {
                using (item)
                {
                    if (item["SecurityServicesRunning"] is not Array services)
                        return "UNKNOWN";
                    return services.Cast<object>().Any(x => Convert.ToUInt32(x) == 2u)
                        ? "RUNNING" : "NOT_RUNNING";
                }
            }
            return "UNKNOWN";
        }
        catch (Exception ex) when (WmiAccess(ex) || ex is InvalidCastException)
        { return "UNKNOWN"; }
    }

    internal static TpmBitLockerSnapshot Capture()
    {
        // Individual probes fail closed to UNKNOWN. Lack of WMI permissions is
        // not interpreted as TPM absent or BitLocker off.
        var tpm = ReadTpm();
        var drive = ReadSystemVolume();
        return new(tpm.Present, tpm.Version, tpm.Ready, tpm.Enabled, tpm.Activated, tpm.Owned,
            drive.Protection, drive.Conversion,
            ReadHVCIRunning(), ReadHVCIConfigured(), ReadDpapiPolicy());
    }
}
