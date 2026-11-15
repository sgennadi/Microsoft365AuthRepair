using System.Text.Json;

namespace Microsoft365AuthRepair;

// Per-machine administration only. No portable EXE cleanup or user-cache
// operations are part of scheduled task inspection or cleanup.
// Never remove an outstanding logon task or its required ProgramData worker.
internal sealed record ScheduledWorkerSnapshot(
    int Total, int Pending, int Running, int Completed, int Expired, int Attention, int Foreign,
    bool PowerShellWorkerExists, bool ExeWorkerExists)
{
    internal int Removable => Completed + Expired;
    internal bool CanRemoveWorkers => Total == 0;
}

internal sealed record ScheduledTaskRuntime(string Name, string UserSid,
    string Kind, string Mode, string Action, int LastResult);

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
        $allTasks = @()
        if ($null -ne $folder) {
            $collection = $folder.GetTasks(1)
            for ($i = 1; $i -le [int]$collection.Count; $i++) {
                $allTasks += $collection.Item($i)
            }
        }
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
                $validExe = $false
                if ($path -ieq $expectedExe -and
                    $arguments -cmatch '^--worker ([a-f0-9]{32}) (S-1-5-21-(?:[0-9]+-){3}[0-9]+) (WAM_S_1_5_21_(?:[0-9]+_){3}[0-9]+_[a-f0-9]{8}) (reregister|full)$') {
                    $validExe = $matches[2] -ceq $principalSid -and
                        $matches[3] -ceq $name -and
                        $matches[1].Substring(0,8) -ceq $name.Substring($name.Length-8)
                }
                $validPs1 = $false
                if ($path -match '(?i)\\powershell\.exe$' -and
                    $arguments.Contains('-File "' + $expectedPs1 + '"') -and
                    $arguments.Contains('-ExpectedSid ' + $principalSid) -and
                    $arguments.EndsWith('-TaskName ' + $name) -and
                    $arguments -cmatch '-RunId ([a-f0-9]{32})') {
                    $validPs1 = $matches[1].Substring(0,8) -ceq $name.Substring($name.Length-8)
                }
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
                $lastCode = [int]$t.LastTaskResult
                $completed = -not $isRunning -and $hasRun -and $lastCode -eq 0
                $attention = -not $isRunning -and $hasRun -and $lastCode -ne 0
                $kind = if ($isRunning) { 'Running' }
                    elseif ($attention) { 'Attention' }
                    elseif ($completed) { 'Completed' }
                    elseif ($expired) { 'Expired' }
                    else { 'Pending' }
                $workerMode = if ($validPs1) { 'PowerShell' } else { 'EXE' }
                $workerAction = if ($validPs1) { 'reregister' }
                    elseif ($arguments.EndsWith(' full')) { 'full' }
                    else { 'reregister' }
                $owned += [pscustomobject]@{
                    Name = $name; UserSid = $principalSid; Kind = $kind
                    Mode = $workerMode; Action = $workerAction; LastResult = $lastCode
                }
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
                Attention = @($owned | Where-Object { $_.Kind -eq 'Attention' }).Count
                Foreign = $foreign
            } | ConvertTo-Json -Compress
            """);

    private static string ToStatusJsonScript() => BaseScript + """
            
            [pscustomobject]@{
                Tasks = @($owned | ForEach-Object {
                    [pscustomobject]@{
                        Name = $_.Name; UserSid = $_.UserSid; Kind = $_.Kind
                        Mode = $_.Mode; Action = $_.Action; LastResult = $_.LastResult
                    }
                })
            } | ConvertTo-Json -Compress -Depth 5
            """;

    // Read-only: same strict task-name, principal and action checks as cleanup.
    // No repair, task deletion or WAM action can be triggered from this query.
    internal static IReadOnlyList<ScheduledTaskRuntime> InspectTaskStates()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator access required to inspect all-user tasks.");
        using var json = Execute(ToStatusJsonScript(), maxLength: 262144);
        var tasks = json.RootElement.GetProperty("Tasks");
        var results = new List<ScheduledTaskRuntime>();
        foreach (var item in tasks.EnumerateArray())
        {
            var name = item.GetProperty("Name").GetString() ?? "";
            var sid = item.GetProperty("UserSid").GetString() ?? "";
            var kind = item.GetProperty("Kind").GetString() ?? "";
            var mode = item.GetProperty("Mode").GetString() ?? "";
            var action = item.GetProperty("Action").GetString() ?? "";
            var code = item.GetProperty("LastResult").GetInt32();
            if (name.Length < 30 || sid.Length < 15 ||
                kind is not ("Pending" or "Running" or "Completed" or "Expired" or "Attention") ||
                mode is not ("EXE" or "PowerShell") ||
                action is not ("reregister" or "full"))
                continue;
            results.Add(new ScheduledTaskRuntime(name, sid, kind, mode, action, code));
        }
        return results;
    }

    private static JsonDocument Execute(string script, int maxLength = 8192)
    {
        var response = ExternalCommand.RunPowerShell(script, 35);
        if (response.ExitCode != 0)
            throw new IOException("Could not inspect our scheduled tasks (exit code " + response.ExitCode + ").");
        var json = response.StdOut.Trim();
        if (json.Length == 0 || json.Length > maxLength)
            throw new IOException("Unexpected Task Scheduler response.");
        return JsonDocument.Parse(json);
    }

    private static bool ExistsSafe(string path)
    {
        // The parent is a fixed, administrator-owned ProgramData location.
        // Never follow a junction/symlink when deleting shared workers.
        if (Directory.Exists(Root) &&
            (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Protected worker folder is a reparse point. Cleanup denied.");
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
            Int("Running"), Int("Completed"), Int("Expired"), Int("Attention"), Int("Foreign"),
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
            " pending, " + after.Running + " running, " + after.Attention +
            " attention, " + after.Foreign + " other).";
    }

    internal static bool SelfTest()
    {
        var safe = new ScheduledWorkerSnapshot(0, 0, 0, 0, 0, 0, 0, true, true);
        var waiting = safe with { Total = 1, Pending = 1 };
        var running = safe with { Total = 1, Running = 1 };
        return safe.CanRemoveWorkers && !waiting.CanRemoveWorkers &&
            !running.CanRemoveWorkers && waiting.Removable == 0 &&
            (safe with { Total = 1, Attention = 1 }).Removable == 0 &&
            (safe with { Total = 2, Completed = 1, Expired = 1 }).Removable == 2 &&
            Ps1.EndsWith(@"Microsoft365AuthRepair\WamReregisterWorker.ps1",
                StringComparison.OrdinalIgnoreCase) &&
            Exe.EndsWith(@"Microsoft365AuthRepair\Microsoft365AuthRepair.exe",
                StringComparison.OrdinalIgnoreCase) &&
            !ToJsonScript(false).Contains("DeleteTask", StringComparison.Ordinal) &&
            ToJsonScript(true).Contains("DeleteTask", StringComparison.Ordinal);
    }
}
