using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

internal static class Scheduling
{
    private static readonly Regex SidPattern = new(@"^S-1-5-21-(?:\d+-){3}\d+$", RegexOptions.CultureInvariant);
    private const string FolderName = "Microsoft365AuthRepair";
    private static readonly string SharedRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), FolderName);
    private static readonly string SharedExe = System.IO.Path.Combine(SharedRoot, "Microsoft365AuthRepair.exe");

    internal static string ProtectedRoot => SharedRoot;

    internal static void EnsureProtectedRoot()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights required for protected updater staging.");
        CheckNotReparse(SharedRoot);
        Directory.CreateDirectory(SharedRoot);
        CheckNotReparse(SharedRoot);
        ProtectDirectory();
    }

    internal static void PrepareProtectedExe()
    {
        EnsureProtectedRoot();
        CheckNotReparse(SharedExe);
        var source = Environment.ProcessPath ?? throw new IOException("Cannot identify executing EXE path.");
        if (!System.IO.Path.GetFullPath(source).Equals(System.IO.Path.GetFullPath(SharedExe), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(SharedExe)) File.Delete(SharedExe);
            File.Copy(source, SharedExe);
        }
        ProtectDirectory();
        using var a = File.OpenRead(source);
        using var b = File.OpenRead(SharedExe);
        var hashA = SHA256.HashData(a);
        var hashB = SHA256.HashData(b);
        if (!CryptographicOperations.FixedTimeEquals(hashA, hashB))
            throw new IOException("Staged EXE hash differs from the executing EXE. Aborting.");
        Console.WriteLine("Verified protected worker EXE: " + SharedExe);
    }

    private static void CheckNotReparse(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing reparse point in protected worker path: " + path);
    }

    private static void ProtectDirectory()
    {
        var icacls = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "icacls.exe");
        var result = ExternalCommand.Run(icacls,
            [SharedRoot, "/inheritance:r", "/grant:r", "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-544:(OI)(CI)F", "*S-1-5-32-545:(OI)(CI)RX", "/T"], 60);
        if (result.ExitCode != 0) throw new IOException("Could not lock down worker folder ACL: " + result.StdErr.Trim() + " " + result.StdOut.Trim());
    }

    internal static string QueueForSid(WindowsProfile profile, bool userOnline, string repairAction)
    {
        if (!SidPattern.IsMatch(profile.Sid)) throw new ArgumentException("Invalid profile SID.");
        if (repairAction is not ("reregister" or "full")) throw new ArgumentException("Invalid repair action.");
        var runId = Guid.NewGuid().ToString("N");
        var taskName = "WAM_" + profile.Sid.Replace('-', '_') + "_" + runId[..8];
        var args = $"--worker {runId} {profile.Sid} {taskName} {repairAction}";
        static string Q(string value) => value.Replace("'", "''", StringComparison.Ordinal);

        // PowerShell is only used as a COM bridge to Windows Task Scheduler.
        // The repair itself is entirely implemented by this .NET executable.
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $service = New-Object -ComObject Schedule.Service
            $service.Connect()
            try { $folder = $service.GetFolder('\Microsoft365AuthRepair') }
            catch { $folder = $service.GetFolder('\').CreateFolder('Microsoft365AuthRepair', $null) }
            $d = $service.NewTask(0)
            $d.RegistrationInfo.Description = 'One-time per-user WAM reset by Microsoft365AuthRepair.exe'
            $d.Principal.UserId = '{{Q(profile.Sid)}}'
            $d.Principal.LogonType = 3
            $d.Principal.RunLevel = 0
            $d.Settings.Enabled = $true
            $d.Settings.AllowDemandStart = $true
            $d.Settings.StartWhenAvailable = $true
            $d.Settings.ExecutionTimeLimit = 'PT15M'
            $d.Settings.DeleteExpiredTaskAfter = 'PT15M'
            $d.Settings.MultipleInstances = 2
            $tr = $d.Triggers.Create(9)
            $tr.UserId = '{{Q(profile.Sid)}}'
            $tr.StartBoundary = (Get-Date).AddMinutes(-1).ToString('s')
            $tr.EndBoundary = (Get-Date).AddDays(90).ToString('s')
            $a = $d.Actions.Create(0)
            $a.Path = '{{Q(SharedExe)}}'
            $a.Arguments = '{{Q(args)}}'
            $t = $folder.RegisterTaskDefinition('{{Q(taskName)}}', $d, 6, '{{Q(profile.Sid)}}', $null, 3, $null)
            if ({{(userOnline ? "$true" : "$false")}}) { $null = $t.Run($null) }
            Write-Output 'TASK_REGISTERED'
            """;
        var response = ExternalCommand.RunPowerShell(script, 60);
        if (response.ExitCode != 0 || !response.StdOut.Contains("TASK_REGISTERED", StringComparison.Ordinal))
            throw new IOException("Task Scheduler could not register " + profile.Account + ": " + response.StdErr.Trim() + " " + response.StdOut.Trim());
        return $"{profile.Account}: {(userOnline ? "STARTED" : "QUEUED FOR NEXT SIGN-IN")} | {taskName}";
    }

    internal static int Worker(string runId, string expectedSid, string taskName, string repairAction)
    {
        Identity.RequireInteractiveUser();
        if (!Regex.IsMatch(runId, "^[a-f0-9]{32}$") || !SidPattern.IsMatch(expectedSid) || repairAction is not ("reregister" or "full"))
            throw new ArgumentException("Invalid worker arguments.");
        var expectedName = "WAM_" + expectedSid.Replace('-', '_') + "_" + runId[..8];
        if (!string.Equals(taskName, expectedName, StringComparison.Ordinal))
            throw new ArgumentException("Invalid worker task name.");
        if (!string.Equals(Identity.CurrentSid(), expectedSid, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Worker token does not match the intended Windows profile SID.");

        var doneDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft365AuthRepair", "Completed");
        Directory.CreateDirectory(doneDir);
        var attempted = System.IO.Path.Combine(doneDir, runId + ".attempted");
        var complete = System.IO.Path.Combine(doneDir, runId + ".ok");
        try
        {
            using var onlyOnce = new FileStream(attempted, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = Encoding.UTF8.GetBytes(DateTimeOffset.Now.ToString("O"));
            onlyOnce.Write(bytes);
        }
        catch (IOException)
        {
            // Even an unsuccessful full reset must not be silently repeated at every sign-in.
            TryDeleteOwnTask(taskName);
            return File.Exists(complete) ? 0 : 2;
        }

        var exitCode = 2;
        try
        {
            exitCode = repairAction == "full" ? Repair.Run(quiet: true) : Repair.ReRegister(quiet: true);
            if (exitCode == 0) File.WriteAllText(complete, DateTimeOffset.Now.ToString("O"));
            return exitCode;
        }
        finally { TryDeleteOwnTask(taskName); }
    }

    private static void TryDeleteOwnTask(string taskName)
    {
        try
        {
            var script = "$s=New-Object -ComObject Schedule.Service; $s.Connect();" +
                "$s.GetFolder('\\Microsoft365AuthRepair').DeleteTask('" + taskName.Replace("'", "''", StringComparison.Ordinal) + "',0)";
            _ = ExternalCommand.RunPowerShell(script, 20);
        }
        catch { /* Limited users might not have rights: expiry + .attempted marker prevents replay. */ }
    }
}
