using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Local, administrator-readable inventory. No tokens, keys or diagnostic payloads.
// The absence of a scheduled task is NOT evidence that the repair succeeded.
internal sealed record TrackedTaskRun(
    string TaskName, string UserSid, string RunId, string Mode, string Action,
    DateTimeOffset SubmittedUtc, bool ImmediateStartRequested);

internal sealed record TaskStatusRow(
    string User, string Action, string Mode, DateTimeOffset? SubmittedUtc,
    string State, string Evidence);

internal static class TaskRunHistory
{
    private static readonly string Root = Path.Combine(Scheduling.ProtectedRoot, "TaskHistory");
    private static readonly Regex SidFormat = new(
        @"^S-1-5-21-(?:[0-9]+-){3}[0-9]+$", RegexOptions.CultureInvariant);
    private static readonly Regex RunFormat = new(
        "^[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    private const int MaxRead = 150;

    private static bool Valid(TrackedTaskRun run) =>
        SidFormat.IsMatch(run.UserSid) &&
        RunFormat.IsMatch(run.RunId) &&
        run.TaskName == "WAM_" + run.UserSid.Replace('-', '_') + "_" + run.RunId[..8] &&
        run.Mode is "EXE" or "PowerShell" &&
        run.Action is "reregister" or "full" &&
        !(run.Mode == "PowerShell" && run.Action == "full") &&
        run.SubmittedUtc != default &&
        run.SubmittedUtc <= DateTimeOffset.UtcNow.AddMinutes(5);

    private static void RejectReparse(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing redirected status path.");
    }

    internal static void Record(TrackedTaskRun entry)
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights required for task status history.");
        if (!Valid(entry))
            throw new ArgumentException("Invalid task history record.");

        RejectReparse(Scheduling.ProtectedRoot);
        RejectReparse(Root);
        Directory.CreateDirectory(Root); // ProtectedRoot already has admin-only write ACL.
        RejectReparse(Root);

        var path = Path.Combine(Root, entry.TaskName + ".json");
        RejectReparse(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, entry);
        stream.Flush(flushToDisk: true);
    }

    private static IReadOnlyList<TrackedTaskRun> ReadHistory()
    {
        RejectReparse(Scheduling.ProtectedRoot);
        RejectReparse(Root);
        if (!Directory.Exists(Root)) return [];
        var output = new List<TrackedTaskRun>();
        foreach (var path in Directory.EnumerateFiles(Root, "WAM_*.json", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc).Take(MaxRead))
        {
            try
            {
                RejectReparse(path);
                if (new FileInfo(path).Length is < 80 or > 2048) continue;
                using var stream = File.OpenRead(path);
                var entry = JsonSerializer.Deserialize<TrackedTaskRun>(stream);
                if (entry is not null && Valid(entry) &&
                    string.Equals(Path.GetFileName(path), entry.TaskName + ".json",
                        StringComparison.Ordinal))
                    output.Add(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or JsonException or ArgumentException) { }
        }
        return output;
    }

    private static bool OrdinaryFolder(string path)
    {
        if (!Directory.Exists(path)) return false;
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    }

    private static (bool Attempted, bool Ok, bool Failed) ReadMarkers(
        WindowsProfile? profile, string runId)
    {
        if (profile is null || !RunFormat.IsMatch(runId)) return default;
        try
        {
            var directory = profile.DirectoryPath;
            if (directory.StartsWith(@"\\", StringComparison.Ordinal)) return default;
            foreach (var part in new[] { "AppData", "Local", "Microsoft365AuthRepair", "Completed" })
            {
                if (!OrdinaryFolder(directory)) return default;
                directory = Path.Combine(directory, part);
            }
            if (!OrdinaryFolder(directory)) return default;
            bool Marker(string ext)
            {
                var path = Path.Combine(directory, runId + ext);
                if (!File.Exists(path)) return false;
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            return (Marker(".attempted"), Marker(".ok"), Marker(".failed"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or System.Security.SecurityException or ArgumentException)
        { return default; }
    }

    // Deliberately conservative: Task Scheduler exit code 0 without an .ok marker
    // does not prove WAM changed, much less that Office sign-in succeeded.
    internal static string Classify(ScheduledTaskRuntime? live, bool attempted,
        bool success, bool failed)
    {
        if (success && attempted && !failed) return "WorkerReportedSuccess";
        if (failed && attempted) return "WorkerReportedFailure";
        if (live?.Kind == "Running") return "Running";
        if (live?.Kind == "Attention") return "SchedulerNeedsAttention";
        if (attempted) return "AttemptedUnconfirmed";
        return live?.Kind switch
        {
            "Pending" => "Queued",
            "Expired" => "Expired",
            "Completed" => "TaskFinishedUnconfirmed",
            _ => "Unconfirmed"
        };
    }

    private static TaskStatusRow Build(
        TrackedTaskRun entry, WindowsProfile? profile, ScheduledTaskRuntime? live)
    {
        var markers = ReadMarkers(profile, entry.RunId);
        var state = Classify(live, markers.Attempted, markers.Ok, markers.Failed);
        var evidence = state switch
        {
            "WorkerReportedSuccess" => "Worker completion marker; does not verify Office app sign-in.",
            "WorkerReportedFailure" => "Worker failure marker; inspect the user's local repair log.",
            "Running" => "Task Scheduler reports that the worker is running.",
            "Queued" => "Task registered; waiting for logon or its next trigger.",
            "Expired" => "Task expired; no successful worker receipt.",
            "SchedulerNeedsAttention" => "Task Scheduler reports a nonzero result; inspect task details.",
            "AttemptedUnconfirmed" => "Worker attempted execution, but no final receipt exists.",
            "TaskFinishedUnconfirmed" => "Scheduler reports completion, but the worker did not confirm success.",
            _ => "Task missing or no verifiable worker result. Do not assume success."
        };
        return new TaskStatusRow(profile?.Account ?? "Profile not found",
            entry.Action, entry.Mode, entry.SubmittedUtc, state, evidence);
    }

    internal static IReadOnlyList<TaskStatusRow> Snapshot()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights required to view all-user tasks.");
        var live = ScheduledWorkerMaintenance.InspectTaskStates();
        var byName = live.ToDictionary(x => x.Name, StringComparer.Ordinal);
        var profiles = UserProfiles.Enumerate().ToDictionary(
            x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var history = ReadHistory();
        var result = new List<TaskStatusRow>();
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in history.OrderByDescending(x => x.SubmittedUtc))
        {
            byName.TryGetValue(entry.TaskName, out var task);
            profiles.TryGetValue(entry.UserSid, out var profile);
            result.Add(Build(entry, profile, task));
            observed.Add(entry.TaskName);
        }
        foreach (var task in live)
        {
            // Older tasks cannot be reconciled after self-deletion because the
            // per-profile marker has only the 8-character task-name suffix.
            if (!observed.Add(task.Name)) continue;
            profiles.TryGetValue(task.UserSid, out var profile);
            var state = Classify(task, false, false, false);
            result.Add(new TaskStatusRow(profile?.Account ?? "Profile not found",
                task.Action, task.Mode, null, state,
                "Task was created before tracked history; no worker receipt is available here."));
        }
        return result.OrderByDescending(x => x.SubmittedUtc ?? DateTimeOffset.MinValue)
            .Take(100).ToArray();
    }

    internal static bool SelfTest()
    {
        var now = DateTimeOffset.UtcNow;
        const string sid = "S-1-5-21-123-456-789-1001";
        const string runId = "abcdef0123456789abcdef0123456789";
        var entry = new TrackedTaskRun("WAM_" + sid.Replace('-', '_') + "_abcdef01",
            sid, runId, "EXE", "reregister", now, false);
        var queued = new ScheduledTaskRuntime(entry.TaskName, sid, "Pending", "EXE", "reregister", 0);
        var running = queued with { Kind = "Running" };
        var attention = queued with { Kind = "Attention", LastResult = 2 };
        return Valid(entry) && !Valid(entry with { TaskName = "unrelated" }) &&
            !Valid(entry with { Mode = "PowerShell", Action = "full" }) &&
            Classify(queued, false, false, false) == "Queued" &&
            Classify(running, true, false, false) == "Running" &&
            Classify(attention, true, false, false) == "SchedulerNeedsAttention" &&
            Classify(null, true, true, false) == "WorkerReportedSuccess" &&
            Classify(null, true, false, true) == "WorkerReportedFailure" &&
            Classify(null, true, false, false) == "AttemptedUnconfirmed" &&
            Classify(null, false, false, false) == "Unconfirmed";
    }
}
