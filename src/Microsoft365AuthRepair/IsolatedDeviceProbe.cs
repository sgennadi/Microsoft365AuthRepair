using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Microsoft365AuthRepair;

// Each fixed read-only provider is isolated in a short-lived child process.
// A stalled WMI/TPM provider is not permitted to block or pile up managed UI
// threads. All output contains only whitelisted classifications. No recovery
// keys, device identifiers, user identities, registry values or raw WMI data.
internal sealed record DeviceProbeProgress(string Part, string State);
internal sealed record DeviceProbeRun(TpmBitLockerSnapshot Snapshot,
    string[] UnverifiedParts);

internal static class IsolatedDeviceProbe
{
    private sealed record Wire(string Part, TpmBitLockerSnapshot Snapshot);
    private sealed record Result(string Part, string State, TpmBitLockerSnapshot? Snapshot);

    internal const int PartTimeoutSeconds = 10;
    internal const int MaxParallel = 3;

    internal static int RunReadOnlyWorker(string part)
    {
        if (!TpmBitLockerProbe.PartNames.Contains(part, StringComparer.Ordinal))
            return 2;
        try
        {
            var snapshot = TpmBitLockerProbe.CapturePart(part);
            var output = JsonSerializer.Serialize(new Wire(part, snapshot));
            // The WinExe subsystem may have no console attached. Its standard
            // output is explicitly redirected by the requesting process.
            using var writer = new StreamWriter(Console.OpenStandardOutput(),
                new UTF8Encoding(false), 1024, leaveOpen: true);
            writer.Write(output);
            writer.Flush();
            return 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Never print an exception: paths and device data may appear there.
            return 3;
        }
    }

    private static TpmBitLockerSnapshot? Parse(string part, string output)
    {
        if (output.Length is < 10 or > 16384) return null;
        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(output);
            if (wire?.Part != part || wire.Snapshot is null) return null;
            var state = wire.Snapshot;
            // A child is our own signed-by-release app. Nonetheless, validate
            // the narrow set of fields that can enter the parent process.
            return part switch
            {
                "TPM_WMI" when state.TpmPresent is "YES" or "NO" or "UNKNOWN" &&
                    state.TpmVersion is "1.2" or "2.0" or "UNKNOWN" =>
                    state with
                    {
                        TpmManufacturer = state.TpmManufacturer is "STM" or "MSFT" or
                            "OTHER" or "UNKNOWN" ? state.TpmManufacturer : "UNKNOWN",
                        TpmFirmwareVersion = TpmBitLockerProbe.FirmwareVersion(
                            state.TpmFirmwareVersion)
                    },
                "BITLOCKER_WMI" when state.OsVolumeProtection is "ON" or "OFF" or "UNKNOWN" &&
                    state.OsVolumeConversion is "FULLY_DECRYPTED" or
                        "FULLY_ENCRYPTED" or "ENCRYPTING" or "DECRYPTING" or
                        "ENCRYPTION_PAUSED" or "DECRYPTION_PAUSED" or "UNKNOWN" => state,
                "DEVICE_GUARD_WMI" when state.MemoryIntegrityRuntime is
                    "RUNNING" or "NOT_RUNNING" or "UNKNOWN" &&
                    state.MemoryIntegrityConfigured is "ON" or "OFF" or
                        "NOT_SET" or "UNKNOWN" => state,
                "DOMAIN_RWDC" when state.DomainJoinStatus is "YES" or "NO" or
                    "UNKNOWN" && state.WritableDcStatus is "AVAILABLE" or
                    "NOT_FOUND" or "UNKNOWN" or "NOT_APPLICABLE" => state,
                "TPM_LOCKOUT" when state.TpmLockedOut is "YES" or "NO" or
                    "UNKNOWN" => state,
                "DPAPI_ATTESTATION" when state.DpapiProtectionPolicy is
                    "NOT_SET" or "ZERO" or "ONE" or "OTHER" or "UNKNOWN" &&
                    state.WindowsRelease is "WIN11_24H2" or "WIN11_25H2" or
                        "WIN11_NEWER" or "OTHER" or "UNKNOWN" &&
                    state.AikEnrollmentError is "NOT_FOUND" or "TIMEOUT" or
                        "INVALID_PUBLIC_KEY" or "SERVER_ERROR" or "OTHER_ERROR" or
                        "NONE_RECORDED" or "UNKNOWN" => state,
                _ => null
            };
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or
            ArgumentException or InvalidOperationException)
        { return null; }
    }

    private static async Task<Result> ReadPartAsync(
        string part, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(PartTimeoutSeconds));
        Process? process = null;
        try
        {
            string executable = Environment.ProcessPath ??
                throw new InvalidOperationException("Executable path unknown.");
            // Only predeclared subcommands are accepted. The child does no
            // registration, network authentication or credential modification.
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false
            };
            start.ArgumentList.Add("--read-only-device-probe");
            start.ArgumentList.Add(part);
            process = new Process { StartInfo = start };
            if (!process.Start()) return new(part, "UNAVAILABLE", null);
            var output = process.StandardOutput.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            // Bound inherited output handles independently of process exit.
            var value = await output.WaitAsync(TimeSpan.FromSeconds(2),
                limit.Token).ConfigureAwait(false);
            var parsed = process.ExitCode == 0 ? Parse(part, value) : null;
            return parsed is null ||
                !TpmBitLockerProbe.HasPartEvidence(part, parsed)
                ? new(part, "UNAVAILABLE", parsed)
                : new(part, "FINISHED", parsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            return new(part, "TIME_LIMIT", null);
        }
        catch (TimeoutException)
        {
            return new(part, "TIME_LIMIT", null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException or
            System.Security.SecurityException or ArgumentException)
        {
            return new(part, "UNAVAILABLE", null);
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or
                    System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
        }
    }

    internal static async Task<DeviceProbeRun> CaptureAsync(
        IProgress<DeviceProbeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var gate = new SemaphoreSlim(MaxParallel);
        try
        {
            async Task<Result> One(string part)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    progress?.Report(new(part, "STARTED"));
                    var result = await ReadPartAsync(part, cancellationToken)
                        .ConfigureAwait(false);
                    progress?.Report(new(part, result.State));
                    return result;
                }
                finally { gate.Release(); }
            }
            var all = await Task.WhenAll(TpmBitLockerProbe.PartNames
                .Select(One)).ConfigureAwait(false);
            var result = TpmBitLockerSnapshot.Unknown;
            var unknown = new List<string>();
            foreach (var item in all)
            {
                if (item.Snapshot is not null)
                    result = TpmBitLockerProbe.MergePart(
                        result, item.Part, item.Snapshot);
                if (item.State != "FINISHED") unknown.Add(item.Part);
            }
            if (result.TpmPresent == "NO")
                result = result with { TpmLockedOut = "NOT_APPLICABLE" };
            return new(result, unknown.ToArray());
        }
        finally { gate.Dispose(); }
    }

    internal static bool SelfTest()
    {
        var invalid = Parse("TPM_WMI", "NOT_A_JSON");
        var unknown = TpmBitLockerSnapshot.Unknown;
        var probe = new Wire("TPM_LOCKOUT",
            unknown with { TpmLockedOut = "YES" });
        var parsed = Parse("TPM_LOCKOUT", JsonSerializer.Serialize(probe));
        return TpmBitLockerProbe.PartNames.Length == 6 &&
            TpmBitLockerProbe.PartNames.Distinct().Count() == 6 &&
            PartTimeoutSeconds >= 5 && PartTimeoutSeconds <= 15 &&
            MaxParallel >= 1 && MaxParallel <= 3 &&
            invalid is null && parsed?.TpmLockedOut == "YES" &&
            Parse("TPM_WMI", JsonSerializer.Serialize(probe)) is null &&
            !TpmBitLockerProbe.HasPartEvidence("TPM_WMI", unknown) &&
            TpmBitLockerProbe.HasPartEvidence("TPM_LOCKOUT", parsed);
    }
}
