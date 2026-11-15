using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

internal enum ScheduledWorkerMode { Exe, PowerShell }

internal static class Scheduling
{
    private static readonly Regex SidPattern = new(@"^S-1-5-21-(?:\d+-){3}\d+$", RegexOptions.CultureInvariant);
    private const string FolderName = "Microsoft365AuthRepair";
    private static readonly string SharedRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), FolderName);
    private static readonly string SharedExe = System.IO.Path.Combine(SharedRoot, "Microsoft365AuthRepair.exe");
    private static readonly string SharedScript = System.IO.Path.Combine(SharedRoot, "WamReregisterWorker.ps1");

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

    // A real standalone PowerShell fallback for WAM re-registration only.
    // The embedded script is materialized under administrator-owned ProgramData.
    // It does NOT call the EXE, so it remains usable if someone deletes the
    // protected worker binary. Machine/group policy still governs PS execution.
    internal static void PrepareProtectedPowerShell()
    {
        EnsureProtectedRoot();
        const string embeddedName = "Microsoft365AuthRepair.WamReregisterWorker";
        using var source = typeof(Scheduling).Assembly.GetManifestResourceStream(embeddedName)
            ?? throw new IOException("Embedded WAM PowerShell worker was not found.");
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        var expected = memory.ToArray();
        if (expected.Length < 1000 || expected.Length > 100_000)
            throw new InvalidDataException("Unexpected embedded worker size.");

        CheckNotReparse(SharedScript);
        if (File.Exists(SharedScript))
        {
            using var stream = File.OpenRead(SharedScript);
            if (CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(stream), SHA256.HashData(expected)))
            {
                ProtectDirectory();
                return;
            }
        }

        var temporary = SharedScript + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.Write(expected);
            File.Move(temporary, SharedScript, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        ProtectDirectory();
        CheckNotReparse(SharedScript);
        using var verify = File.OpenRead(SharedScript);
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(verify), SHA256.HashData(expected)))
            throw new IOException("PowerShell worker did not pass SHA-256 verification.");
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

    internal static string QueueForSid(WindowsProfile profile, bool userOnline, string repairAction,
        ScheduledWorkerMode mode = ScheduledWorkerMode.Exe, bool scheduleOnly = false)
    {
        if (!SidPattern.IsMatch(profile.Sid)) throw new ArgumentException("Invalid profile SID.");
        if (repairAction is not ("reregister" or "full")) throw new ArgumentException("Invalid repair action.");
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Invalid scheduled worker mode.");
        if (repairAction == "full" && mode == ScheduledWorkerMode.PowerShell)
            throw new NotSupportedException("PowerShell only supports non-destructive WAM re-registration. Use EXE for full reset.");
        var runId = Guid.NewGuid().ToString("N");
        var taskName = "WAM_" + profile.Sid.Replace('-', '_') + "_" + runId[..8];
        var targetExe = mode == ScheduledWorkerMode.PowerShell ? ExternalCommand.PowerShellPath() : SharedExe;
        var args = mode == ScheduledWorkerMode.PowerShell
            ? $"-NoLogo -NoProfile -NonInteractive -File \"{SharedScript}\" -RunId {runId} -ExpectedSid {profile.Sid} -TaskName {taskName}"
            : $"--worker {runId} {profile.Sid} {taskName} {repairAction}";
        var startImmediately = userOnline && !scheduleOnly;
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
            $d.RegistrationInfo.Description = '{{(mode == ScheduledWorkerMode.PowerShell ? "One-time WAM re-register via protected PowerShell script" : "One-time WAM repair by protected Microsoft365AuthRepair.exe")}}'
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
            $a.Path = '{{Q(targetExe)}}'
            $a.Arguments = '{{Q(args)}}'
            $t = $folder.RegisterTaskDefinition('{{Q(taskName)}}', $d, 6, '{{Q(profile.Sid)}}', $null, 3, $null)
            if ({{(startImmediately ? "$true" : "$false")}}) { $null = $t.Run($null) }
            Write-Output 'TASK_REGISTERED'
            """;
        var response = ExternalCommand.RunPowerShell(script, 60);
        if (response.ExitCode != 0 || !response.StdOut.Contains("TASK_REGISTERED", StringComparison.Ordinal))
            throw new IOException("Task Scheduler could not register " + profile.Account + ": " + response.StdErr.Trim() + " " + response.StdOut.Trim());
        var state = startImmediately ? "STARTED" : "QUEUED FOR NEXT SIGN-IN";
        var engine = mode == ScheduledWorkerMode.PowerShell ? "PowerShell" : "EXE";
        return $"{profile.Account}: {state} [{engine}] | {taskName}";
    }

    internal static bool SelfTest()
    {
        // Ensures the independent PowerShell worker is truly embedded in
        // the single-file EXE rather than fetched from the original folder.
        using var stream = typeof(Scheduling).Assembly.GetManifestResourceStream(
            "Microsoft365AuthRepair.WamReregisterWorker");
        if (stream is null) return false;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var script = reader.ReadToEnd();
        return script.Length is > 1000 and < 100_000
            && script.Contains("Add-AppxPackage @arguments", StringComparison.Ordinal)
            && script.Contains("ExpectedSid", StringComparison.Ordinal)
            && !script.Contains("ExecutionPolicy Bypass", StringComparison.OrdinalIgnoreCase)
            && Enum.IsDefined(ScheduledWorkerMode.Exe)
            && Enum.IsDefined(ScheduledWorkerMode.PowerShell);
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
