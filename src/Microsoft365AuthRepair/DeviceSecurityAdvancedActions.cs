using System.Diagnostics;
using System.Management;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Administrator-only, opt-in operations. The regular Check & Diagnose path
// NEVER calls any member of this class. Recovery passwords are never returned
// to GUI fields, clipboard, command-line arguments, activity or reports.
internal enum BitLockerAdminAction { Decrypt, SuspendOneRestart, Resume }

internal static class DeviceSecurityAdvancedActions
{
    private const string Namespace = @"root\CIMV2\Security\MicrosoftVolumeEncryption";
    private static readonly Regex RecoveryPasswordShape = new(
        @"^\d{6}(?:-\d{6}){7}$", RegexOptions.CultureInvariant);

    internal static bool IsRecoveryPasswordShape(string? value) =>
        value is not null && RecoveryPasswordShape.IsMatch(value);

    internal static string? SystemDrive()
    {
        string? raw = Environment.GetEnvironmentVariable("SystemDrive");
        if (TpmBitLockerProbe.SystemVolumeQuery(raw, protection: true) is null)
            return null;
        return char.ToUpperInvariant(raw![0]) + ":";
    }

    private static void RequireAdministrator()
    {
        if (!Identity.IsElevated())
            throw new UnauthorizedAccessException("Administrator rights required.");
        Identity.RequireInteractiveUser();
    }

    internal static bool MayExportTo(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename) ||
            !Path.IsPathFullyQualified(filename) ||
            filename.StartsWith(@"\\", StringComparison.Ordinal) ||
            !filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory) ||
                File.Exists(filename)) return false;
            // A nested junction could silently redirect an apparently local
            // NTFS path into a synchronized or remote location.
            for (var cursor = directory; !string.IsNullOrWhiteSpace(cursor);)
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    return false;
                var parent = Path.GetDirectoryName(cursor.TrimEnd('\\'));
                if (string.IsNullOrEmpty(parent) ||
                    string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase))
                    break;
                cursor = parent;
            }
            // Refuse common cloud-synchronized roots even if the underlying
            // filesystem is local NTFS. A recovery password must not be
            // unintentionally synced into OneDrive/Dropbox/Google Drive/iCloud.
            var absolute = Path.GetFullPath(filename);
            foreach (var name in new[] { "OneDrive", "OneDriveCommercial",
                         "OneDriveConsumer", "Dropbox", "GoogleDrive", "iCloudDrive" })
            {
                var sync = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(sync) &&
                    absolute.StartsWith(Path.GetFullPath(sync).TrimEnd('\\') + "\\",
                        StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (absolute.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Google Drive", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("iCloudDrive", StringComparison.OrdinalIgnoreCase)))
                return false;
            var root = Path.GetPathRoot(directory);
            if (string.IsNullOrWhiteSpace(root)) return false;
            var drive = new DriveInfo(root);
            return drive.DriveType == DriveType.Fixed &&
                drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or
            System.Security.SecurityException)
        { return false; }
    }

    internal static int ExportSystemRecoveryPasswords(string file)
    {
        RequireAdministrator();
        if (!MayExportTo(file))
            throw new InvalidOperationException("Use a NEW .txt file on a local NTFS drive.");
        var drive = SystemDrive() ??
            throw new InvalidOperationException("Windows system volume not verified.");
        var query = TpmBitLockerProbe.SystemVolumeQuery(drive, protection: false)
            ?? throw new InvalidOperationException("Invalid system volume.");
        var passwords = new List<string>();
        // No other volume, external key, TPM secret or key protector is read.
        using (var searcher = new ManagementObjectSearcher(Namespace, query))
        using (var volumes = searcher.Get())
        {
            foreach (ManagementObject item in volumes)
            {
                using (item)
                {
                    if (!string.Equals(item["DriveLetter"] as string, drive,
                            StringComparison.OrdinalIgnoreCase)) continue;
                    using var input = item.GetMethodParameters("GetKeyProtectors");
                    if (input is null)
                        throw new IOException("Recovery protector query unavailable.");
                    input["KeyProtectorType"] = 3u; // Numerical recovery only
                    using var output = item.InvokeMethod("GetKeyProtectors", input, null);
                    if (output is null ||
                        Convert.ToUInt32(output["ReturnValue"]) != 0u)
                        throw new IOException("Recovery protector query unavailable.");
                    if (output["VolumeKeyProtectorID"] is not string[] ids)
                        break;
                    foreach (var id in ids)
                    {
                        using var pwInput = item.GetMethodParameters(
                            "GetKeyProtectorNumericalPassword");
                        if (pwInput is null)
                            throw new IOException("Recovery password query unavailable.");
                        pwInput["VolumeKeyProtectorID"] = id;
                        using var pwOutput = item.InvokeMethod(
                            "GetKeyProtectorNumericalPassword", pwInput, null);
                        if (pwOutput is null ||
                            Convert.ToUInt32(pwOutput["ReturnValue"]) != 0u ||
                            pwOutput["NumericalPassword"] is not string password ||
                            !IsRecoveryPasswordShape(password))
                            throw new IOException("Recovery password query unavailable.");
                        passwords.Add(password);
                    }
                    break;
                }
            }
        }
        if (passwords.Count == 0)
            throw new InvalidOperationException("No numerical recovery password found. Nothing exported.");

        // Restrict file ACLs *before* writing any secret bytes. Refuse
        // overwrites and non-NTFS/nonlocal destinations. The plaintext file
        // remains solely under the administrator-chosen location.
        bool created = false;
        try
        {
            // FileInfo.SetAccessControl needs a second Windows handle. Permit
            // security-descriptor updates while the file is still EMPTY;
            // restrict its DACL before writing a single secret byte.
            using var stream = new FileStream(file, FileMode.CreateNew,
                FileAccess.Write, FileShare.ReadWrite | FileShare.Delete,
                4096, FileOptions.WriteThrough);
            created = true;
            var security = new FileSecurity();
            var user = WindowsIdentity.GetCurrent().User ??
                throw new UnauthorizedAccessException("Cannot determine file owner.");
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true,
                preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user,
                FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(file).SetAccessControl(security);
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024,
                leaveOpen: true))
            {
                writer.WriteLine("BitLocker OS-volume recovery password backup");
                writer.WriteLine("SENSITIVE: Anyone with this recovery password may unlock the volume.");
                writer.WriteLine("Store offline; do not email, sync, screenshot or share this text.");
                writer.WriteLine("Volume: " + drive);
                writer.WriteLine("Captured UTC: " +
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",
                        System.Globalization.CultureInfo.InvariantCulture));
                foreach (var password in passwords)
                    writer.WriteLine("Recovery password: " + password);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            return passwords.Count;
        }
        catch
        {
            // If ACL enforcement or the write fails, remove incomplete data.
            if (created)
                try { File.Delete(file); } catch (IOException) { }
            throw;
        }
        finally
        {
            // Do not retain recovery passwords on a form or in any report.
            passwords.Clear();
        }
    }

    internal static string[] ArgumentsFor(BitLockerAdminAction action, string drive) =>
        action switch
        {
            BitLockerAdminAction.Decrypt => ["-off", drive],
            BitLockerAdminAction.SuspendOneRestart =>
                ["-protectors", "-disable", drive, "-RebootCount", "1"],
            BitLockerAdminAction.Resume => ["-protectors", "-enable", drive],
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    internal static async Task<bool> RunBitLockerActionAsync(
        BitLockerAdminAction action)
    {
        RequireAdministrator();
        var drive = SystemDrive() ??
            throw new InvalidOperationException("System drive not available.");
        var exe = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows), "System32", "manage-bde.exe");
        if (!File.Exists(exe))
            throw new IOException("The Windows BitLocker management tool is unavailable.");
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = false, RedirectStandardError = false
        };
        foreach (string arg in ArgumentsFor(action, drive))
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ??
            throw new IOException("Cannot launch BitLocker management.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            // A timed-out command may already have changed protection.
            throw new TimeoutException("Command status unknown; check Windows BitLocker settings.");
        }
        return process.ExitCode == 0;
    }

    internal static void OpenBitLockerManagement() =>
        Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32", "control.exe"),
            "/name Microsoft.BitLockerDriveEncryption")
        { UseShellExecute = true });

    internal static void OpenTpmManagement()
    {
        RequireAdministrator();
        // Intentional: no Clear-Tpm, no Win32_Tpm.Clear and no automatic
        // owner/protector changes. User must invoke Clear in Windows TPM UI.
        Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32", "tpm.msc"))
        { UseShellExecute = true });
    }

    internal static bool SelfTest()
    {
        var x = ArgumentsFor(BitLockerAdminAction.Decrypt, "C:");
        var y = ArgumentsFor(BitLockerAdminAction.SuspendOneRestart, "C:");
        var z = ArgumentsFor(BitLockerAdminAction.Resume, "C:");
        return x.SequenceEqual(["-off", "C:"]) &&
            y.SequenceEqual(["-protectors", "-disable", "C:", "-RebootCount", "1"]) &&
            z.SequenceEqual(["-protectors", "-enable", "C:"]) &&
            IsRecoveryPasswordShape("111111-222222-333333-444444-555555-666666-777777-888888") &&
            !IsRecoveryPasswordShape("anything-secret") &&
            !MayExportTo(@"\\untrusted\share\recovery.txt") &&
            !MayExportTo(@"C:\secret.dat") &&
            !MayExportTo(@"C:\Users\Person\OneDrive\recovery.txt");
    }
}
