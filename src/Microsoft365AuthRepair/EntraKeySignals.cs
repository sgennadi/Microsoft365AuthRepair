using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Only fixed, non-identifying values are accepted from dsregcmd /status.
// Device ID, tenant ID, NgcKeyId, KeyContainerId and certificate data are
// never stored or included in this model or anonymous diagnostics.
internal sealed record EntraKeySignals(
    string TpmProtected, string KeySignTest, string AadRecoveryEnabled, string NgcSet)
{
    internal bool NeedsReview => KeySignTest == "FAILED" || AadRecoveryEnabled == "YES";

    // TpmProtected=NO can be a valid software-backed device key.
    // NgcSet=NO can be expected for a user without Windows Hello.
    private static string Read(string input, string name, params string[] allowed)
    {
        var result = Regex.Match(input,
            @"(?im)^\s*" + Regex.Escape(name) + @"\s*:\s*([A-Za-z_]+)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!result.Success) return "UNKNOWN";
        var token = result.Groups[1].Value.ToUpperInvariant();
        return allowed.Contains(token, StringComparer.Ordinal) ? token : "UNKNOWN";
    }

    internal static EntraKeySignals FromReport(string report) => new(
        Read(report, "TpmProtected", "YES", "NO"),
        Read(report, "KeySignTest", "PASSED", "FAILED"),
        Read(report, "AadRecoveryEnabled", "YES", "NO"),
        Read(report, "NgcSet", "YES", "NO"));

    internal static bool SelfTest()
    {
        const string source = "TpmProtected: NO\nKeySignTest: PASSED\n" +
            "AadRecoveryEnabled: NO\nNgcSet: NO\n" +
            "DeviceId: PRIVATE_DEVICE_ID\nNgcKeyId: PRIVATE_NGC_KEY_ID";
        var good = FromReport(source);
        var bad = FromReport(source.Replace(
            "KeySignTest: PASSED", "KeySignTest: FAILED", StringComparison.Ordinal));
        var recovery = FromReport(source.Replace(
            "AadRecoveryEnabled: NO", "AadRecoveryEnabled: YES", StringComparison.Ordinal));
        var unknown = FromReport("KeySignTest: ERROR ACCESS_DENIED\n" +
            "NgcSet: PRIVATE_VALUE\nTpmProtected: BAD\nAadRecoveryEnabled: 000");
        return !good.NeedsReview && good.TpmProtected == "NO" &&
            good.NgcSet == "NO" && bad.NeedsReview && recovery.NeedsReview &&
            unknown.KeySignTest == "UNKNOWN" && unknown.NgcSet == "UNKNOWN" &&
            unknown.TpmProtected == "UNKNOWN" &&
            unknown.AadRecoveryEnabled == "UNKNOWN" &&
            !good.ToString().Contains("PRIVATE_DEVICE_ID", StringComparison.Ordinal) &&
            !good.ToString().Contains("PRIVATE_NGC_KEY_ID", StringComparison.Ordinal);
    }
}
