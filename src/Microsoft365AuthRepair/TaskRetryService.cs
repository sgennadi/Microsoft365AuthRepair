namespace Microsoft365AuthRepair;

// Explicit and narrowly scoped retry: only a failed WAM re-registration,
// never a full Office identity reset. Always queue for next sign-in, not now.
internal static class TaskRetryService
{
    internal static bool CanRetry(TaskStatusRow? row) =>
        row is { State: "WorkerReportedFailure", Action: "reregister" } &&
        !string.IsNullOrEmpty(row.TaskName) && !string.IsNullOrEmpty(row.UserSid);

    internal static string QueueAfterApproval(TaskStatusRow selection)
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator required for selected task retry.");
        if (!CanRetry(selection))
            throw new InvalidOperationException("Only verified failed WAM registration can be retried.");

        // Re-read current task and worker receipts after the human confirmation.
        // A changed/deleted/stale selection must never silently enqueue work.
        var latest = TaskRunHistory.Snapshot().FirstOrDefault(x =>
            x.TaskName == selection.TaskName &&
            string.Equals(x.UserSid, selection.UserSid, StringComparison.OrdinalIgnoreCase));
        if (!CanRetry(latest) || latest?.State != selection.State)
            throw new InvalidOperationException("The task status changed. Refresh before retrying.");

        var profile = UserProfiles.Enumerate().SingleOrDefault(x =>
            x.Sid.Equals(selection.UserSid, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
            throw new InvalidOperationException("The original Windows profile no longer exists.");
        var existing = ScheduledWorkerMaintenance.InspectTaskStates();
        if (existing.Any(x => x.UserSid.Equals(profile.Sid, StringComparison.OrdinalIgnoreCase) &&
            x.Kind is "Pending" or "Running"))
            throw new InvalidOperationException("Another task is already queued or running for this user.");

        // No unattended launch or credentials. The affected user will execute
        // this trusted EXE with their own interactive Windows logon token.
        Scheduling.PrepareProtectedExe();
        return Scheduling.QueueForSid(profile, userOnline: false, "reregister",
            ScheduledWorkerMode.Exe, scheduleOnly: true);
    }

    internal static bool SelfTest()
    {
        var valid = new TaskStatusRow("User", "reregister", "EXE",
            DateTimeOffset.UtcNow, "WorkerReportedFailure", "worker failed")
        { UserSid = "S-1-5-21-1-2-3-1001", TaskName = "WAM_S_1_5_21_1_2_3_1001_abcdef01" };
        return CanRetry(valid) &&
               !CanRetry(valid with { State = "Queued" }) &&
               !CanRetry(valid with { State = "AttemptedUnconfirmed" }) &&
               !CanRetry(valid with { Action = "full" }) &&
               !CanRetry(valid with { TaskName = null });
    }
}
