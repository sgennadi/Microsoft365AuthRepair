using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Read-only Office licensing detail. Microsoft 365 subscription and Office
// volume installations are intentionally never treated as interchangeable.
internal sealed record OfficeActivationHealth(string Family, string Summary,
    string Evidence, bool VerifiedActivation, bool RequiresReview);

internal static class OfficeActivationProbe
{
    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);

    private static string? FindVNextDiag()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (var pf in roots)
        {
            if (string.IsNullOrWhiteSpace(pf)) continue;
            foreach (var suffix in new[]
            {
                @"Microsoft Office\root\Office16\vnextdiag.ps1",
                @"Microsoft Office\Office16\vnextdiag.ps1"
            })
            {
                var path = Path.GetFullPath(Path.Combine(pf, suffix));
                if (!path.StartsWith(Path.GetFullPath(pf) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (!File.Exists(path) ||
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    return path;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        return null;
    }

    // Only allowlisted state words, not the raw vnextdiag output: this can
    // contain UPN, tenant IDs, license file paths and identifiers.
    internal static string InterpretVNextState(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "UNKNOWN";
        foreach (var line in text.Replace("\r", "").Split('\n').Take(200))
        {
            var match = Regex.Match(line, @"(?i)^\s*(?:License\s*(?:State|Status)|Activation\s*Status)\s*:\s*(?<state>[A-Za-z _-]{2,30})\s*$");
            if (!match.Success) continue;
            var state = match.Groups["state"].Value.Trim().ToUpperInvariant();
            if (state is "LICENSED" or "ACTIVATED") return "LICENSED";
            if (state is "UNLICENSED" or "UNLICENSED NOTIFICATION") return "UNLICENSED";
            if (state is "GRACE" or "EXTENDED GRACE") return "GRACE";
        }
        return "UNKNOWN";
    }

    internal static OfficeActivationHealth Capture(OfficeLicenseSnapshot? info,
        bool sameWindowsAccount)
    {
        if (info is null || !info.Installed)
            return new("Unknown",
                T("Office installation not verified", "Установка Office не подтверждена",
                    "התקנת Office לא אומתה"),
                T("Click-to-Run inventory did not confirm installed Office.",
                    "Инвентаризация Click-to-Run не подтвердила Office.",
                    "לא זוהתה התקנת Office."), false, true);

        if (info.ProductFamily.Contains("volume", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var inventory = OfficeVolumeLicensing.Read();
                var existing = OfficeVolumeLicensing.PreferredProducts(inventory)
                    .Where(x => x.HasKey).ToArray();
                var licensed = existing.Count(OfficeVolumeLicensing.IsActive);
                var preview = existing.Count(x => x.IsPreview);
                string evidence = T(
                    $"Office Volume licensing provider: {existing.Length} installed keyed product records; {licensed} report Licensed; {preview} Preview records. Product identity is rechecked before any key change.",
                    $"Провайдер Office Volume: {existing.Length} записей с ключом; {licensed} сообщают Licensed; {preview} записей Preview. Перед изменением ключей идентификатор проверяется повторно.",
                    $"ספק רישוי Office Volume: {existing.Length} מוצרים, {licensed} מופעלים, {preview} בגרסת Preview.");
                if (inventory.Warning is not null) evidence += " [Provider: " + inventory.Warning.GetType().Name + "]";
                // A mixed installation can have both activated and unlicensed
                // products. Do not summarize it as wholly activated.
                bool complete = existing.Length > 0 && licensed == existing.Length && preview == 0;
                return new("Volume",
                    complete
                        ? T("Volume products report Licensed", "Volume-продукты сообщают Licensed", "מוצרי Volume מופעלים")
                        : T("Review Office/Visio/Project activation", "Проверьте активацию Office/Visio/Project", "בדוק הפעלת Office"),
                    evidence, complete, !complete);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or System.Management.ManagementException or InvalidOperationException
                or System.Runtime.InteropServices.COMException)
            {
                return new("Volume",
                    T("Volume licensing status unavailable", "Статус Office Volume недоступен", "מצב רישוי Volume אינו זמין"),
                    "Office-only licensing provider: " + ex.GetType().Name, false, true);
            }
        }

        if (!info.ProductFamily.Contains("subscription", StringComparison.OrdinalIgnoreCase))
            return new("Retail / Other",
                T("Office edition detected; activation unverified", "Редакция Office найдена; активация не подтверждена",
                    "גרסת Office זוהתה אך הפעלה לא אומתה"),
                T("This is not a verified Office 2024 Volume SKU; KMS key replacement is not offered.",
                  "Это не подтверждённая Office 2024 Volume; замена KMS-ключа не предлагается.",
                  "אין להחליף מפתח KMS ללא אימות גרסה."), false, true);

        if (!sameWindowsAccount || !info.UserLicenseScopeChecked)
            return new("Microsoft 365 subscription",
                T("Subscription status belongs to another account", "Подписка относится к другому пользователю",
                  "רישיון השייך למשתמש אחר"),
                T("Launch unelevated as the affected user. Administrator HKCU tokens are not that user's tokens.",
                  "Запустите без повышения прав от проблемного пользователя; HKCU администратора не относится к нему.",
                  "הפעל כמשתמש הרלוונטי ללא הרשאות מנהל."), false, true);

        var script = FindVNextDiag();
        if (script is null)
            return new("Microsoft 365 subscription",
                T("Microsoft subscription tool unavailable", "Средство диагностики подписки недоступно",
                    "כלי בדיקת מנוי אינו זמין"),
                T("Local subscription markers are hints, not a confirmed activated license.",
                  "Локальные маркеры подписки — только признаки, но не подтверждение активации.",
                  "סימנים מקומיים אינם אישור להפעלה."), false, true);
        try
        {
            // Microsoft-supplied local read-only '-action list'. Do not bypass
            // PowerShell execution policy, transmit output, or retain account IDs.
            var command = ExternalCommand.Run(ExternalCommand.PowerShellPath(),
                ["-NoLogo", "-NoProfile", "-NonInteractive",
                 "-File", script, "-action", "list"], 35);
            var state = command.ExitCode == 0
                ? InterpretVNextState(command.StdOut) : "UNKNOWN";
            return new("Microsoft 365 subscription",
                state switch
                {
                    "LICENSED" => T("Microsoft tool reports a licensed subscription",
                        "Microsoft сообщает о лицензированной подписке", "מנוי מורשה לפי כלי Microsoft"),
                    "UNLICENSED" => T("Microsoft tool reports unlicensed",
                        "Microsoft сообщает: лицензия отсутствует", "רישיון חסר לפי כלי Microsoft"),
                    "GRACE" => T("Microsoft tool reports grace period",
                        "Microsoft сообщает о льготном периоде", "תקופת חסד לפי כלי Microsoft"),
                    _ => T("Subscription activation could not be verified",
                        "Активацию подписки не удалось подтвердить", "לא ניתן לאמת הפעלת מנוי")
                },
                T("Read-only vnextdiag.ps1 -action list. Raw output, tenant IDs, UPNs and license data were not retained. Result: ",
                  "Только чтение vnextdiag.ps1 -action list. Исходный вывод, UPN и данные лицензий не сохраняются. Результат: ",
                  "פלט רגיש לא נשמר. תוצאה: ") + state,
                state == "LICENSED", state != "LICENSED");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException
            or TimeoutException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new("Microsoft 365 subscription",
                T("Could not verify subscription activation", "Не удалось проверить активацию подписки",
                    "לא ניתן לאמת מנוי"),
                "Microsoft local tool: " + ex.GetType().Name, false, true);
        }
    }

    internal static bool SelfTest() =>
        InterpretVNextState("License State: Licensed") == "LICENSED" &&
        InterpretVNextState("License State: Unlicensed") == "UNLICENSED" &&
        InterpretVNextState("Activation Status: Grace") == "GRACE" &&
        InterpretVNextState("Email: somebody@example.test") == "UNKNOWN" &&
        InterpretVNextState("License State: xxx person@example.test") == "UNKNOWN" &&
        InterpretVNextState(null) == "UNKNOWN";
}
