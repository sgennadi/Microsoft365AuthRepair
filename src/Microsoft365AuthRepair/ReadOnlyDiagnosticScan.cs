using System.Threading;

namespace Microsoft365AuthRepair;

// The four probes are READ ONLY. Windows WMI and PowerShell may hang outside
// our process. A bounded UI wait prevents one slow provider hiding every other
// finished result. On timeout pending reads may finish in the background;
// they never mutate Office, TPM, WAM or identity state.
internal sealed record ReadOnlyDiagnosticResult(
    string WamReport,
    OfficeLicenseSnapshot? OfficeLicense,
    OfficeActivationHealth? OfficeActivation,
    RepairPreflight? Preflight,
    TpmBitLockerSnapshot DeviceProtection,
    string[] UnverifiedSections,
    bool ReachedDeadline);

internal sealed record DiagnosticSectionProgress(int Completed, int Total, string LastCompleted, bool Verified);

internal static class ReadOnlyDiagnosticScan
{
    internal const int MaxWaitSeconds = 60;
    internal const int TotalSections = 4;

    private sealed record OfficeResult(OfficeLicenseSnapshot? License,
        OfficeActivationHealth? Activation);

    private static readonly object Sync = new();
    private static Task<string?>? wamInFlight;
    private static Task<OfficeResult?>? officeInFlight;


    // A timed-out WMI query may remain in a native Windows provider. Reuse
    // the pending task rather than endlessly spawning additional queries.
    private static Task<T?> SingleFlight<T>(ref Task<T?>? slot, Func<T> read)
        where T : class
    {
        lock (Sync)
        {
            if (slot is not null && !slot.IsCompleted)
                return slot;
            slot = Task.Run(() =>
            {
                try { return read(); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Exceptions and raw diagnostic strings must not be
                    // forwarded to the UI or the anonymous report.
                    return null;
                }
            });
            return slot;
        }
    }

    private static Task<string?> StartWam() =>
        SingleFlight(ref wamInFlight, Repair.DiagnoseReport);

    private static Task<OfficeResult?> StartOffice(bool currentUser) =>
        SingleFlight(ref officeInFlight, () =>
        {
            var license = OfficeLicenseDiagnostics.Capture(currentUser);
            OfficeActivationHealth? activation = null;
            try { activation = OfficeActivationProbe.Capture(license, currentUser); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { }
            return new OfficeResult(license, activation);
        });

    private static T? Successful<T>(Task<T?> task) where T : class =>
        task.IsCompletedSuccessfully ? task.Result : null;

    private static Task Track<T>(Task<T?> task, Action<bool> completed)
        where T : class
    {
        // Never confuse a completed but failed provider with verified data.
        // Result is only inspected on a successfully completed Task.
        return task.ContinueWith(t =>
            completed(t.IsCompletedSuccessfully && t.Result is not null),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal static async Task<ReadOnlyDiagnosticResult> RunAsync(
        bool currentDesktopUser,
        IProgress<DiagnosticSectionProgress>? progress,
        CancellationToken cancellationToken = default,
        IProgress<DeviceProbeProgress>? deviceProgress = null,
        TimeSpan? maxWait = null)
    {
        var wait = maxWait ?? TimeSpan.FromSeconds(MaxWaitSeconds);
        if (wait <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxWait));

        var completed = 0;
        // A native WMI provider can remain blocked indefinitely. Holding a
        // strong reference to Progress<T> would also pin the closed WinForms
        // window for the lifetime of that provider query.
        var progressWeak = progress is null ? null :
            new WeakReference<IProgress<DiagnosticSectionProgress>>(progress);
        void Finished(string section, bool verified)
        {
            int n = Interlocked.Increment(ref completed);
            if (progressWeak?.TryGetTarget(out var sink) == true)
                sink.Report(new(n, TotalSections, section, verified));
        }

        // Start independent components concurrently. TPM, BitLocker, Device
        // Guard and RWDC are isolated in individually timed subprocesses, so
        // one blocked WMI provider does not consume the global 60-second budget.
        var wam = StartWam();
        var office = StartOffice(currentDesktopUser);
        var tpm = IsolatedDeviceProbe.CaptureAsync(deviceProgress,
            cancellationToken);
        var network = Task.Run(async () =>
        {
            try
            {
                return await RepairPreflightProbe.CollectAsync(
                    currentDesktopUser, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
        });
        var completion = Task.WhenAll(
            Track(wam, ok => Finished("WAM_ENTRA", ok)),
            Track(office, ok => Finished("OFFICE_LICENSE", ok)),
            Track(tpm, ok => Finished("TPM_BITLOCKER",
                ok && tpm.Result.UnverifiedParts.Length == 0)),
            Track(network, ok => Finished("NETWORK_PREFLIGHT", ok)));

        bool deadlineReached = false;
        try
        {
            await completion.WaitAsync(wait, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException) { deadlineReached = true; }
        // Explicit cancel must leave the UI without publishing guessed results.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        var wamReport = Successful(wam);
        var officeInfo = Successful(office);
        var deviceInfo = tpm.IsCompletedSuccessfully ? tpm.Result : null;
        var networkInfo = Successful(network);
        var missing = new List<string>();
        if (wamReport is null) missing.Add("WAM_ENTRA");
        if (officeInfo is null) missing.Add("OFFICE_LICENSE");
        if (deviceInfo is null) missing.Add("TPM_BITLOCKER");
        else missing.AddRange(deviceInfo.UnverifiedParts);
        if (networkInfo is null) missing.Add("NETWORK_PREFLIGHT");

        // Never pretend that missing dsregcmd or a failed WMI query confirms
        // the device is healthy. The baseline intentionally has no Good flags.
        wamReport ??= "BrokerPlugin: UNKNOWN\nAzureAdJoined: UNKNOWN\n" +
            "DeviceAuthStatus: UNKNOWN\nAzureAdPrt: UNKNOWN\nWamDefaultSet: UNKNOWN";

        return new(wamReport, officeInfo?.License, officeInfo?.Activation,
            networkInfo, deviceInfo?.Snapshot ?? TpmBitLockerSnapshot.Unknown,
            missing.ToArray(), deadlineReached);
    }

    internal static bool SelfTest()
    {
        var good = Task.FromResult<string?>("OK");
        var pending = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ReadOnlyDiagnosticResult("BrokerPlugin: UNKNOWN",
            null, null, null, TpmBitLockerSnapshot.Unknown,
            ["OFFICE_LICENSE"], true);
        return Successful(good) == "OK" && Successful(pending.Task) is null &&
            IsolatedDeviceProbe.SelfTest() &&
            TotalSections == 4 && MaxWaitSeconds == 60 &&
            source.ReachedDeadline && source.UnverifiedSections.Length == 1 &&
            source.WamReport.Contains("UNKNOWN", StringComparison.Ordinal);
    }
}
