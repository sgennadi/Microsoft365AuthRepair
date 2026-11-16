using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
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
    string DpapiProtectionPolicy,
    string DomainJoinStatus = "UNKNOWN", string WritableDcStatus = "UNKNOWN")
{
    // Local-only attestation evidence; never collect EK public keys, EK
    // certificates, raw registry values, AIK identity or certificate subjects.
    internal string TpmManufacturer { get; init; } = "UNKNOWN";
    internal string TpmFirmwareVersion { get; init; } = "UNKNOWN";
    internal string WindowsRelease { get; init; } = "UNKNOWN";
    internal string AikEnrollmentError { get; init; } = "UNKNOWN";
    internal string EkCertStore { get; init; } = "UNKNOWN";

    internal bool NeedsAttestationReview =>
        AikEnrollmentError is "NOT_FOUND" or "TIMEOUT" or
            "INVALID_PUBLIC_KEY" or "SERVER_ERROR" or "OTHER_ERROR";

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
        " · DPAPI policy " + DpapiProtectionPolicy +
        (DomainJoinStatus == "YES" ? " · RWDC " + WritableDcStatus : "");

    internal static string DescribeError(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "0X80070490" or "80070490" => "0x80070490 / ERROR_NOT_FOUND: an AIK attestation attempt may fail locally. " +
            "This is NOT the timeout code and does NOT prove an EK certificate/public-key mismatch. " +
            "Inspect recorded AIK state, Windows release, TPM firmware and OEM updates; do not clear TPM.",
        "0X800705B4" or "800705B4" => "0x800705B4 / ERROR_TIMEOUT: an Autopilot or attestation operation timed out. " +
            "A timeout may follow an earlier attestation failure; it does not by itself diagnose TPM hardware.",
        "0X80090016" or "80090016" or "-2146893802" => "0x80090016 / NTE_BAD_KEYSET: cryptographic keyset is missing or invalid. " +
            "Review the affected user's WAM/DPAPI and device registration. This is NOT proof of broken TPM hardware.",
        "0X80090030" or "80090030" => "0x80090030 / NTE_DEVICE_NOT_READY: the cryptographic device is not ready. " +
            "Review TPM provisioning, OS/firmware updates and Microsoft device registration. Do not clear TPM as the first step.",
        "0XC0090016" or "C0090016" => "0xC0090016: Office/WAM reported a cryptographic sign-in error. " +
            "Review WAM identity and Entra device registration; this code is not assumed to be 0x80090016.",
        "0X80090031" or "80090031" or "-2146893775" => "0x80090031 / NTE_AUTHENTICATION_IGNORED: may indicate a temporary TPM lockout. " +
            "Allow cooldown and review TPM state. Do not clear TPM automatically.",
        "0X80090034" or "80090034" => "0x80090034 / NTE_ENCRYPTION_FAILURE: encryption failed. " +
            "For domain users, verify writable domain controller (RWDC) connectivity, VPN and DPAPI policy; " +
            "also inspect WAM and Entra join state. This error does not prove TPM hardware failure.",
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
            "NOT_SET", "ZERO", "ONE", "OTHER", "UNKNOWN"]) +
        "; DpapiDomainJoined: " + Safe(DomainJoinStatus, ["YES", "NO", "UNKNOWN"]) +
        "; DpapiWritableDc: " + Safe(WritableDcStatus, [
            "AVAILABLE", "NOT_FOUND", "UNKNOWN", "NOT_APPLICABLE"]) +
        "; TpmAikEnrollment: " + Safe(AikEnrollmentError, [
            "NOT_FOUND", "TIMEOUT", "INVALID_PUBLIC_KEY", "SERVER_ERROR",
            "OTHER_ERROR", "NONE_RECORDED", "UNKNOWN"]) +
        "; TpmEkCertStore: " + Safe(EkCertStore, [
            "IMPORT_INCOMPLETE", "UNKNOWN"]) +
        "; TpmManufacturerClass: " + Safe(TpmManufacturer, [
            "STM", "MSFT", "OTHER", "UNKNOWN"]) +
        "; WindowsTpmRelease: " + Safe(WindowsRelease, [
            "WIN11_24H2", "WIN11_25H2", "WIN11_NEWER", "OTHER", "UNKNOWN"]);

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
            DescribeError("0x80070490").Contains("NOT the timeout", StringComparison.Ordinal) &&
            DescribeError("0x800705b4").Contains("ERROR_TIMEOUT", StringComparison.Ordinal) &&
            DescribeError("80090034").Contains("NTE_ENCRYPTION_FAILURE", StringComparison.Ordinal) &&
            !DescribeError("0xC0090016").Contains("0x80090016", StringComparison.Ordinal) &&
            !DescribeError("random text").Contains("failed", StringComparison.OrdinalIgnoreCase) &&
            !healthy.CompactSummary().Contains("Recovery", StringComparison.OrdinalIgnoreCase) &&
            !healthy.ToSafeSummary().Contains("Recovery", StringComparison.OrdinalIgnoreCase) &&
            (healthy with { TpmVersion = "SECRET_DEVICE_NAME" }).ToSafeSummary()
                .Contains("TpmVersion: UNKNOWN", StringComparison.Ordinal) &&
            (healthy with { DomainJoinStatus = "YES", WritableDcStatus = "NOT_FOUND" })
                .ToSafeSummary().Contains("DpapiWritableDc: NOT_FOUND", StringComparison.Ordinal) &&
            (healthy with { AikEnrollmentError = "NOT_FOUND" }).NeedsAttestationReview &&
            !(healthy with { AikEnrollmentError = "UNKNOWN" }).NeedsAttestationReview &&
            (healthy with { TpmFirmwareVersion = "123456SECRET",
                TpmManufacturer = "private-device-id", AikEnrollmentError = "0x80070490" })
                .ToSafeSummary().Contains("TpmAikEnrollment: UNKNOWN", StringComparison.Ordinal) &&
            (healthy with { WritableDcStatus = "dc01.internal.example" })
                .ToSafeSummary().Contains("DpapiWritableDc: UNKNOWN", StringComparison.Ordinal) &&
            TpmBitLockerProbe.ValidDomainName("example.test") &&
            !TpmBitLockerProbe.ValidDomainName("invalid;cmd.exe") &&
            !TpmBitLockerProbe.ValidDomainName("not-a-domain") &&
            TpmBitLockerProbe.WindowsReleaseClass("26100") == "WIN11_24H2" &&
            TpmBitLockerProbe.VendorClass(0x4D534654u) == "MSFT" &&
            TpmBitLockerProbe.VendorClass(0x53544D20u) == "STM" &&
            TpmBitLockerProbe.AikErrorClass(0x80070490u) == "NOT_FOUND" &&
            TpmBitLockerProbe.AikErrorClass(0x800705B4u) == "TIMEOUT" &&
            TpmBitLockerProbe.FirmwareVersion("9.8.7") == "9.8.7" &&
            TpmBitLockerProbe.FirmwareVersion("device secret") == "UNKNOWN";
    }
}

internal static class TpmBitLockerProbe
{
    internal static string VendorClass(object? raw)
    {
        if (raw is null) return "UNKNOWN";
        try
        {
            var n = Convert.ToUInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
            return n switch
            {
                0x53544D20u => "STM",
                0x4D534654u => "MSFT",
                0u => "UNKNOWN",
                _ => "OTHER"
            };
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException
            or OverflowException) { return "UNKNOWN"; }
    }

    internal static string FirmwareVersion(string? value) =>
        value is { Length: > 0 and <= 32 } &&
        Regex.IsMatch(value, @"^[0-9]+(?:\.[0-9]+){0,4}$",
            RegexOptions.CultureInvariant)
            ? value : "UNKNOWN";

    internal static string WindowsReleaseClass(string? build)
    {
        if (!int.TryParse(build, out var n)) return "UNKNOWN";
        return n switch
        {
            26100 => "WIN11_24H2",
            26200 => "WIN11_25H2",
            >= 26201 => "WIN11_NEWER",
            > 0 => "OTHER",
            _ => "UNKNOWN"
        };
    }

    internal static string AikErrorClass(object? value)
    {
        if (value is null) return "UNKNOWN";
        uint code;
        try { code = Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException
            or OverflowException) { return "UNKNOWN"; }
        return code switch
        {
            0u => "NONE_RECORDED",
            0x80070490u => "NOT_FOUND",
            0x800705B4u => "TIMEOUT",
            _ => "OTHER_ERROR"
        };
    }

    private static (string Error, string EkStore) ReadAttestation()
    {
        try
        {
            using var tpm = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\TPM\WMI", writable: false);
            string error = AikErrorClass(tpm?.GetValue("AIKEnrollmentErrorCode"));
            // The EK certificate's absence can be indicated by
            // Win32_Tpm.IsReadyInformation (0x40000) below. Do not export,
            // deserialize or inspect the certificate or endorsement key.
            return (error, "UNKNOWN");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
            or System.Security.SecurityException or ArgumentException)
        { return ("UNKNOWN", "UNKNOWN"); }
    }

    private static string ReadWindowsRelease()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false);
            return WindowsReleaseClass(key?.GetValue("CurrentBuildNumber")?.ToString());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
            or System.Security.SecurityException or ArgumentException)
        { return "UNKNOWN"; }
    }

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
        bool? Activated, bool? Owned, string Manufacturer, string Firmware, string EkStore) ReadTpm()
    {
        try
        {
            using var query = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftTpm",
                "SELECT IsEnabled_InitialValue, IsActivated_InitialValue, IsOwned_InitialValue, SpecVersion, ManufacturerId, ManufacturerVersion FROM Win32_Tpm");
            using var items = query.Get();
            foreach (ManagementObject item in items)
            {
                using (item)
                {
                    var value = item["SpecVersion"] as string ?? "";
                    var version = value.StartsWith("2.0", StringComparison.Ordinal)
                        ? "2.0" : value.StartsWith("1.2", StringComparison.Ordinal)
                            ? "1.2" : "UNKNOWN";
                    string vendor = VendorClass(item["ManufacturerId"]);
                    string firmware = FirmwareVersion(item["ManufacturerVersion"] as string);
                    string ekStatus = "UNKNOWN";
                    try
                    {
                        using var state = item.InvokeMethod("IsReadyInformation", null, null);
                        if (state is not null &&
                            Convert.ToUInt32(state["ReturnValue"]) == 0u &&
                            state["Information"] is not null)
                        {
                            uint flags = Convert.ToUInt32(state["Information"]);
                            if ((flags & 0x00040000u) != 0u)
                                ekStatus = "IMPORT_INCOMPLETE";
                            // An unset bit is not proof of EK certificate
                            // validity, a manufacturer EK-certificate chain, or attestation success.
                        }
                    }
                    catch (Exception ex) when (WmiAccess(ex) ||
                        ex is ArgumentException or FormatException or OverflowException) { }
                    return ("YES", version,
                        LiveTpmFlag(item, "IsReady", "IsReady", null),
                        LiveTpmFlag(item, "IsEnabled", "IsEnabled",
                            ReadBool(item, "IsEnabled_InitialValue")),
                        LiveTpmFlag(item, "IsActivated", "IsActivated",
                            ReadBool(item, "IsActivated_InitialValue")),
                        LiveTpmFlag(item, "IsOwned", "IsOwned",
                            ReadBool(item, "IsOwned_InitialValue")),
                        vendor, firmware, ekStatus);
                }
            }
            // A successful query with no instance normally means no TPM.
            return ("NO", "UNKNOWN", null, null, null, null, "UNKNOWN", "UNKNOWN", "UNKNOWN");
        }
        catch (Exception ex) when (WmiAccess(ex))
        { return ("UNKNOWN", "UNKNOWN", null, null, null, null, "UNKNOWN", "UNKNOWN", "UNKNOWN"); }
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

    // Domain join is a machine attribute, not proof that the affected user is
    // a domain account. DC discovery is advisory for DPAPI first-logon issues.
    internal static bool ValidDomainName(string? value) =>
        value is { Length: >= 3 and <= 253 } &&
        Regex.IsMatch(value, @"^(?:[A-Za-z0-9-]{1,63}\.)+[A-Za-z0-9-]{2,63}$",
            RegexOptions.CultureInvariant);

    private static (string Joined, string WritableDc) ReadWritableDc()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2", "SELECT PartOfDomain, Domain FROM Win32_ComputerSystem");
            using var items = searcher.Get();
            foreach (ManagementObject item in items)
            {
                using (item)
                {
                    if (item["PartOfDomain"] is not bool joined)
                        return ("UNKNOWN", "UNKNOWN");
                    if (!joined) return ("NO", "NOT_APPLICABLE");
                    var domain = item["Domain"] as string;
                    // Only WMI-provided DNS names, never user-supplied commands.
                    if (!ValidDomainName(domain)) return ("YES", "UNKNOWN");
                    var nltest = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "System32", "nltest.exe");
                    if (!File.Exists(nltest)) return ("YES", "UNKNOWN");
                    var start = new ProcessStartInfo
                    {
                        FileName = nltest, UseShellExecute = false,
                        CreateNoWindow = true, RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    start.ArgumentList.Add("/dsgetdc:" + domain);
                    start.ArgumentList.Add("/writable");
                    start.ArgumentList.Add("/force");
                    using var process = Process.Start(start);
                    if (process is null) return ("YES", "UNKNOWN");
                    if (!process.WaitForExit(6000))
                    {
                        try { process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                        return ("YES", "UNKNOWN");
                    }
                    // Never read or persist DC hostnames, IPs or domains
                    // from nltest stdout/stderr. Exit status only.
                    return process.ExitCode switch
                    {
                        0 => ("YES", "AVAILABLE"),
                        1355 => ("YES", "NOT_FOUND"),
                        _ => ("YES", "UNKNOWN")
                    };
                }
            }
        }
        catch (Exception ex) when (WmiAccess(ex) ||
            ex is System.ComponentModel.Win32Exception or IOException
            or ArgumentException or InvalidCastException)
        { }
        return ("UNKNOWN", "UNKNOWN");
    }

    internal static TpmBitLockerSnapshot Capture()
    {
        // Individual probes fail closed to UNKNOWN. Lack of WMI permissions is
        // not interpreted as TPM absent or BitLocker off.
        var tpm = ReadTpm();
        var drive = ReadSystemVolume();
        var domain = ReadWritableDc();
        var attestation = ReadAttestation();
        return new(tpm.Present, tpm.Version, tpm.Ready, tpm.Enabled, tpm.Activated, tpm.Owned,
            drive.Protection, drive.Conversion,
            ReadHVCIRunning(), ReadHVCIConfigured(), ReadDpapiPolicy(),
            domain.Joined, domain.WritableDc)
        {
            TpmManufacturer = tpm.Manufacturer,
            TpmFirmwareVersion = tpm.Firmware,
            WindowsRelease = ReadWindowsRelease(),
            AikEnrollmentError = attestation.Error,
            EkCertStore = tpm.EkStore == "IMPORT_INCOMPLETE"
                ? "IMPORT_INCOMPLETE" : attestation.EkStore
        };
    }
}
