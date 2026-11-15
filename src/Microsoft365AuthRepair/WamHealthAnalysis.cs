using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

internal enum WamHealthLevel { Healthy, AttentionRequired, RepairRecommended }

// Short on-screen findings are separate from the complete technical report.
internal sealed record WamHealthResult(WamHealthLevel Level, string Summary, string Details)
{
    internal IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> NextSteps { get; init; } = Array.Empty<string>();
}

internal static class WamHealthAnalysis
{
    private static string T(string en, string ru, string he) =>
        UpdateUiText.T(en, ru, he);

    private static string ReadValue(string report, string key)
    {
        var match = Regex.Match(report, @"(?im)^\s*" + Regex.Escape(key) +
            @"\s*:\s*([^\r\n]*)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim().ToUpperInvariant() : "UNKNOWN";
    }

    private static bool Begins(string value, string prefix) =>
        value.Equals(prefix, StringComparison.Ordinal) ||
        value.StartsWith(prefix + " ", StringComparison.Ordinal) ||
        value.StartsWith(prefix + " (", StringComparison.Ordinal);

    // No directory or Microsoft Graph queries and no login attempts are made.
    // This is a conservative interpretation of the read-only local probe.
    internal static WamHealthResult Evaluate(string raw, bool currentDesktopContext)
    {
        var broker = ReadValue(raw, "BrokerPlugin");
        var joined = ReadValue(raw, "AzureAdJoined");
        var domain = ReadValue(raw, "DomainJoined");
        var auth = ReadValue(raw, "DeviceAuthStatus");
        var prt = ReadValue(raw, "AzureAdPrt");
        var wam = ReadValue(raw, "WamDefaultSet");
        var hasCloudRejection = raw.Contains("AADSTS50155", StringComparison.OrdinalIgnoreCase);
        var brokerMissing = Begins(broker, "MISSING");
        var brokerPresent = Begins(broker, "PRESENT");
        var cloudFailure = hasCloudRejection || Begins(auth, "FAILED");
        var missingData = !brokerPresent && !brokerMissing ||
            joined is not ("YES" or "NO") ||
            prt is not ("YES" or "NO") ||
            wam is not ("YES" or "NO") ||
            (joined == "YES" && auth != "SUCCESS" && !cloudFailure);

        var findings = new List<string>();
        var advice = new List<string>();

        if (!currentDesktopContext)
        {
            findings.Add(T(
                "This check may be running under a different administrator rather than the affected desktop user; WAM and PRT results may be misleading.",
                "Проверка может выполняться от другой учётной записи администратора, а не проблемного пользователя. Результаты WAM и PRT могут быть неверными.",
                "הבדיקה עשויה להתבצע מחשבון מנהל אחר. תוצאות WAM ו-PRT עלולות להיות מטעות."));
            advice.Add(T(
                "Run the app as the affected Windows user (without elevation) and check again.",
                "Запустите программу от имени проблемного пользователя Windows без повышения прав и повторите проверку.",
                "הפעל את התוכנה בחשבון המשתמש הרלוונטי ללא הרשאות מנהל ובדוק שוב."));
        }

        if (cloudFailure)
        {
            findings.Add(T(
                "Entra device authentication is failing. Re-registering WAM or resetting Office caches cannot repair device trust.",
                "Ошибка аутентификации устройства Entra. Перерегистрация WAM или сброс кеша Office не восстановят доверие к устройству.",
                "אימות המכשיר מול Entra נכשל. רישום WAM או ניקוי מטמון Office לא יתקנו את אמון המכשיר."));
            advice.Add(T(
                "Contact IT to verify the Entra device record, hybrid join and device authentication. Do not reset Office as the first step.",
                "Обратитесь в IT: проверьте регистрацию устройства в Entra, Hybrid Join и аутентификацию устройства. Не начинайте со сброса Office.",
                "פנה לתמיכה לבדיקת רישום המכשיר ב-Entra ואימות המכשיר. אל תאפס Office כצעד ראשון."));
        }

        if (brokerMissing)
        {
            findings.Add(T(
                "The AAD BrokerPlugin is not registered for the checked Windows account.",
                "AAD BrokerPlugin не зарегистрирован для проверяемого пользователя Windows.",
                "התוסף AAD BrokerPlugin אינו רשום עבור חשבון Windows שנבדק."));
            advice.Add(T(
                "Use Re-register WAM for the affected user; this does not delete Office sign-in caches.",
                "Нажмите «Re-register WAM» для проблемного пользователя. Это не удаляет кеши входа Office.",
                "הפעל רישום WAM מחדש עבור המשתמש הרלוונטי. פעולה זו אינה מוחקת מטמון כניסה."));
        }
        else if (!brokerPresent)
        {
            findings.Add(T(
                "BrokerPlugin could not be verified; the AppX check may have been blocked.",
                "Невозможно проверить BrokerPlugin; проверка AppX могла быть заблокирована.",
                "לא ניתן לבדוק את BrokerPlugin. ייתכן שבדיקת AppX נחסמה."));
            advice.Add(T(
                "Run the check as the affected user. If it stays unavailable, ask IT to inspect AppX/PowerShell restrictions.",
                "Повторите проверку под нужным пользователем. Если не удаётся — проверьте ограничения AppX/PowerShell с IT.",
                "הפעל את הבדיקה בחשבון הרלוונטי. אם הבדיקה עדיין חסומה פנה לתמיכה."));
        }

        if (prt == "NO")
        {
            findings.Add(T(
                "The checked session has no Azure AD Primary Refresh Token (PRT). This does not prove WAM is broken.",
                "В сеансе нет Azure AD PRT. Само по себе это не означает неисправность WAM.",
                "לא נמצא PRT בהפעלה זו. אין בכך הוכחה לתקלה ב-WAM."));
            advice.Add(T(
                "Check work network/VPN and Windows sign-in, then sign out and back in. If PRT is still absent, involve IT.",
                "Проверьте корпоративную сеть/VPN и вход в Windows, затем выйдите и войдите снова. Если PRT не появился — обратитесь в IT.",
                "בדוק רשת עבודה או VPN, התחבר מחדש ל-Windows ופנה לתמיכה אם ה-PRT עדיין חסר."));
        }

        if (wam == "NO")
        {
            findings.Add(T(
                "No default WAM account is set for this session. Microsoft 365 sign-in may need user action.",
                "В сеансе отсутствует учётная запись WAM по умолчанию. Возможно, потребуется повторный вход в Microsoft 365.",
                "לא הוגדר חשבון WAM כברירת מחדל. ייתכן שיידרש חיבור מחדש ל-Microsoft 365."));
            advice.Add(T(
                "Try signing in to an affected Microsoft 365 app. If it still fails, consider Re-register WAM before any cache reset.",
                "Попробуйте войти в проблемное приложение Microsoft 365. Если ошибка остаётся — выполните Re-register WAM до сброса кешей.",
                "נסה להתחבר ליישום Microsoft 365. אם יש תקלה, בצע רישום WAM מחדש לפני איפוס מטמון."));
        }

        if (joined == "NO")
        {
            findings.Add(T(
                "This Windows installation is not Entra joined. That can be expected for some personal/on-premises configurations.",
                "Windows не присоединена к Entra ID. Для некоторых личных и локальных конфигураций это нормально.",
                "המחשב אינו מצורף ל-Entra ID. זה עשוי להיות תקין בהגדרות מסוימות."));
            advice.Add(T(
                "If your organization requires Entra or hybrid join, ask IT to check the device enrollment; do not use a WAM cache reset to join it.",
                "Если организация требует Entra или Hybrid Join, попросите IT проверить регистрацию устройства. Сброс WAM не подключает компьютер к Entra.",
                "אם הארגון דורש צירוף ל-Entra, פנה לתמיכה. איפוס WAM לא מחבר את המחשב."));
        }

        if (missingData)
        {
            findings.Add(T(
                "At least one prerequisite could not be verified. Do not treat missing values as healthy.",
                "Не удалось проверить часть параметров. Отсутствующие значения нельзя считать исправными.",
                "לא ניתן לאמת חלק מהנתונים. ערכים חסרים אינם מעידים על מצב תקין."));
            if (advice.Count == 0)
                advice.Add(T(
                    "Check Windows network/device registration and run the diagnostic again.",
                    "Проверьте сеть и регистрацию устройства Windows, затем повторите диагностику.",
                    "בדוק את הרשת ואת רישום המכשיר והפעל את האבחון שוב."));
        }

        WamHealthLevel level;
        if (currentDesktopContext && brokerMissing && !cloudFailure)
            level = WamHealthLevel.RepairRecommended;
        else if (currentDesktopContext && brokerPresent && joined == "YES" &&
            auth == "SUCCESS" && prt == "YES" && wam == "YES" && !hasCloudRejection)
            level = WamHealthLevel.Healthy;
        else
            level = WamHealthLevel.AttentionRequired;

        var summary = level switch
        {
            WamHealthLevel.Healthy => T(
                "Healthy prerequisites — verify sign-in inside an actual Microsoft 365 app",
                "Основные компоненты исправны — проверьте вход в приложении Microsoft 365",
                "רכיבי ההתחברות נראים תקינים. בדוק התחברות באפליקציית Microsoft 365"),
            WamHealthLevel.RepairRecommended => T(
                "Repair recommended — AAD BrokerPlugin registration is missing",
                "Рекомендуется восстановление — отсутствует регистрация AAD BrokerPlugin",
                "מומלץ תיקון — רישום AAD BrokerPlugin חסר"),
            _ => T(
                "Attention required — see findings and next steps",
                "Требуется внимание — смотрите причины и действия",
                "נדרשת בדיקה — פירוט וסדר פעולות בהמשך")
        };

        if (level == WamHealthLevel.Healthy)
        {
            findings.Add(T(
                "Basic device/WAM signals are present; this check does not test Office, Teams or OneDrive sign-in.",
                "Основные признаки исправности есть, но вход в Office, Teams и OneDrive здесь НЕ проверяется.",
                "נמצאו סימנים תקינים. הבדיקה אינה בודקת בפועל כניסה ל-Office, Teams או OneDrive."));
            advice.Add(T(
                "Try signing in to the affected Microsoft 365 application. If it fails, Re-register WAM is a low-impact first repair; Reset Office sign-in is a last resort.",
                "Попробуйте войти в проблемное приложение Microsoft 365. При ошибке сначала используйте Re-register WAM; Reset Office sign-in — крайняя мера.",
                "נסה להתחבר ליישום Microsoft 365. אם יש תקלה, עדיף לנסות רישום WAM מחדש לפני איפוס Office."));
        }

        var sb = new StringBuilder();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine(T("WHAT WE FOUND", "РЕЗУЛЬТАТЫ", "תוצאות"));
        sb.AppendLine("  AAD BrokerPlugin: " + broker);
        sb.AppendLine("  AzureAdJoined: " + joined);
        sb.AppendLine("  DomainJoined: " + domain);
        sb.AppendLine("  DeviceAuthStatus: " + auth);
        sb.AppendLine("  AzureAdPrt: " + prt);
        sb.AppendLine("  WamDefaultSet: " + wam);
        sb.AppendLine();
        foreach (var item in findings) sb.AppendLine("• " + item);
        sb.AppendLine();
        sb.AppendLine(T("WHAT TO DO NEXT", "ЧТО ДЕЛАТЬ ДАЛЬШЕ", "מה לעשות עכשיו"));
        for (var i = 0; i < advice.Count; i++) sb.AppendLine((i + 1) + ". " + advice[i]);
        sb.AppendLine();
        sb.Append(T(
            "Read-only check. No sign-in data was changed or uploaded.",
            "Проверка без изменений. Данные авторизации не изменены и никуда не отправлены.",
            "בדיקה לקריאה בלבד. לא בוצעו שינויים ולא נשלח מידע."));

        return new WamHealthResult(level, summary, sb.ToString())
        {
            Findings = findings.ToArray(),
            NextSteps = advice.ToArray()
        };
    }

    internal static bool SelfTest()
    {
        const string healthy = "BrokerPlugin: PRESENT (1.0)\nAzureAdJoined : YES\n" +
            "DomainJoined : YES\nDeviceAuthStatus : SUCCESS\nAzureAdPrt : YES\nWamDefaultSet : YES";
        const string missing = "BrokerPlugin: MISSING\nAzureAdJoined : YES\n" +
            "DeviceAuthStatus : SUCCESS\nAzureAdPrt : YES\nWamDefaultSet : YES";
        const string rejected = "BrokerPlugin: PRESENT\nAzureAdJoined : YES\n" +
            "DeviceAuthStatus : FAILED\nAzureAdPrt : NO\nWamDefaultSet : YES\nAADSTS50155";
        return Evaluate(healthy, true).Level == WamHealthLevel.Healthy &&
            Evaluate(healthy, true).NextSteps.Count > 0 &&
            Evaluate(healthy, true).Findings.Count > 0 &&
            Evaluate(missing, true).Level == WamHealthLevel.RepairRecommended &&
            Evaluate(missing, true).NextSteps.Count > 0 &&
            Evaluate(rejected, true).Level == WamHealthLevel.AttentionRequired &&
            Evaluate(healthy, false).Level == WamHealthLevel.AttentionRequired &&
            Evaluate("", true).Level == WamHealthLevel.AttentionRequired &&
            !Evaluate(healthy, true).Details.Contains("Reset now", StringComparison.Ordinal);
    }
}
