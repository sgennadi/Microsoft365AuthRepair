using System.Diagnostics;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

internal static class Repair
{
    internal const string Version = "1.0.1-preview.5";
    private static readonly string[] RegistryKeys =
    [
        @"Software\Microsoft\Office\16.0\Common\Identity",
        @"Software\Microsoft\IdentityCRL\TokenBroker\DefaultAccount"
    ];

    private static readonly string[] CacheRelativePaths =
    [
        @"Packages\Microsoft.AAD.BrokerPlugin_cw5n1h2txyewy",
        @"Microsoft\IdentityCache",
        @"Microsoft\OneAuth",
        @"Microsoft\TokenBroker\Cache",
        @"Microsoft\TokenBroker\Accounts"
    ];

    internal static int Run(bool quiet)
    {
        Identity.RequireInteractiveUser();
        var ownSid = Identity.CurrentSid();
        var session = Identity.SessionId();
        var shells = Native.FindExplorerSessions();
        if (shells.TryGetValue(session, out var owners) && owners.Count > 0 && !owners.Contains(ownSid))
            throw new InvalidOperationException("Process user differs from the desktop owner. Launch normally, not with Run as different user.");

        using var log = new RepairLog(quiet);
        log.Info($"Microsoft 365 Auth Repair 1.0.1-preview.5, session={session}, account={Environment.UserDomainName}\\{Environment.UserName}");
        log.Warn("FULL RESET: Microsoft/Office sign-in caches will be deleted from this user only. Applications may require sign-in again.");

        var manifest = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps", "Microsoft.AAD.BrokerPlugin_cw5n1h2txyewy", "AppxManifest.xml");
        if (!File.Exists(manifest))
        {
            log.Error("AAD BrokerPlugin system manifest was not found. No cache was removed.");
            return 2;
        }

        // Force the broker to release resources, as successfully tested in an interactive user session.
        try { RegisterAppx(manifest); log.Info("Pre-reset WAM registration/shutdown complete."); }
        catch (Exception ex) { log.Warn($"Could not prepare WAM before reset: {ex.Message}"); }

        try { log.Ok($"Office/Microsoft generic credentials removed: {Native.DeleteOfficeGenericCredentials()}"); }
        catch (Exception ex) { log.Error($"Credential Manager: {ex.Message}"); }

        foreach (var key in RegistryKeys)
        {
            try { DeleteRegistryKey(key, log); }
            catch (Exception ex) { log.Error($"Registry {key}: {ex.Message}"); }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var relative in CacheRelativePaths)
        {
            try { DeleteCache(localAppData, relative, log); }
            catch (Exception ex) { log.Error($"Cache {relative}: {ex.Message}"); }
        }

        try { RegisterAppx(manifest); log.Ok("AAD BrokerPlugin re-registered and verified for current user."); }
        catch (Exception ex) { log.Error($"Final WAM re-registration: {ex.Message}"); }
        PrintJoinSummary(log);
        log.Info($"Completed. Errors={log.ErrorCount}; warnings={log.WarningCount}. Log={log.PathName}");
        return log.ErrorCount == 0 ? 0 : 2;
    }

    internal static int ReRegister(bool quiet)
    {
        Identity.RequireInteractiveUser();
        var sid = Identity.CurrentSid();
        var shells = Native.FindExplorerSessions();
        if (shells.TryGetValue(Identity.SessionId(), out var owners) && owners.Count > 0 && !owners.Contains(sid))
            throw new InvalidOperationException("Process account differs from the desktop owner. Run under the actual signed-in user.");
        using var log = new RepairLog(quiet);
        var manifest = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SystemApps", "Microsoft.AAD.BrokerPlugin_cw5n1h2txyewy", "AppxManifest.xml");
        if (!File.Exists(manifest))
        {
            log.Error("Microsoft.AAD.BrokerPlugin system manifest was not found.");
            return 2;
        }
        try
        {
            RegisterAppx(manifest);
            log.Ok("WAM/AAD BrokerPlugin registration VERIFIED for this user. No sign-out required unless problems persist.");
        }
        catch (Exception ex) { log.Error("WAM re-registration failed: " + ex.Message); }
        return log.ErrorCount == 0 ? 0 : 2;
    }

    internal static string DiagnoseReport()
    {
        var output = new System.Text.StringBuilder();
        output.AppendLine("Microsoft 365 Auth Repair - read-only diagnostics");
        output.AppendLine($"Account: {Environment.UserDomainName}\\{Environment.UserName}");
        output.AppendLine($"Elevated: {Identity.IsElevated()}, Session: {Identity.SessionId()}");
        try
        {
            var result = ExternalCommand.RunPowerShell(
                "$p=Get-AppxPackage -Name Microsoft.AAD.BrokerPlugin -ErrorAction Stop; if($p){'BrokerPlugin: PRESENT ('+$p.Version+')'}else{'BrokerPlugin: MISSING'}", 40);
            output.AppendLine(result.ExitCode == 0 ? result.StdOut.Trim() : "BrokerPlugin: CHECK FAILED");
        }
        catch (Exception ex) { output.AppendLine("BrokerPlugin: check failed (" + ex.GetType().Name + ")"); }
        try
        {
            var dsregPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dsregcmd.exe");
            var dsreg = ExternalCommand.Run(dsregPath, ["/status"], 30);
            foreach (var line in dsreg.StdOut.Split('\n'))
            {
                if (new[] { "DomainJoined", "AzureAdJoined", "DeviceAuthStatus", "AzureAdPrt", "WamDefaultSet" }
                    .Any(x => line.TrimStart().StartsWith(x + " ", StringComparison.Ordinal)))
                    output.AppendLine(line.Trim());
            }
            if (dsreg.StdOut.Contains("AADSTS50155", StringComparison.OrdinalIgnoreCase))
                output.AppendLine("WARNING: AADSTS50155 - Entra device authentication rejected. WAM reset cannot repair Entra device registration.");
        }
        catch (Exception ex) { output.AppendLine("Entra registration: check failed (" + ex.GetType().Name + ")"); }
        return output.ToString();
    }

    private static void RegisterAppx(string manifest)
    {
        // Only AppX registration uses the Windows PowerShell AppX cmdlet. All cleanup logic is native .NET.
        var escaped = manifest.Replace("'", "''", StringComparison.Ordinal);
        var script = "$ErrorActionPreference='Stop';" +
            "$c=Get-Command Add-AppxPackage -ErrorAction Stop;" +
            "$a=@{ Path='" + escaped + "'; Register=$true; DisableDevelopmentMode=$true; ErrorAction='Stop' };" +
            "if($c.Parameters.ContainsKey('ForceTargetApplicationShutdown')){$a.ForceTargetApplicationShutdown=$true}" +
            "elseif($c.Parameters.ContainsKey('ForceApplicationShutdown')){$a.ForceApplicationShutdown=$true};" +
            "Add-AppxPackage @a | Out-Null;" +
            "$p=Get-AppxPackage -Name Microsoft.AAD.BrokerPlugin -ErrorAction Stop;" +
            "if(-not $p){throw 'WAM registration could not be verified.'}";
        var result = ExternalCommand.RunPowerShell(script, 180);
        if (result.ExitCode != 0) throw new IOException($"Add-AppxPackage failed: {result.StdErr.Trim()} {result.StdOut.Trim()}");
    }

    private static void DeleteRegistryKey(string path, RepairLog log)
    {
        var lastSlash = path.LastIndexOf('\\');
        if (lastSlash < 0) throw new ArgumentException("Invalid registry key path.");
        using var parent = Registry.CurrentUser.OpenSubKey(path[..lastSlash], writable: true);
        if (parent is null || parent.OpenSubKey(path[(lastSlash + 1)..]) is null) return;
        parent.DeleteSubKeyTree(path[(lastSlash + 1)..], throwOnMissingSubKey: false);
        log.Ok($"Deleted HKCU: {path}");
    }

    private static void DeleteCache(string localRoot, string relative, RepairLog log)
    {
        var root = System.IO.Path.GetFullPath(localRoot).TrimEnd('\\') + "\\";
        var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(localRoot, relative));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Unsafe cache path outside LocalAppData.");
        if (!Directory.Exists(target)) return;
        VerifyNoReparsePoint(target);
        Directory.Delete(target, recursive: true);
        if (Directory.Exists(target)) throw new IOException("Folder still exists or was recreated by Windows.");
        log.Ok($"Deleted cache: {relative}");
    }

    private static void VerifyNoReparsePoint(string folder)
    {
        var stack = new Stack<string>();
        stack.Push(folder);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cache contains symlink/junction: " + current);
            foreach (var item in Directory.EnumerateFileSystemEntries(current))
            {
                var attr = File.GetAttributes(item);
                if ((attr & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cache contains symlink/junction: " + item);
                if ((attr & FileAttributes.Directory) != 0) stack.Push(item);
            }
        }
    }

    private static void PrintJoinSummary(RepairLog log)
    {
        try
        {
            var dsregPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dsregcmd.exe");
            var result = ExternalCommand.Run(dsregPath, ["/status"], 30);
            foreach (var line in result.StdOut.Split('\n'))
            {
                if (new[] { "DomainJoined", "AzureAdJoined", "DeviceAuthStatus", "AzureAdPrt", "WamDefaultSet" }
                    .Any(k => line.TrimStart().StartsWith(k + " ", StringComparison.Ordinal))) log.Info(line.Trim());
            }
            if (result.StdOut.Contains("AADSTS50155", StringComparison.OrdinalIgnoreCase))
                log.Warn("AADSTS50155: device identity rejected by Entra ID; this application does not modify device registration.");
        }
        catch (Exception ex) { log.Warn("dsregcmd diagnostics failed: " + ex.Message); }
    }
}
