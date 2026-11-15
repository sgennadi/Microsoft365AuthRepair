using System.Diagnostics;
using System.Text.Json;

namespace Microsoft365AuthRepair;

// Per-machine administration only. This is completely separate from
// PortableSelfCleanup, which is restricted to the desktop user's own EXE.
// Never remove an outstanding logon task or its required ProgramData worker.
internal sealed record ScheduledWorkerSnapshot(
    int Total, int Pending, int Running, int Completed, int Expired, int Foreign,
    bool PowerShellWorkerExists, bool ExeWorkerExists)
{
    internal int Removable => Completed + Expired;
    internal bool CanRemoveWorkers => Total == 0;
}

internal static class ScheduledWorkerMaintenance
{
    private static readonly string Root = Scheduling.ProtectedRoot;
    private static readonly string Ps1 = Path.Combine(Root, "WamReregisterWorker.ps1");
    private static readonly string Exe = Path.Combine(Root, "Microsoft365AuthRepair.exe");

    // The script is static and contains no user input. Check task name,
    // principal AND action target: this only recognizes our app's tasks.
    private static readonly string BaseScript = """
        $ErrorActionPreference = 'Stop'
        $service = New-Object -ComObject Schedule.Service
        $service.Connect()
        try { $folder = $service.GetFolder('\Microsoft365AuthRepair') }
        catch { $folder = $null }
        $allTasks = if ($null -ne $folder) { @($folder.GetTasks(1)) } else { @() }
        $owned = @()
        $foreign = 0
        $now = Get-Date
        foreach ($t in $allTasks) {
            $name = [string]$t.Name
            if ($name -cnotmatch '^WAM_S_1_5_21_(?:[0-9]+_){3}[0-9]+_[a-f0-9]{8}$') {
                $foreign++
                continue
            }
            try {
                $d = $t.Definition
                if ($d.Actions.Count -ne 1 -or $d.Principal.LogonType -ne 3) {
                    $foreign++
                    continue
                }
                $principalSid = [string]$d.Principal.UserId
                if ($principalSid -notmatch '^S-1-5-21-(?:[0-9]+-){3}[0-9]+$' -or
                    -not $name.StartsWith('WAM_' + $principalSid.Replace('-', '_') + '_')) {
                    $foreign++
                    continue
                }
                $action = $d.Actions.Item(1)
                $path = [string]$action.Path
                $arguments = [string]$action.Arguments
                $expectedExe = Join-Path $env:ProgramData 'Microsoft365AuthRepair\Microsoft365AuthRepair.exe'
                $expectedPs1 = Join-Path $env:ProgramData 'Microsoft365AuthRepair\WamReregisterWorker.ps1'
                $validExe = $path -ieq $expectedExe -and
                    $arguments -match '^--worker [a-f0-9]{32} S-1-5-21-' -and
                    $arguments.EndsWith(' ' + $name)
                $validPs1 = $path -match '(?i)\\powershell\.exe$' -and
                    $arguments.Contains('-File "' + $expectedPs1 + '"') -and
                    $arguments.Contains('-ExpectedSid ' + $principalSid) -and
                    $arguments.EndsWith('-TaskName ' + $name)
                if (-not ($validExe -or $validPs1)) {
                    $foreign++
                    continue
                }
                $isRunning = [int]$t.State -eq 4
                $expired = $false
                foreach ($trigger in $d.Triggers) {
                    try {
                        $end = [string]$trigger.EndBoundary
                        if (-not [string]::IsNullOrWhiteSpace($end)) {
                            $endTime = [datetime]::Parse($end,
                                [Globalization.CultureInfo]::InvariantCulture)
                            if ($endTime -lt $now) { $expired = $true }
                        }
                    } catch { }
                }
                $hasRun = ([datetime]$t.LastRunTime).Year -gt 2000
                $completed = -not $isRunning -and $hasRun -and
                    [int]$t.LastTaskResult -eq 0
                $kind = if ($isRunning) { 'Running' }
                    elseif ($completed) { 'Completed' }
                    elseif ($expired) { 'Expired' }
                    else { 'Pending' }
                $owned += [pscustomobject]@{ Name = $name; Kind = $kind }
            } catch {
                $foreign++
            }
        }
        """;

    private static string ToJsonScript(bool removeStale) => BaseScript + (removeStale
        ? """
            
            $removed = 0
            foreach ($row in $owned) {
                if ($row.Kind -ne 'Completed' -and $row.Kind -ne 'Expired') { continue }
                try { $folder.DeleteTask($row.Name, 0); $removed++ }
                catch { }
            }
            [pscustomobject]@{ Removed = $removed } | ConvertTo-Json -Compress
            """
        : """
            
            [pscustomobject]@{
                Total = @($allTasks).Count
                Pending = @($owned | Where-Object { $_.Kind -eq 'Pending' }).Count
                Running = @($owned | Where-Object { $_.Kind -eq 'Running' }).Count
                Completed = @($owned | Where-Object { $_.Kind -eq 'Completed' }).Count
                Expired = @($owned | Where-Object { $_.Kind -eq 'Expired' }).Count
                Foreign = $foreign
            } | ConvertTo-Json -Compress
            """);

    private static JsonDocument Execute(string script)
    {
        var response = ExternalCommand.RunPowerShell(script, 35);
        if (response.ExitCode != 0)
            throw new IOException("Could not inspect our scheduled tasks: " +
                response.StdErr.Trim().Split('\n').FirstOrDefault());
        var json = response.StdOut.Trim();
        if (json.Length == 0 || json.Length > 8192)
            throw new IOException("Unexpected Task Scheduler response.");
        return JsonDocument.Parse(json);
    }

    private static bool ExistsSafe(string path)
    {
        if (!File.Exists(path)) return false;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Worker path is a reparse point. Cleanup denied.");
        return true;
    }

    internal static ScheduledWorkerSnapshot Inspect()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator access required to inspect all-user tasks.");
        using var json = Execute(ToJsonScript(removeStale: false));
        var item = json.RootElement;
        int Int(string key) => item.GetProperty(key).GetInt32();
        return new ScheduledWorkerSnapshot(Int("Total"), Int("Pending"),
            Int("Running"), Int("Completed"), Int("Expired"), Int("Foreign"),
            ExistsSafe(Ps1), ExistsSafe(Exe));
    }

    internal static string Clean(bool removeStaleTasks, bool removeUnusedWorkers)
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator access required for worker cleanup.");
        int removed = 0;
        if (removeStaleTasks)
        {
            using var json = Execute(ToJsonScript(removeStale: true));
            removed = json.RootElement.GetProperty("Removed").GetInt32();
        }

        // Re-check the entire folder after task deletion, including foreign
        // entries. Never delete a worker while ANY registered task remains.
        var after = Inspect();
        int filesRemoved = 0;
        if (removeUnusedWorkers && after.CanRemoveWorkers)
        {
            foreach (var path in new[] { Ps1, Exe })
            {
                if (!ExistsSafe(path)) continue;
                if (Path.GetFullPath(Environment.ProcessPath ?? "").Equals(
                        Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    continue; // Do not remove an executable that is running us.
                try
                {
                    File.Delete(path);
                    filesRemoved++;
                }
                catch (IOException) { /* An active process can hold the file open. */ }
                catch (UnauthorizedAccessException) { }
            }
        }
        return "Removed old tasks: " + removed + ". Removed unused workers: " + filesRemoved +
            ". Remaining tasks: " + after.Total + " (" + after.Pending +
            " pending, " + after.Running + " running, " + after.Foreign + " other).";
    }

    internal static bool SelfTest()
    {
        var safe = new ScheduledWorkerSnapshot(0, 0, 0, 0, 0, 0, true, true);
        var waiting = safe with { Total = 1, Pending = 1 };
        var running = safe with { Total = 1, Running = 1 };
        return safe.CanRemoveWorkers && !waiting.CanRemoveWorkers &&
            !running.CanRemoveWorkers && waiting.Removable == 0 &&
            (safe with { Total = 2, Completed = 1, Expired = 1 }).Removable == 2 &&
            Ps1.EndsWith(@"Microsoft365AuthRepair\WamReregisterWorker.ps1",
                StringComparison.OrdinalIgnoreCase) &&
            Exe.EndsWith(@"Microsoft365AuthRepair\Microsoft365AuthRepair.exe",
                StringComparison.OrdinalIgnoreCase) &&
            !ToJsonScript(false).Contains("DeleteTask", StringComparison.Ordinal) &&
            ToJsonScript(true).Contains("DeleteTask", StringComparison.Ordinal);
    }
}
