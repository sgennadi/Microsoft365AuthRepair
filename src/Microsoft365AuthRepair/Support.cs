using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

internal static class Identity
{
    internal static WindowsIdentity Current() => WindowsIdentity.GetCurrent();

    internal static bool IsElevated()
    {
        using var identity = Current();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static string CurrentSid()
    {
        using var identity = Current();
        return identity.User?.Value ?? throw new InvalidOperationException("Current user SID could not be determined.");
    }

    internal static int SessionId() => Process.GetCurrentProcess().SessionId;

    internal static void RequireInteractiveUser()
    {
        using var identity = Current();
        if (identity.IsSystem || SessionId() == 0)
            throw new InvalidOperationException("Full reset requires an interactive user session; SYSTEM/session 0 is unsupported.");
    }
}

internal sealed class RepairLog : IDisposable
{
    private readonly StreamWriter writer;
    private readonly bool quiet;
    private int warnings;
    private int errors;

    internal int ErrorCount => errors;
    internal int WarningCount => warnings;
    internal string PathName { get; }

    internal RepairLog(bool quiet)
    {
        this.quiet = quiet;
        var root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft365AuthRepair", "Logs");
        Directory.CreateDirectory(root);
        PathName = System.IO.Path.Combine(root, $"repair_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(PathName, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }

    internal void Info(string message) => Write("INFO", message);
    internal void Ok(string message) => Write("OK", message);
    internal void Warn(string message) { warnings++; Write("WARN", message); }
    internal void Error(string message) { errors++; Write("ERROR", message); }

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        writer.WriteLine(line);
        if (!quiet) Console.WriteLine(line);
    }

    public void Dispose() => writer.Dispose();
}

internal static class ExternalCommand
{
    internal sealed record Result(int ExitCode, string StdOut, string StdErr);

    internal static Result Run(string exe, IEnumerable<string> args, int timeoutSeconds = 180)
    {
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException($"Failed to start {System.IO.Path.GetFileName(exe)}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{System.IO.Path.GetFileName(exe)} exceeded the operation timeout.");
        }
        return new Result(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    internal static string PowerShellPath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

    internal static Result RunPowerShell(string script, int timeoutSeconds = 180)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return Run(PowerShellPath(), ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], timeoutSeconds);
    }
}

internal sealed record WindowsProfile(string Sid, string Account, string DirectoryPath);

internal static class UserProfiles
{
    private static readonly Regex RealUserSid = new(@"^S-1-5-21-(?:\d+-){3}\d+$", RegexOptions.CultureInvariant);

    internal static List<WindowsProfile> Enumerate()
    {
        var result = new List<WindowsProfile>();
        using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
        if (root is null) throw new IOException("Windows ProfileList registry key not found.");
        foreach (var sid in root.GetSubKeyNames())
        {
            if (!RealUserSid.IsMatch(sid)) continue;
            using var key = root.OpenSubKey(sid);
            var raw = key?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var path = Environment.ExpandEnvironmentVariables(raw);
            try
            {
                if (!Directory.Exists(path)) continue;
                var item = File.GetAttributes(path);
                if ((item & FileAttributes.ReparsePoint) != 0) continue;
                var account = sid;
                try { account = new SecurityIdentifier(sid).Translate(typeof(NTAccount)).Value; }
                catch { /* Disconnected domain or stale SID: keep SID for scheduling. */ }
                result.Add(new WindowsProfile(sid, account, System.IO.Path.GetFullPath(path)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return result.OrderBy(profile => profile.Account, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
