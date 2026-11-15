using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Identifies the owner of the INTERACTIVE desktop, not merely the account
// used to answer a UAC prompt. Never assume an elevated admin is the target.
internal sealed record DesktopUser(string Name, string Sid, bool Verified)
{
    internal bool SameAsProcess => Sid.Length > 0 &&
        Sid.Equals(Identity.CurrentSid(), StringComparison.OrdinalIgnoreCase);
}

internal static class UserScopeSupport
{
    internal static DesktopUser Resolve()
    {
        var processSid = Identity.CurrentSid();
        using var identity = Identity.Current();
        var processName = identity.Name;
        try
        {
            var allSessions = Native.FindExplorerSessions();
            if (allSessions.TryGetValue(Identity.SessionId(), out var users) && users.Count == 1)
            {
                var sid = users.Single();
                string name = sid;
                try { name = new SecurityIdentifier(sid).Translate(typeof(NTAccount)).Value; }
                catch (IdentityNotMappedException) { /* Display SID if domain unavailable. */ }
                return new DesktopUser(name, sid, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            // Do not default to an unrelated elevated administrator identity.
        }
        return Identity.IsElevated()
            ? new DesktopUser("Unknown desktop user", "", false)
            : new DesktopUser(processName, processSid, false);
    }

    internal static bool RequiresUac(bool allUsers, bool elevated) => allUsers && !elevated;

    internal static bool CanRunCurrent(bool elevated, DesktopUser desktop) =>
        !elevated || desktop.Verified;

    internal static bool TryElevateForAll(out string? error)
    {
        error = null;
        try
        {
            var exe = Environment.ProcessPath ??
                throw new FileNotFoundException("Application EXE not available.");
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe))
            };
            // Only selects the scope after elevation. Never executes an
            // identity reset or other repair automatically after UAC.
            psi.ArgumentList.Add("--all-users");
            using var p = Process.Start(psi) ??
                throw new InvalidOperationException("Windows did not open the administrator window.");
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "UAC_CANCELLED";
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    internal static bool SelfTest() =>
        RequiresUac(true, false) && !RequiresUac(false, false) &&
        !RequiresUac(true, true) &&
        CanRunCurrent(false, new DesktopUser("DOMAIN\\someone", "", false)) &&
        !CanRunCurrent(true, new DesktopUser("Unknown", "", false)) &&
        CanRunCurrent(true, new DesktopUser("DOMAIN\\someone", "S-1-5-21-1-2-3-4", true));
}
