using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Read-only, privacy-safe identity/TPM evidence. Do not expose TPM owner auth,
// BitLocker recovery keys, CNG containers, user SIDs or Entra device IDs.
internal sealed record IdentityKeySignals(
    string TpmPresent, string TpmReady, string TpmLockedOut,
    string BitLockerProtection, string TpmProtected, string KeySignTest,
    string AadRecoveryEnabled, string NgcSet)
{
    internal static IdentityKeySignals Unknown => new(
        "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN",
        "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN");

    internal bool TpmNeedsReview => TpmLockedOut == "YES" ||
        (TpmPresent == "YES" && TpmReady == "NO");

    internal bool DeviceKeyNeedsReview =>
        KeySignTest == "FAILED" || AadRecoveryEnabled == "YES";

    internal bool RequiresReview => TpmNeedsReview || DeviceKeyNeedsReview;

    // Coarse local-only status for testing/recommendations. It is intentionally
    // NOT added to the anonymous UniversalDiagnostics gateway payload.
    internal string LocalSummary => RequiresReview ? "NEEDS_REVIEW" :
        TpmReady == "YES" && TpmLockedOut == "NO" &&
        KeySignTest == "PASSED" && AadRecoveryEnabled == "NO"
            ? "CHECKS_PASSED" : "NOT_VERIFIED";

    private static string Read(string report, string key, params string[] allowed)
    {
        var match = Regex.Match(report,
            @"(?im)^\s*" + Regex.Escape(key) + @"\s*:\s*([A-Za-z_]+)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) return "UNKNOWN";
        var value = match.Groups[1].Value.ToUpperInvariant();
        return allowed.Contains(value, StringComparer.Ordinal) ? value : "UNKNOWN";
    }

    internal static IdentityKeySignals FromReport(string report) => new(
        Read(report, "TpmPresent", "YES", "NO"),
        Read(report, "TpmReady", "YES", "NO"),
        Read(report, "TpmLockedOut", "YES", "NO"),
        Read(report, "BitLockerProtection", "ON", "OFF"),
        Read(report, "TpmProtected", "YES", "NO"),
        Read(report, "KeySignTest", "PASSED", "FAILED"),
        Read(report, "AadRecoveryEnabled", "YES", "NO"),
        Read(report, "NgcSet", "YES", "NO"));

    // Only enumerated non-identifying values are returned, not PowerShell objects.
    internal string MachineReport() =>
        "TpmPresent: " + TpmPresent + Environment.NewLine +
        "TpmReady: " + TpmReady + Environment.NewLine +
        "TpmLockedOut: " + TpmLockedOut + Environment.NewLine +
        "BitLockerProtection: " + BitLockerProtection + Environment.NewLine;
}

internal static class IdentityKeyDiagnostics
{
    // These Windows PowerShell cmdlets only read the current machine's status.
    // Never run Clear-Tpm, Remove-*, Suspend-BitLocker, key export or device join.
    // If PowerShell/AppLocker or current permissions block the query: UNKNOWN.
    internal static string CaptureMachine()
    {
        const string script = @"
function BoolToken($value) {
    if ($null -eq $value) { return 'UNKNOWN' }
    if ($value -is [bool]) { if ($value) { return 'YES' } else { return 'NO' } }
    return 'UNKNOWN'
}
try {
    $t = Get-Tpm -ErrorAction Stop
    if ($null -ne $t) {
        Write-Output ('TpmPresent: ' + (BoolToken $t.TpmPresent))
        Write-Output ('TpmReady: ' + (BoolToken $t.TpmReady))
        Write-Output ('TpmLockedOut: ' + (BoolToken $t.LockedOut))
    }
} catch { }
try {
    $volume = Get-BitLockerVolume -MountPoint $env:SystemDrive -ErrorAction Stop
    $protection = [string]$volume.ProtectionStatus
    if ($protection -eq 'On') { Write-Output 'BitLockerProtection: ON' }
    elseif ($protection -eq 'Off') { Write-Output 'BitLockerProtection: OFF' }
} catch { }
";
        try
        {
            var result = ExternalCommand.RunPowerShell(script, 25);
            if (result.ExitCode == 0)
                return IdentityKeySignals.FromReport(result.StdOut).MachineReport();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or TimeoutException or
            InvalidOperationException or System.Security.SecurityException)
        {
            // Raw command output and exceptions must never reach a public report.
        }
        return IdentityKeySignals.Unknown.MachineReport();
    }

    internal static bool SelfTest()
    {
        const string healthy = "TpmPresent: YES\nTpmReady: YES\nTpmLockedOut: NO\n" +
            "BitLockerProtection: ON\nTpmProtected: YES\nKeySignTest: PASSED\n" +
            "AadRecoveryEnabled: NO\nNgcSet: YES\n";
        var pass = IdentityKeySignals.FromReport(healthy);
        var locked = IdentityKeySignals.FromReport(healthy.Replace(
            "TpmLockedOut: NO", "TpmLockedOut: YES", StringComparison.Ordinal));
        var keyFailure = IdentityKeySignals.FromReport(healthy.Replace(
            "KeySignTest: PASSED", "KeySignTest: FAILED", StringComparison.Ordinal));
        var recovery = IdentityKeySignals.FromReport(healthy.Replace(
            "AadRecoveryEnabled: NO", "AadRecoveryEnabled: YES", StringComparison.Ordinal));
        var unknown = IdentityKeySignals.FromReport(
            "TpmPresent: OWNER_AUTH_PRIVATE\nKeySignTest: MUST_RUN_ELEVATED\n" +
            "BitLockerProtection: 1234-REDACT-ME\n");
        return pass.LocalSummary == "CHECKS_PASSED" && !pass.RequiresReview &&
            locked.TpmNeedsReview && keyFailure.DeviceKeyNeedsReview &&
            recovery.RequiresReview && locked.LocalSummary == "NEEDS_REVIEW" &&
            unknown.TpmPresent == "UNKNOWN" && unknown.KeySignTest == "UNKNOWN" &&
            unknown.BitLockerProtection == "UNKNOWN" &&
            unknown.LocalSummary == "NOT_VERIFIED" &&
            !pass.MachineReport().Contains("OwnerAuth", StringComparison.Ordinal) &&
            !pass.MachineReport().Contains("RecoveryPassword", StringComparison.Ordinal);
    }
}
