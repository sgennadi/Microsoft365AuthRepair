using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniversalUpdater;

namespace Microsoft365AuthRepair;

// A self-contained, same-user helper copied from the installed EXE. The helper
// waits for the parent process to exit before invoking UniversalUpdater's
// transactional File.Replace + .bak rollback + post-install UI self-test.
// Never copy or run a user-writable helper with an elevated security token.
internal static class UpdateHandoff
{
    private const string ExeName = "Microsoft365AuthRepair.exe";
    private const string HelperName = "Microsoft365AuthRepair.UpdateHelper.exe";
    private const string HelpersSubfolder = "UpdaterHelpers";
    private const string ResultFile = "last-update-result.json";

    private sealed class Ticket
    {
        public int ParentPid { get; set; }
        public string OriginalPath { get; set; } = "";
        public string OriginalSha256 { get; set; } = "";
        public string InstalledVersion { get; set; } = "";
        public string Version { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string Channel { get; set; } = "";
        public string ArchivePath { get; set; } = "";
        public string ArchiveSha256 { get; set; } = "";
        public string ExtractedPath { get; set; } = "";
        public string ExtractedSha256 { get; set; } = "";
    }

    private sealed class Receipt
    {
        public string Code { get; set; } = "";
        public string Version { get; set; } = "";
        // Newer helpers include exact-file evidence. Older Preview helpers
        // wrote Code/Version only; legacy cleanup requires PE version proof.
        public string InstalledPath { get; set; } = "";
        public string InstalledSha256 { get; set; } = "";
        public string BackupSha256 { get; set; } = "";
    }

    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft365AuthRepair");

    private static string HelperRoot => Path.Combine(Root, HelpersSubfolder);
    // Elevated helper code must live in admin-protected storage, never in a
    // standard user's writable LocalAppData temp directory.
    private static string ProtectedHelperRoot =>
        Path.Combine(Scheduling.ProtectedRoot, HelpersSubfolder);

    private static string Digest(string file)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(fs));
    }

    private static bool Sha(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string Full(string path) => Path.GetFullPath(path);

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Reparse-point paths are forbidden during an update.");
    }

    private static string NormalizeRoot(string path) =>
        Full(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    internal static void Queue(StagedPackage package)
    {
        bool elevated = Identity.IsElevated();
        if (elevated)
            Scheduling.EnsureProtectedRoot();

        var target = Environment.ProcessPath ??
            throw new InvalidOperationException("Installed application path is unavailable.");
        target = Full(target);
        if (!Path.GetFileName(target).Equals(ExeName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The application must be launched from its normal EXE.");
        if (target.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Automatic update is not available from a network share.");

        var installDir = Path.GetDirectoryName(target)!;
        RejectLink(installDir);
        RejectLink(target);
        var options = UpdaterIntegration.Options(UpdaterIntegration.ChannelFor(package.Release));
        options.Validate();
        if (package.Release.Repository != UpdaterIntegration.Repository ||
            !string.Equals(package.Release.Rule.Architecture, options.Architecture, StringComparison.Ordinal) ||
            package.Release.Rule.Kind != PackageKind.SingleExeZip ||
            package.ExtractedExecutablePath is null ||
            package.ExtractedExecutableSha256 is null ||
            !Sha(package.ArchiveSha256) || !Sha(package.ExtractedExecutableSha256) ||
            package.Release.Version <= SemanticVersion.Parse(Repair.Version))
            throw new InvalidDataException("Staged update does not match the installed application.");

        // Fail early on read-only directories, without escalating or creating
        // an executable in Program Files or another protected folder.
        var writeProbe = Path.Combine(installDir, ".m365-update-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var file = new FileStream(writeProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        finally
        {
            try { if (File.Exists(writeProbe)) File.Delete(writeProbe); } catch (IOException) { }
        }

        var helperBase = elevated ? ProtectedHelperRoot : HelperRoot;
        if (elevated)
        {
            Directory.CreateDirectory(helperBase); // inherits protected ACL
            RejectLink(helperBase);
        }
        var helperDir = Path.Combine(helperBase, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(helperDir);
        RejectLink(helperDir);
        var helperPath = Path.Combine(helperDir, HelperName);
        var ticketPath = Path.Combine(helperDir, "update-ticket.json");
        try
        {
            File.Copy(target, helperPath, overwrite: false);
            var sourceHash = Digest(target);
            if (Digest(helperPath) != sourceHash)
                throw new InvalidDataException("Updater helper copy verification failed.");

            var ticket = new Ticket
            {
                ParentPid = Environment.ProcessId,
                OriginalPath = target,
                OriginalSha256 = sourceHash,
                InstalledVersion = Repair.Version,
                Version = package.Release.Version.ToString(),
                Architecture = options.Architecture,
                Channel = options.Channel.ToString(),
                ArchivePath = package.ArchivePath,
                ArchiveSha256 = package.ArchiveSha256,
                ExtractedPath = package.ExtractedExecutablePath,
                ExtractedSha256 = package.ExtractedExecutableSha256
            };
            File.WriteAllText(ticketPath, JsonSerializer.Serialize(ticket), Encoding.UTF8);
            var psi = new ProcessStartInfo(helperPath)
            {
                UseShellExecute = false,
                WorkingDirectory = helperDir,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--update-helper");
            psi.ArgumentList.Add(ticketPath);
            using var started = Process.Start(psi) ??
                throw new InvalidOperationException("Unable to start the update helper.");
        }
        catch
        {
            try { Directory.Delete(helperDir, recursive: true); } catch (IOException) { }
            throw;
        }
    }

    // Called only after the user clicked Update and staging verified the
    // GitHub asset. For a machine-protected install UAC launches a fresh copy
    // of the installed EXE, which downloads/verifies in its own admin profile
    // and completes the replacement. Never elevate a copied temp executable.
    internal static bool TryRestartElevated(bool includePreview, out string result)
    {
        result = "No permission change was requested.";
        if (Identity.IsElevated()) return false;
        var origin = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(origin) || !File.Exists(origin)) return false;
        origin = Path.GetFullPath(origin);
        if (!Path.GetFileName(origin).Equals(ExeName, StringComparison.OrdinalIgnoreCase) ||
            origin.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        var dir = Path.GetDirectoryName(origin)!;
        string programFiles = Path.GetFullPath(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles)).TrimEnd('\\') + "\\";
        string programFilesX86 = Path.GetFullPath(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86)).TrimEnd('\\') + "\\";
        string protectedRoot = Path.GetFullPath(Scheduling.ProtectedRoot).TrimEnd('\\') + "\\";
        if (!origin.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) &&
            !origin.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase) &&
            !origin.StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            result = "The executable is in a nonstandard protected folder; " +
                     "automatic privileged installation is not enabled for that location.";
            return false;
        }

        var caption = UpdateUiText.T("Administrator permission required",
            "Требуются права администратора", "נדרשת הרשאת מנהל");
        var message = UpdateUiText.T(
            "Windows requires administrator permission to replace the EXE in this protected folder. Continue automatic Update? The new elevated window will verify the release again and restart the application. No account repair will start.",
            "Для замены EXE в этой защищённой папке нужны права администратора. Продолжить автоматическое обновление? Новое окно повторно проверит релиз и перезапустит программу. Восстановление учётных записей не запускается.",
            "דרושה הרשאת מנהל להחלפת ה-EXE בתיקייה זו. להמשיך עדכון אוטומטי? לא תתבצע פעולת תיקון חשבון.");
        if (System.Windows.Forms.MessageBox.Show(message, caption,
            System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Information,
            System.Windows.Forms.MessageBoxDefaultButton.Button2) != System.Windows.Forms.DialogResult.Yes)
        {
            result = "Operator declined UAC. No installation was performed.";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo(origin)
            {
                UseShellExecute = true, Verb = "runas", WorkingDirectory = dir
            };
            psi.ArgumentList.Add(includePreview ? "--update-elevated-preview" : "--update-elevated-stable");
            using var started = Process.Start(psi) ??
                throw new InvalidOperationException("Windows did not start the elevated updater.");
            result = "UAC accepted; continuing automated update as administrator.";
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            result = "UAC cancelled; verified package is still available.";
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            result = "Could not elevate updater (" + ex.GetType().Name + ").";
            return false;
        }
    }

    internal static int RunHelper(string ticketPath)
    {
        try
        {
            return RunHelperAsync(ticketPath).GetAwaiter().GetResult();
        }
        catch
        {
            SaveResult("HELPER_FAILED", "");
            return 2;
        }
    }

    private static async Task<int> RunHelperAsync(string ticketPath)
    {
        var self = Full(Environment.ProcessPath ??
            throw new InvalidOperationException("Update helper executable unavailable."));
        var expectedHelperRoot = Identity.IsElevated() ? ProtectedHelperRoot : HelperRoot;
        if (!Path.GetFileName(self).Equals(HelperName, StringComparison.Ordinal) ||
            !Path.GetDirectoryName(self)!.StartsWith(NormalizeRoot(expectedHelperRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Updater helper is not in its expected directory.");

        ticketPath = Full(ticketPath);
        if (!Path.GetDirectoryName(ticketPath)!.Equals(Path.GetDirectoryName(self), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(ticketPath) != "update-ticket.json")
            throw new InvalidDataException("Updater ticket is outside the helper directory.");
        RejectLink(ticketPath);
        RejectLink(self);
        var ticket = JsonSerializer.Deserialize<Ticket>(File.ReadAllText(ticketPath))
            ?? throw new InvalidDataException("Update ticket is missing.");
        if (ticket.ParentPid <= 0 || ticket.InstalledVersion != Repair.Version ||
            !Sha(ticket.OriginalSha256) || !Sha(ticket.ArchiveSha256) ||
            !Sha(ticket.ExtractedSha256) || !RegexVersion(ticket.Version) ||
            ticket.Architecture != UpdaterIntegration.Options(UpdateChannel.Stable).Architecture ||
            ticket.Channel is not ("Stable" or "Prerelease"))
            throw new InvalidDataException("Invalid update ticket.");

        if (Digest(self) != ticket.OriginalSha256)
            throw new InvalidDataException("Updater helper differs from the installed application.");

        var target = Full(ticket.OriginalPath);
        if (!Path.GetFileName(target).Equals(ExeName, StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid installed EXE target.");
        RejectLink(Path.GetDirectoryName(target)!);
        RejectLink(target);

        // Parent has to exit normally; do not kill user processes.
        try
        {
            using var parent = Process.GetProcessById(ticket.ParentPid);
            if (!parent.WaitForExit(60000))
                throw new TimeoutException("The application did not exit for the update.");
        }
        catch (ArgumentException) { /* parent already exited */ }

        if (Digest(target) != ticket.OriginalSha256)
            throw new InvalidDataException("Installed EXE changed before replacement.");

        var channel = ticket.Channel == "Stable" ? UpdateChannel.Stable : UpdateChannel.Prerelease;
        var options = UpdaterIntegration.Options(channel);
        var rule = options.Assets.Single(a => a.Architecture == ticket.Architecture);
        var version = SemanticVersion.Parse(ticket.Version);
        if (version <= SemanticVersion.Parse(ticket.InstalledVersion) ||
            channel == UpdateChannel.Stable && version.IsPrerelease)
            throw new InvalidDataException("Refusing a downgrade or an incompatible channel.");

        var tag = "v" + ticket.Version;
        var url = $"https://github.com/{UpdaterIntegration.Repository}/releases/download/{tag}/{rule.AssetName}";
        var release = new UpdateRelease(
            UpdaterIntegration.Repository, version, tag,
            $"https://github.com/{UpdaterIntegration.Repository}/releases/tag/{tag}",
            "", null, rule, new Uri(url), new FileInfo(ticket.ArchivePath).Length,
            ticket.ArchiveSha256);
        var staged = new StagedPackage(release, ticket.ArchivePath, ticket.ArchiveSha256,
            ticket.ExtractedPath, ticket.ExtractedSha256);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var result = await new PortableExeTransaction().ApplyManualAsync(
            options, staged, Path.GetDirectoryName(target)!, ExeName,
            explicitlyApproved: true, HealthProbe, deadline.Token);

        SaveResult(result.Success ? "INSTALL_OK" : result.Summary, ticket.Version,
            result.Success ? target : "",
            result.Success ? ticket.ExtractedSha256 : "",
            result.Success ? ticket.OriginalSha256 : "");
        if (!result.Success && !result.RolledBack)
            return 3;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch { /* The user can launch the executable from its original location. */ }
        return result.Success ? 0 : 4;
    }

    private static bool RegexVersion(string v) =>
        System.Text.RegularExpressions.Regex.IsMatch(v,
            @"\A\d+\.\d+\.\d+(?:-preview\.\d+)?\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static async Task<bool> HealthProbe(string installedExe, CancellationToken ct)
    {
        try
        {
            var info = new ProcessStartInfo(installedExe) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("--ui-self-test");
            using var test = Process.Start(info);
            if (test is null) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            try { await test.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                try { test.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return false;
            }
            return test.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void SaveResult(string code, string version,
        string installedPath = "", string installedHash = "", string backupHash = "")
    {
        try
        {
            Directory.CreateDirectory(Root);
            var receipt = JsonSerializer.Serialize(new Receipt
            {
                Code = code, Version = version,
                InstalledPath = installedPath,
                InstalledSha256 = installedHash, BackupSha256 = backupHash
            });
            File.WriteAllText(Path.Combine(Root, ResultFile), receipt, Encoding.UTF8);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static Receipt? ReadReceipt()
    {
        try
        {
            var file = Path.Combine(Root, ResultFile);
            if (!File.Exists(file) || new FileInfo(file).Length > 4096)
                return null;
            var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file));
            return receipt is { Version.Length: <= 70, Code.Length: <= 70 } &&
                RegexVersion(receipt.Version) ? receipt : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or JsonException or System.Security.SecurityException)
        { return null; }
    }

    private static void DeleteReceipt()
    {
        try { File.Delete(Path.Combine(Root, ResultFile)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // INSTALL_OK is written only after the transaction checked the installed
    // hash and ran the GUI self-test. The .bak is deliberately retained until
    // the updated main application reaches its visible Shown event.
    internal static string? ConsumeResult()
    {
        var receipt = ReadReceipt();
        if (receipt is null) return null;
        if (receipt.Code != "INSTALL_OK")
        {
            DeleteReceipt();
            return receipt.Code switch
            {
                "INSTALL_FAILED_RESTORED" =>
                    "Update failed; previous version restored. Check updater logs.",
                "ROLLBACK_REQUIRES_ATTENTION" =>
                    "Update failed; manual rollback is required.",
                _ => "Update did not finish (" + receipt.Code + ")."
            };
        }
        if (receipt.Version != Repair.Version) return null;
        return "Updated successfully to v" + receipt.Version + ".";
    }

    internal static bool CanDeleteSuccessfulBackup(string code,
        string receiptVersion, string runningVersion, string exeName,
        string receiptPath, string actualPath) =>
        code == "INSTALL_OK" && receiptVersion == runningVersion &&
        exeName.Equals(ExeName, StringComparison.OrdinalIgnoreCase) &&
        !actualPath.StartsWith(@"\\", StringComparison.Ordinal) &&
        (string.IsNullOrEmpty(receiptPath) ||
         string.Equals(receiptPath, actualPath, StringComparison.OrdinalIgnoreCase));

    // Called only after the new main WinForms window is shown, never from
    // --ui-self-test, the old updater helper, or a maintenance/worker entry.
    // Failed updates and mismatched versions leave .bak completely untouched.
    internal static void CleanupSuccessfulBackupAfterStartup()
    {
        var receipt = ReadReceipt();
        if (receipt is null || receipt.Code != "INSTALL_OK") return;
        try
        {
            var current = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(current)) return;
            current = Full(current);
            if (!CanDeleteSuccessfulBackup(receipt.Code, receipt.Version,
                Repair.Version, Path.GetFileName(current), receipt.InstalledPath,
                current)) return;
            var backup = current + ".bak";
            if (!File.Exists(backup))
            {
                DeleteReceipt(); // successful install did not create a backup
                return;
            }
            RejectLink(Path.GetDirectoryName(current)!);
            RejectLink(current);
            RejectLink(backup);
            // A new helper supplies exact before/after hashes. A legacy helper
            // (Preview.26 -> Preview.27) did not; in that case we require the
            // backup's Windows file version to be strictly older than the new
            // program, preventing deletion of an unrelated same-name file.
            if (Sha(receipt.InstalledSha256) && Sha(receipt.BackupSha256))
            {
                if (!Digest(current).Equals(receipt.InstalledSha256,
                        StringComparison.OrdinalIgnoreCase) ||
                    !Digest(backup).Equals(receipt.BackupSha256,
                        StringComparison.OrdinalIgnoreCase)) return;
            }
            else
            {
                var old = FileVersionInfo.GetVersionInfo(backup).FileVersion;
                var installed = FileVersionInfo.GetVersionInfo(current).FileVersion;
                if (!Version.TryParse(old, out var oldVersion) ||
                    !Version.TryParse(installed, out var installedVersion) ||
                    oldVersion >= installedVersion) return;
            }
            File.Delete(backup);
            DeleteReceipt();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException or ArgumentException or
            System.ComponentModel.Win32Exception)
        {
            // Safe retry on a later normal startup; the rollback file is
            // intentionally preserved if access or integrity is in doubt.
        }
    }

    internal static void CleanupOldHelpers()
    {
        try
        {
            // Never delete a protected machine worker from a non-elevated
            // process. It might still be needed for offline profile logons.
            var baseDir = Identity.IsElevated() ? ProtectedHelperRoot : HelperRoot;
            if (!Directory.Exists(baseDir)) return;
            foreach (var dir in Directory.EnumerateDirectories(baseDir))
            {
                if (Directory.GetCreationTimeUtc(dir) > DateTime.UtcNow.AddDays(-1)) continue;
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static bool SelfTest()
    {
        return RegexVersion("1.0.2-preview.3")
            && RegexVersion("1.2.3")
            && !RegexVersion("0.0.3 && whoami")
            && !RegexVersion("../evil")
            && Sha(new string('a', 64)) &&
            CanDeleteSuccessfulBackup("INSTALL_OK", Repair.Version, Repair.Version,
                ExeName, "", @"C:\Apps\Microsoft365AuthRepair.exe") &&
            !CanDeleteSuccessfulBackup("INSTALL_FAILED_RESTORED",
                Repair.Version, Repair.Version, ExeName, "",
                @"C:\Apps\Microsoft365AuthRepair.exe") &&
            !CanDeleteSuccessfulBackup("INSTALL_OK", "1.0.2-preview.26",
                Repair.Version, ExeName, "",
                @"C:\Apps\Microsoft365AuthRepair.exe") &&
            !CanDeleteSuccessfulBackup("INSTALL_OK", Repair.Version,
                Repair.Version, "unrelated.exe", "",
                @"C:\Apps\unrelated.exe") &&
            !CanDeleteSuccessfulBackup("INSTALL_OK", Repair.Version,
                Repair.Version, ExeName, @"C:\Other\Microsoft365AuthRepair.exe",
                @"C:\Apps\Microsoft365AuthRepair.exe");
    }
}
