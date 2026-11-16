using System.Globalization;

namespace Microsoft365AuthRepair;

internal static class UiStrings
{
    internal static string Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "ru" => "ru", "he" => "he", _ => "en"
    };

    internal static string T(string id)
    {
        string en = id switch
        {
            "title" => "Microsoft 365 Auth Repair",
            "subtitle" => "Microsoft Entra WAM / AAD Broker Plugin / Office sign-in",
            "repair" => "Re-register WAM",
            "full" => "Reset Office sign-in",
            "diagnose" => "Check Entra / WAM",
            "updater" => "Updates",
            "debug" => "Diagnostics & reports",
            "logs" => "Open local logs",
            "scope" => "User scope",
            "current" => "Current user",
            "all" => "All users on this PC",
            "statusReady" => "Ready. Recommended repair re-registers WAM without deleting tokens.",
            "running" => "Working. Some Microsoft applications may close.",
            "done" => "Operation completed. Check your Microsoft 365 sign-in.",
            "failed" => "The operation reported an error. Open local logs for details.",
            "advanced" => "Advanced: full reset deletes Office/WAM sign-in caches and requires sign-in again.",
            "confirmTitle" => "Confirm full identity reset",
            "confirmCurrent" => "Delete Microsoft 365 sign-in caches for the current Windows user? This may sign you out of Office, Teams and OneDrive.",
            "confirmAll" => "Schedule identity reset for ALL local user profiles, including domain users? Online users may be signed out. Offline profiles will run at next sign-in.",
            "admin" => "Administrator: all-users tasks run within each user session.",
            "standard" => "Standard user: current-user repair needs no admin. All users needs UAC.",
            "updateAvailable" => "Update available",
            "none" => "No eligible release detected",
            _ => id
        };
        if (Language == "en") return en;
        return (Language, id) switch
        {
            ("ru", "title") => "Microsoft 365 Auth Repair",
            ("ru", "subtitle") => "Microsoft Entra WAM / AAD Broker Plugin / \u0432\u0445\u043e\u0434 \u0432 Office",
            ("ru", "repair") => "Перерегистрировать WAM",
            ("ru", "full") => "Сбросить вход Office",
            ("ru", "diagnose") => "Проверить Entra / WAM",
            ("ru", "updater") => "\u041e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u044f",
            ("ru", "debug") => "\u0414\u0438\u0430\u0433\u043d\u043e\u0441\u0442\u0438\u043a\u0430 \u0438 \u043e\u0442\u0447\u0451\u0442\u044b",
            ("ru", "logs") => "\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u0436\u0443\u0440\u043d\u0430\u043b\u044b",
            ("ru", "scope") => "\u0414\u043b\u044f \u043a\u043e\u0433\u043e",
            ("ru", "current") => "Текущий пользователь",
            ("ru", "all") => "Все пользователи этого ПК",
            ("ru", "statusReady") => "\u0413\u043e\u0442\u043e\u0432\u043e. \u0412\u043e\u0441\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0435 WAM \u043d\u0435 \u0443\u0434\u0430\u043b\u044f\u0435\u0442 \u0442\u043e\u043a\u0435\u043d\u044b.",
            ("ru", "running") => "\u0412\u044b\u043f\u043e\u043b\u043d\u044f\u0435\u0442\u0441\u044f. \u041f\u0440\u0438\u043b\u043e\u0436\u0435\u043d\u0438\u044f Microsoft \u043c\u043e\u0433\u0443\u0442 \u0437\u0430\u043a\u0440\u044b\u0442\u044c\u0441\u044f.",
            ("ru", "done") => "\u0413\u043e\u0442\u043e\u0432\u043e. \u041f\u0440\u043e\u0432\u0435\u0440\u044c\u0442\u0435 \u0432\u0445\u043e\u0434 \u0432 Microsoft 365.",
            ("ru", "failed") => "\u041e\u0448\u0438\u0431\u043a\u0430. \u0421\u043c\u043e\u0442\u0440\u0438\u0442\u0435 \u043b\u043e\u043a\u0430\u043b\u044c\u043d\u044b\u0439 \u0436\u0443\u0440\u043d\u0430\u043b.",
            ("ru", "advanced") => "\u041f\u043e\u043b\u043d\u044b\u0439 \u0441\u0431\u0440\u043e\u0441 \u0443\u0434\u0430\u043b\u0438\u0442 \u043a\u0435\u0448\u0438 \u0432\u0445\u043e\u0434\u0430 Office/WAM.",
            ("ru", "confirmTitle") => "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u0435 \u043f\u043e\u043b\u043d\u044b\u0439 \u0441\u0431\u0440\u043e\u0441",
            ("ru", "confirmCurrent") => "\u0423\u0434\u0430\u043b\u0438\u0442\u044c \u043a\u0435\u0448\u0438 \u0432\u0445\u043e\u0434\u0430 Microsoft 365 \u0443 \u0442\u0435\u043a\u0443\u0449\u0435\u0433\u043e \u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044f?",
            ("ru", "confirmAll") => "\u041d\u0430\u0437\u043d\u0430\u0447\u0438\u0442\u044c \u0441\u0431\u0440\u043e\u0441 \u0432\u0441\u0435\u043c \u043f\u0440\u043e\u0444\u0438\u043b\u044f\u043c, \u0432\u043a\u043b\u044e\u0447\u0430\u044f \u0434\u043e\u043c\u0435\u043d\u043d\u044b\u0435?",
            ("ru", "admin") => "\u0410\u0434\u043c\u0438\u043d\u0438\u0441\u0442\u0440\u0430\u0442\u043e\u0440: \u0432\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044f.",
            ("ru", "standard") => "\u0411\u0435\u0437 \u043f\u0440\u0430\u0432 \u0430\u0434\u043c\u0438\u043d\u0430: \u0442\u043e\u043b\u044c\u043a\u043e \u0442\u0435\u043a\u0443\u0449\u0438\u0439 \u043f\u0440\u043e\u0444\u0438\u043b\u044c.",
            ("he", "title") => "Microsoft 365 Auth Repair",
            ("he", "repair") => "רישום WAM מחדש",
            ("he", "full") => "איפוס התחברות Office",
            ("he", "diagnose") => "בדיקת Entra / WAM",
            ("he", "updater") => "\u05e2\u05d3\u05db\u05d5\u05e0\u05d9\u05dd",
            ("he", "debug") => "\u05d0\u05d1\u05d7\u05d5\u05df \u05d5\u05d3\u05d5\u05d7\u05d5\u05ea",
            ("he", "logs") => "\u05e4\u05ea\u05d7 \u05d9\u05d5\u05de\u05e0\u05d9\u05dd",
            ("he", "scope") => "\u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd",
            ("he", "current") => "המשתמש הנוכחי",
            ("he", "all") => "כל המשתמשים במחשב זה",
            ("he", "statusReady") => "\u05de\u05d5\u05db\u05df \u05dc\u05ea\u05d9\u05e7\u05d5\u05df WAM \u05dc\u05dc\u05d0 \u05de\u05d7\u05d9\u05e7\u05ea \u05d0\u05e1\u05d9\u05de\u05d5\u05e0\u05d9\u05dd.",
            ("he", "running") => "\u05de\u05d1\u05e6\u05e2 \u05ea\u05d9\u05e7\u05d5\u05df...",
            ("he", "done") => "\u05d4\u05e1\u05ea\u05d9\u05d9\u05dd. \u05d1\u05d3\u05d5\u05e7 \u05d0\u05ea \u05d4\u05d4\u05ea\u05d7\u05d1\u05e8\u05d5\u05ea.",
            ("he", "failed") => "\u05e9\u05d2\u05d9\u05d0\u05d4. \u05e2\u05d9\u05d9\u05df \u05d1\u05d9\u05d5\u05de\u05df.",
            ("he", "advanced") => "\u05d0\u05d9\u05e4\u05d5\u05e1 \u05de\u05dc\u05d0 \u05d9\u05de\u05d7\u05e7 \u05e0\u05ea\u05d5\u05e0\u05d9 \u05d4\u05ea\u05d7\u05d1\u05e8\u05d5\u05ea Office/WAM.",
            ("he", "confirmTitle") => "\u05d0\u05d9\u05e9\u05d5\u05e8 \u05d0\u05d9\u05e4\u05d5\u05e1",
            ("he", "confirmCurrent") => "\u05dc\u05de\u05d7\u05d5\u05e7 \u05e0\u05ea\u05d5\u05e0\u05d9 \u05d4\u05d6\u05d4\u05d5\u05d9 \u05e9\u05dc \u05d4\u05de\u05e9\u05ea\u05de\u05e9?",
            ("he", "confirmAll") => "\u05dc\u05d0\u05e4\u05e1 \u05d0\u05ea \u05db\u05dc \u05e4\u05e8\u05d5\u05e4\u05d9\u05dc\u05d9 \u05d4\u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd?",
            ("he", "admin") => "\u05de\u05e0\u05d4\u05dc: \u05d1\u05d7\u05e8 \u05ea\u05d7\u05d5\u05dd \u05e4\u05e2\u05d5\u05dc\u05d4.",
            ("he", "standard") => "\u05de\u05e9\u05ea\u05de\u05e9 \u05e8\u05d2\u05d9\u05dc: \u05d4\u05e4\u05e8\u05d5\u05e4\u05d9\u05dc \u05d4\u05e0\u05d5\u05db\u05d7\u05d9 \u05d1\u05dc\u05d1\u05d3.",
            _ => en
        };
    }
}
