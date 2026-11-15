using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft365AuthRepair;

// Optional one-shot cleanup of a user's portable copy. Never touches the
// protected per-machine worker, another user's files, or a shared install.
// The helper runs unelevated and waits for the original EXE to terminate.
internal static class PortableSelfCleanup
{
    private const string ExecutableName = "Microsoft365AuthRepair.exe";
    private const string HelperName = "Microsoft365AuthRepair.Cleanup.exe";
    private sealed class Ticket
    {
        public int ParentPid { get; set; }
        public string Target { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string UserSid { get; set; } = "";
    }

    internal static bool IsEligible()
    {
        if (Identity.IsElevated() || !OperatingSystem.IsWindows()) return false;
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
            !Path.GetFileName(path).Equals(ExecutableName, StringComparison.OrdinalIgnoreCase))
            return false;
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var own = Path.GetFullPath(profile).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var shared = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft365AuthRepair")).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var regularInstall = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Microsoft365AuthRepair")).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!full.StartsWith(own, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(shared, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(regularInstall, StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal static void Queue()
    {
        if (!IsEligible()) throw new InvalidOperationException(
            "Automatic removal is restricted to a standard user's own portable EXE.");
        var origin = Path.GetFullPath(Environment.ProcessPath!);
        // No arbitrary path or command is accepted from the GUI.
        var root = Path.Combine(Path.GetTempPath(), "Microsoft365AuthRepair-Cleanup",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var helper = Path.Combine(root, HelperName);
        var ticket = Path.Combine(root, "cleanup.json");
        try
        {
            File.Copy(origin, helper, overwrite: false);
            var hash = Hash(origin);
            if (Hash(helper) != hash)
                throw new IOException("Cleanup helper copy changed.");
            File.WriteAllText(ticket, JsonSerializer.Serialize(new Ticket
            {
                ParentPid = Environment.ProcessId, Target = origin,
                Sha256 = hash, UserSid = Identity.CurrentSid()
            }));
            var start = new ProcessStartInfo(helper)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = root
            };
            start.ArgumentList.Add("--cleanup-helper");
            start.ArgumentList.Add(ticket);
            using var child = Process.Start(start) ??
                throw new IOException("Could not start cleanup helper.");
        }
        catch
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            throw;
        }
    }

    internal static int RunHelper(string ticketFile)
    {
        try
        {
            if (Identity.IsElevated()) return 2;
            var path = Path.GetFullPath(ticketFile);
            var self = Path.GetFullPath(Environment.ProcessPath ?? "");
            if (Path.GetFileName(self) != HelperName ||
                Path.GetDirectoryName(path) != Path.GetDirectoryName(self) ||
                Path.GetFileName(path) != "cleanup.json" ||
                (File.GetAttributes(self) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return 2;
            var ticket = JsonSerializer.Deserialize<Ticket>(File.ReadAllText(path));
            if (ticket is null || ticket.ParentPid < 1 ||
                !ticket.UserSid.Equals(Identity.CurrentSid(), StringComparison.OrdinalIgnoreCase))
                return 2;
            var target = Path.GetFullPath(ticket.Target);
            if (!Path.GetFileName(target).Equals(ExecutableName, StringComparison.OrdinalIgnoreCase) ||
                !target.StartsWith(Path.GetFullPath(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return 2;

            try
            {
                using var parent = Process.GetProcessById(ticket.ParentPid);
                if (!parent.WaitForExit(60000)) return 2;
            }
            catch (ArgumentException) { /* parent exited */ }
            if (!File.Exists(target) || Hash(target) != ticket.Sha256 ||
                (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                return 2;
            File.Delete(target);
            return 0;
        }
        catch { return 2; }
    }

    internal static bool SelfTest() =>
        ExecutableName.EndsWith(".exe", StringComparison.Ordinal) &&
        HelperName != ExecutableName;
}
