using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

/// <summary>
/// Optional admin-approved campaign for accounts with no existing Windows profile.
/// HKLM Run launches protected EXE in each *real* interactive user's session.
/// One-time markers guarantee no repeated destructive actions. This never attempts
/// to operate on another user's WAM under admin/SYSTEM or uses a password.
/// </summary>
internal static class FirstLogonDeployment
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Microsoft365AuthRepair_FirstSignIn";
    private const string CampaignFileName = "first-logon-campaign.json";
    private static readonly Regex ValidId = new(@"^[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex ValidSid = new(@"^S-1-5-21-(?:\d+-){3}\d+$", RegexOptions.CultureInvariant);

    private sealed class Campaign
    {
        public string Id { get; set; } = "";
        public string Action { get; set; } = "";
        public DateTimeOffset ExpiresUtc { get; set; }
        public List<string> ExistingProfileSids { get; set; } = [];
    }

    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Microsoft365AuthRepair");

    private static string ProtectedExe => Path.Combine(Root, "Microsoft365AuthRepair.exe");
    private static string CampaignPath => Path.Combine(Root, CampaignFileName);

    internal static string Register(string action)
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights are required for an all-users first-sign-in campaign.");
        if (action is not ("full" or "reregister"))
            throw new ArgumentException("Invalid campaign action.");

        // The caller has already shown explicit warning + admin confirmation.
        // The existing protected storage has Administrators and SYSTEM F, Users RX.
        // The all-users scheduler prepared the protected file before this
        // method. Do not rewrite it again: another user may have a live handle.
        if (!File.Exists(ProtectedExe) || (File.GetAttributes(ProtectedExe) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Protected worker executable is unavailable.");

        var id = Guid.NewGuid().ToString("N");
        var campaign = new Campaign { Id = id, Action = action,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(90),
            // Profiles already present are handled by the SID-specific tasks.
            // Excluding them prevents a second full reset at their next logon.
            ExistingProfileSids = UserProfiles.Enumerate().Select(p => p.Sid).ToList()
        };
        File.WriteAllText(CampaignPath, JsonSerializer.Serialize(campaign));
        using var key = Registry.LocalMachine.CreateSubKey(RunKey, writable: true)
            ?? throw new IOException("Cannot register the machine logon campaign.");
        // No credentials or user IDs in the command line; future accounts with
        // no existing profile are picked up after their first real sign-in.
        key.SetValue(RunValue, $"\"{ProtectedExe}\" --first-logon {id}", RegistryValueKind.String);
        return "FIRST SIGN-IN CAMPAIGN REGISTERED: once per user, 90-day expiry. " +
               "The shared protected executable is intentionally retained for offline users.";
    }

    internal static int Worker(string campaignId)
    {
        if (!ValidId.IsMatch(campaignId) ||
            !Path.GetFullPath(Environment.ProcessPath ?? "").Equals(
                Path.GetFullPath(ProtectedExe), StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(ProtectedExe) & FileAttributes.ReparsePoint) != 0)
            return 2;
        Identity.RequireInteractiveUser();
        var sid = Identity.CurrentSid();
        if (!ValidSid.IsMatch(sid)) return 2;
        Campaign? campaign;
        try
        {
            if ((File.GetAttributes(CampaignPath) & FileAttributes.ReparsePoint) != 0) return 2;
            campaign = JsonSerializer.Deserialize<Campaign>(File.ReadAllText(CampaignPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return 2;
        }
        if (campaign is null || campaign.Id != campaignId ||
            campaign.Action is not ("full" or "reregister") ||
            campaign.ExpiresUtc <= DateTimeOffset.UtcNow ||
            campaign.ExpiresUtc > DateTimeOffset.UtcNow.AddDays(91))
            return 2;
        if (campaign.ExistingProfileSids.Contains(sid, StringComparer.OrdinalIgnoreCase))
            return 0; // Already covered by existing-profile scheduled tasks.

        var completed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft365AuthRepair", "Completed");
        Directory.CreateDirectory(completed);
        var once = Path.Combine(completed, campaignId + ".attempted");
        var succeeded = Path.Combine(completed, campaignId + ".ok");
        try
        {
            // FileMode.CreateNew wins even if multiple logon triggers race.
            using var first = new FileStream(once, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            first.WriteByte(1);
            first.Flush();
        }
        catch (IOException)
        {
            return File.Exists(succeeded) ? 0 : 2;
        }

        var rc = campaign.Action == "full"
            ? Repair.Run(quiet: true)
            : Repair.ReRegister(quiet: true);
        if (rc == 0)
            File.WriteAllText(succeeded, DateTimeOffset.UtcNow.ToString("O"));
        // Do not delete the protected shared EXE: other profiles may be offline.
        return rc;
    }

    internal static bool SelfTest() =>
        ValidId.IsMatch(new string('a', 32)) &&
        !ValidId.IsMatch("unknown-profile") &&
        ValidSid.IsMatch("S-1-5-21-1-2-3-1001");
}
