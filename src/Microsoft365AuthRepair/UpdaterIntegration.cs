using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UniversalUpdater;

namespace Microsoft365AuthRepair;

internal static class UpdaterIntegration
{
    internal const string Repository = "sgennadi/Microsoft365AuthRepair";
    internal static UpdateOptions Options(UpdateChannel channel) => new()
    {
        ProductId = "Microsoft365AuthRepair",
        Repository = Repository,
        InstalledVersion = Repair.Version,
        Architecture = UpdateOptions.CurrentWindowsArchitecture(),
        Channel = channel,
        Assets =
        [
            new UpdateAssetRule("win-x64", "Microsoft365AuthRepair-win-x64.zip", PackageKind.SingleExeZip, "Microsoft365AuthRepair.exe"),
            new UpdateAssetRule("win-arm64", "Microsoft365AuthRepair-win-arm64.zip", PackageKind.SingleExeZip, "Microsoft365AuthRepair.exe")
        ]
    };

    internal static async Task<UpdateRelease?> FindAsync(bool withPreview, CancellationToken token = default)
    {
        using var client = new GitHubReleaseClient();
        var stable = await client.CheckAsync(Options(UpdateChannel.Stable), token);
        if (!withPreview) return stable;
        var preview = await client.CheckAsync(Options(UpdateChannel.Prerelease), token);
        return preview is not null && (stable is null || preview.Version > stable.Version) ? preview : stable;
    }

    internal static UpdateChannel ChannelFor(UpdateRelease release) =>
        release.Version.IsPrerelease ? UpdateChannel.Prerelease : UpdateChannel.Stable;
}

internal static class UpdateUiText
{
    internal static string T(string en, string ru, string he) =>
        UiStrings.Language switch { "ru" => ru, "he" => he, _ => en };

    internal static string FormatWhatsNew(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "No change details supplied.";
        var all = new List<string>();
        var selected = new List<string>();
        bool section = false, seen = false;
        foreach (var original in notes.Replace("\r", "").Split('\n'))
        {
            var line = original.Trim();
            if (Regex.IsMatch(line, @"^#{1,5}\s+(What's new|Changes|Изменения|Что нового|Changelog)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                section = seen = true;
                continue;
            }
            if (section && line.StartsWith('#')) break;
            var match = Regex.Match(line, @"^(?:\d{1,2}[.)]|[-*+])\s+(.+)$");
            if (!match.Success) continue;
            var item = Regex.Replace(match.Groups[1].Value, @"\[([^\]]+)\]\([^)]+\)", "$1");
            item = item.Replace("**", "").Replace("\u0060", "").Trim();
            if (item.Length == 0) continue;
            if (item.Length > 190) item = item[..187] + "...";
            if (section) selected.Add(item);
            else if (!seen) all.Add(item);
        }
        var list = selected.Count > 0 ? selected : all;
        return list.Count == 0 ? "See release page for details." :
            string.Join(Environment.NewLine, list.Take(8).Select((x, i) => $"{i + 1}. {x}"));
    }

    internal static string CurrentChangelog() => T(
        "1. Modern buttons across all three windows.\n2. One-click Update: verified download and safe replace with .bak rollback.\n3. Real progress, restart and short What's New.\n4. WAM diagnostics and anonymous report preview.",
        "1. Современные кнопки во всех окнах.\n2. Update: проверка, замена EXE и откат при ошибке.\n3. Прогресс, перезапуск и краткий список изменений.\n4. Диагностика WAM и предпросмотр отчёта.",
        "1. כפתורים מודרניים בכל החלונות.\n2. עדכון מאומת עם גיבוי ושחזור.\n3. התקדמות והפעלה מחדש.\n4. אבחון WAM ודוח לבדיקה.");
    
    internal static bool SelfTest() => FormatWhatsNew(
        "## What's new\n1. Alpha\n2. Beta\n## Limitations\n- Private") ==
        $"1. Alpha{Environment.NewLine}2. Beta";
}

internal sealed class UpdateCenter : Form
{
    private readonly ComboBox channel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Label installed = UiTheme.Caption(string.Empty);
    private readonly Label available = UiTheme.Caption(string.Empty);
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly Label bytesText = UiTheme.Caption(string.Empty);
    private readonly TextBox changes = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 10f), BackColor = SystemColors.Window
    };
    private readonly ProgressBar progress = new()
    {
        Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Height = 20
    };
    private readonly Button check = UiTheme.CreateActionButton("Check now");
    private readonly Button download = UiTheme.CreateActionButton("Update", true);
    private readonly Button page = UiTheme.CreateActionButton("Release page");
    private readonly Button folder = UiTheme.CreateActionButton("Open folder");
    private readonly Button cancel = UiTheme.CreateActionButton("Cancel");
    private UpdateRelease? release;
    private StagedPackage? staged;
    private CancellationTokenSource? active;
    private bool busy;

    internal UpdateCenter()
    {
        Text = $"Microsoft 365 Auth Repair v{Repair.Version} - Updates";
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(710, 510);
        Size = new Size(825, 630);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
            Padding = new Padding(18), ColumnCount = 1, RowCount = 8 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 7; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = UiTheme.Caption("Microsoft 365 Auth Repair", true);
        header.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        root.Controls.Add(header, 0, 0);

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        top.Controls.Add(UiTheme.Caption(UpdateUiText.T("Channel:", "Канал:", "ערוץ:")));
        channel.Items.AddRange([
            UpdateUiText.T("Stable", "Стабильная", "יציבה"),
            "Preview + Stable"
        ]);
        channel.SelectedIndex = 0;
        top.Controls.Add(channel);
        check.Text = UpdateUiText.T("Check now", "Проверить", "בדוק");
        top.Controls.Add(check);
        root.Controls.Add(top, 0, 1);

        installed.Text = UpdateUiText.T(
            $"Installed: v{Repair.Version} / {UpdaterIntegration.Options(UpdateChannel.Stable).Architecture}",
            $"Установлено: v{Repair.Version} / {UpdaterIntegration.Options(UpdateChannel.Stable).Architecture}",
            $"מותקן: v{Repair.Version} / {UpdaterIntegration.Options(UpdateChannel.Stable).Architecture}");
        available.Text = UpdateUiText.T("Available: not checked", "Доступно: не проверено", "זמין: טרם נבדק");
        root.Controls.Add(installed, 0, 2);
        root.Controls.Add(available, 0, 3);

        var transfer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 8, 0, 8) };
        transfer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        transfer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        transfer.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        transfer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        status.Text = UpdateUiText.T("Ready.", "Готово.", "מוכן.");
        transfer.Controls.Add(status, 0, 0);
        transfer.Controls.Add(progress, 0, 1);
        transfer.Controls.Add(bytesText, 0, 2);
        root.Controls.Add(transfer, 0, 4);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        download.Text = UpdateUiText.T("Update", "Обновить", "עדכן");
        page.Text = UpdateUiText.T("Release page", "Страница релиза", "דף גרסה");
        folder.Text = UpdateUiText.T("Open folder", "Открыть папку", "פתח תיקייה");
        cancel.Text = UpdateUiText.T("Cancel", "Отмена", "ביטול");
        actions.Controls.Add(download);
        actions.Controls.Add(page);
        actions.Controls.Add(folder);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 5);
        root.Controls.Add(UiTheme.Caption(UpdateUiText.T("What's new", "Что нового", "מה חדש")), 0, 6);
        changes.Text = UpdateUiText.CurrentChangelog();
        root.Controls.Add(changes, 0, 7);
        Controls.Add(root);
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(check, UiGlyph.Search);
        UiIcons.Assign(download, UiGlyph.Updates, primary: true);
        download.UseMnemonic = false;
        UiIcons.Assign(page, UiGlyph.Link);
        UiIcons.Assign(folder, UiGlyph.Folder);
        UiIcons.Assign(cancel, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        var tips = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500, ReshowDelay = 200 };
        tips.SetToolTip(download, "One click: download, SHA-256 and architecture verification, safe install, health check and rollback.");
        tips.SetToolTip(channel, "Preview + Stable chooses the highest newer compatible version from both channels.");
        download.Enabled = page.Enabled = folder.Enabled = cancel.Enabled = false;
        check.Click += async (_, _) => await CheckAsync();
        download.Click += async (_, _) => await UpdateAsync();
        cancel.Click += (_, _) => active?.Cancel();
        channel.SelectedIndexChanged += async (_, _) =>
        {
            // Selecting Stable or Preview+Stable immediately performs a check.
            if (!busy) await CheckAsync();
        };
        page.Click += (_, _) =>
        {
            if (release is not null)
                Process.Start(new ProcessStartInfo(release.ReleaseUrl) { UseShellExecute = true });
        };
        folder.Click += (_, _) =>
        {
            if (staged is not null && File.Exists(staged.ArchivePath))
                Process.Start(new ProcessStartInfo("explorer.exe",
                    $"/select,\"{staged.ArchivePath}\"") { UseShellExecute = true });
        };
        FormClosing += (_, _) => active?.Cancel();
        Shown += async (_, _) => await CheckAsync(); // One check immediately on opening, no second Check click.
    }

    private void Clear()
    {
        release = null;
        staged = null;
        available.Text = UpdateUiText.T("Available: not checked", "Доступно: не проверено", "זמין: טרם נבדק");
        changes.Clear();
        progress.Value = 0;
        bytesText.Text = string.Empty;
        download.Enabled = page.Enabled = folder.Enabled = false;
    }

    private void Busy(bool value)
    {
        busy = value;
        check.Enabled = channel.Enabled = !value;
        cancel.Enabled = value;
        download.Enabled = !value && release is not null && staged is null;
        page.Enabled = !value && release is not null;
        folder.Enabled = !value && staged is not null;
    }

    private async Task CheckAsync()
    {
        if (busy) return;
        Clear();
        Busy(true);
        status.Text = UpdateUiText.T("Checking GitHub Releases...", "Проверка GitHub Releases...", "בודק גרסאות...");
        progress.Style = ProgressBarStyle.Marquee;
        active = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            release = await UpdaterIntegration.FindAsync(channel.SelectedIndex == 1, active.Token);
            if (release is null)
            {
                available.Text = UpdateUiText.T("Available: none", "Доступно: нет", "זמין: אין");
                status.Text = UpdateUiText.T("You're up to date.", "Установлена актуальная версия.", "הגרסה מעודכנת.");
                changes.Text = UpdateUiText.CurrentChangelog();
            }
            else
            {
                available.Text = UpdateUiText.T($"New: v{release.Version} ({release.Rule.Architecture})",
                    $"Доступна: v{release.Version} ({release.Rule.Architecture})",
                    $"זמין: v{release.Version} ({release.Rule.Architecture})");
                changes.Text = UpdateUiText.FormatWhatsNew(release.ReleaseNotes);
                download.Focus(); // Keyboard users can update directly after release discovery.
                status.Text = UpdateUiText.T("Update available.", "Есть обновление.", "עדכון זמין.");
                bytesText.Text = UpdateUiText.T("Update will verify, install and restart (same-user writable installs).",
                    "Update проверит файл, установит и перезапустит программу (без UAC).",
                    "העדכון ייבדק ויותקן ללא העלאת הרשאות.");
            }
        }
        catch (OperationCanceledException) { status.Text = UpdateUiText.T("Cancelled.", "Отменено.", "בוטל."); }
        catch (Exception ex)
        {
            available.Text = UpdateUiText.T("Available: unknown", "Доступно: неизвестно", "זמין: לא ידוע");
            status.Text = $"GitHub check failed ({ex.GetType().Name}).";
        }
        finally
        {
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            active.Dispose();
            active = null;
            Busy(false);
        }
    }

    private async Task UpdateAsync()
    {
        if (busy || release is null) return;
        var chosen = release;
        Busy(true);
        progress.Style = ProgressBarStyle.Continuous;
        progress.Value = 0;
        bytesText.Text = "0%";
        status.Text = UpdateUiText.T("Updating: downloading...", "Обновление: загрузка...", "מעדכן: מוריד...");
        active = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var receiver = new Progress<UpdateProgress>(p =>
        {
            if (IsDisposed || Disposing) return;
            if (p.Phase == UpdatePhase.Downloading && p.TotalBytes is > 0)
            {
                var percent = (int)Math.Clamp(p.ReceivedBytes * 100 / p.TotalBytes.Value, 0L, 100L);
                progress.Value = percent;
                bytesText.Text = $"{percent}% • {p.ReceivedBytes / 1048576d:F1} / {p.TotalBytes.Value / 1048576d:F1} MiB";
            }
            else if (p.Phase == UpdatePhase.Verified)
            {
                progress.Value = 100;
                bytesText.Text = "100% • SHA-256 verified";
            }
        });
        try
        {
            using var downloader = new UpdateDownloader();
            staged = await downloader.StageAsync(
                UpdaterIntegration.Options(UpdaterIntegration.ChannelFor(chosen)),
                chosen, receiver, active.Token);
            progress.Value = 100;
            bytesText.Text = "100% - SHA-256 and architecture verified";
            status.Text = UpdateUiText.T("Installing verified update...", "Установка проверенного обновления...", "מתקין עדכון מאומת...");
            // A same-user helper runs outside the application, after it exits.
            // The helper calls PortableExeTransaction for atomic replacement,
            // health probe and rollback. Never silently elevate.
            UpdateHandoff.Queue(staged);
            status.Text = UpdateUiText.T("Restarting to complete update...", "Перезапуск для завершения обновления...", "מפעיל מחדש לסיום העדכון...");
            Application.Exit();
        }
        catch (OperationCanceledException)
        {
            status.Text = UpdateUiText.T("Update cancelled.", "Обновление отменено.", "העדכון בוטל.");
            progress.Value = 0;
            bytesText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            status.Text = UpdateUiText.T(
                "Update not installed: " + ex.Message + " Verified ZIP remains available under Open folder.",
                "Обновление не установлено: " + ex.Message + " Проверенный ZIP доступен через «Открыть папку».",
                "העדכון לא הותקן: " + ex.GetType().Name + ". הקובץ המאומת זמין בתיקייה.");
            progress.Value = 0;
            bytesText.Text = string.Empty;
        }
        finally
        {
            active.Dispose();
            active = null;
            Busy(false);
        }
    }
}
