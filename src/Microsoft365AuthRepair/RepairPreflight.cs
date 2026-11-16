using System.Diagnostics;
using System.Net.Http;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

// Informational read-only preflight. Never uploads user/device data and
// never treats a network probe as an authentication test.
internal sealed record RepairPreflight(
    bool CurrentUserContext, bool? MicrosoftLoginReachable,
    bool? ClockAcceptable, bool OfficeAppsRunning, string OfficeApps,
    bool? AppxDeploymentRestricted, string NetworkStatus)
{
    internal bool BlocksFullReset =>
        !CurrentUserContext || ClockAcceptable == false || OfficeAppsRunning;
    internal bool NeedsNetworkReview => MicrosoftLoginReachable == false;
}

internal static class RepairPreflightProbe
{
    private static readonly string[] OfficeProcessNames =
        ["WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "ONENOTE", "MSACCESS",
         "MSPUB", "VISIO", "WINPROJ", "Teams", "ms-teams"];

    private static (bool, string) DetectOfficeApps()
    {
        var active = new List<string>();
        foreach (var processName in OfficeProcessNames)
        {
            try
            {
                var found = Process.GetProcessesByName(processName);
                try { if (found.Length > 0) active.Add(processName); }
                finally { foreach (var process in found) process.Dispose(); }
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.ComponentModel.Win32Exception) { }
        }
        return (active.Count > 0, string.Join(", ", active.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool? ReadAppxRestriction()
    {
        try
        {
            // An explicit enterprise app deployment restriction is only a hint:
            // it is not proof that BrokerPlugin registration is blocked.
            using var policy = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Policies\Microsoft\Windows\Appx", writable: false);
            if (policy?.GetValue("BlockNonAdminUserInstall") is int i) return i == 1;
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or System.Security.SecurityException or IOException) { return null; }
    }

    internal static async Task<RepairPreflight> CollectAsync(
        bool currentUserContext, CancellationToken cancellationToken = default)
    {
        var running = DetectOfficeApps();
        var restriction = ReadAppxRestriction();
        bool? loginOk = null, clockOk = null;
        string status = "Not checked";
        try
        {
            using var client = new HttpClient(
                new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(8) };
            using var request = new HttpRequestMessage(
                HttpMethod.Head, "https://login.microsoftonline.com/");
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            // A valid HTTPS response (including a redirect) proves transport
            // reachability only; never claim authentication succeeded.
            loginOk = true;
            status = $"HTTPS reachable (HTTP {(int)response.StatusCode}); sign-in NOT tested";
            if (response.Headers.Date is { } serverUtc)
                clockOk = Math.Abs((serverUtc - DateTimeOffset.UtcNow).TotalMinutes) <= 5;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            loginOk = false;
            status = "HTTPS check timed out; proxy/VPN/network may need attention";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   or System.Security.Authentication.AuthenticationException)
        {
            loginOk = false;
            status = "HTTPS check unavailable: " + ex.GetType().Name;
        }
        return new RepairPreflight(currentUserContext, loginOk, clockOk,
            running.Item1, running.Item2, restriction, status);
    }

    internal static bool SelfTest()
    {
        var ok = new RepairPreflight(true, true, true, false, "", null, "Reachable");
        var blocked = ok with { OfficeAppsRunning = true };
        var network = ok with { MicrosoftLoginReachable = false };
        return !ok.BlocksFullReset && !ok.NeedsNetworkReview &&
               blocked.BlocksFullReset && network.NeedsNetworkReview &&
               !(network with { MicrosoftLoginReachable = null }).NeedsNetworkReview &&
               (ok with { CurrentUserContext = false }).BlocksFullReset &&
               (ok with { ClockAcceptable = false }).BlocksFullReset;
    }
}
