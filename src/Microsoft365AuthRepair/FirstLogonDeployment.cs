using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

// Migration support only: Preview.5 included an unnecessary HKLM Run action
// for people who did not yet have a profile. Preview.6 never registers it.
// This method can remove only the exact registration made by this product,
// without touching unrelated startup entries.
internal static class FirstLogonDeployment
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Microsoft365AuthRepair_FirstSignIn";
    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Microsoft365AuthRepair");
    private static string OriginalCommandPrefix =>
        "\"" + Path.Combine(Root, "Microsoft365AuthRepair.exe") + "\" --first-logon ";

    internal static bool DisableLegacyIfPresent()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights are required to remove a machine startup entry.");
        using var key = Registry.LocalMachine.OpenSubKey(RunKey, writable: true);
        if (key is null) return false;
        var value = key.GetValue(RunValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if (string.IsNullOrEmpty(value)) return false;
        if (!IsOurs(value)) return false; // Never delete an unrelated entry.

        key.DeleteValue(RunValue, throwOnMissingValue: false);
        // Belt and suspenders: an older protected worker also checks this
        // campaign file; removing it prevents a stale Run entry from acting.
        var oldCampaign = Path.Combine(Root, "first-logon-campaign.json");
        if (File.Exists(oldCampaign))
        {
            if ((File.GetAttributes(oldCampaign) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing to delete a linked campaign file.");
            File.Delete(oldCampaign);
        }
        return true;
    }

    private static bool IsOurs(string value)
    {
        var prefix = OriginalCommandPrefix;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var id = value[prefix.Length..];
        return Regex.IsMatch(id, @"\A[a-f0-9]{32}\z", RegexOptions.CultureInvariant);
    }

    internal static bool SelfTest() =>
        IsOurs(OriginalCommandPrefix + new string('a', 32)) &&
        !IsOurs(OriginalCommandPrefix + "../unrelated") &&
        !IsOurs("\"C:\\Other\\app.exe\" --first-logon " + new string('a', 32));
}
