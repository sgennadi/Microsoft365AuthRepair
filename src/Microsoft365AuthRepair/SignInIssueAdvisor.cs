using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Offline-only interpretation of Microsoft 365 sign-in error codes.
// Never receives credentials, uses no HTTP/Graph, makes no account changes.
internal enum SignInIssueScope { LocalRepair, UserAction, Administrator, Investigation }
internal sealed record SignInIssue(string Code, string Title, string Cause,
    string NextStep, SignInIssueScope Scope);

internal static class SignInIssueAdvisor
{
    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);

    internal static SignInIssue Explain(string text)
    {
        // Match only a known error-code shape; do not show/retain arbitrary
        // pasted account names, tenant IDs, request IDs, tokens or log text.
        string code = ExtractCode(text);
        return code switch
        {
            "1001" => new("1001",
                T("Desktop Microsoft 365 sign-in failure", "Сбой входа в приложениях Microsoft 365", "כשל כניסה ביישומי Microsoft 365"),
                T("A common cause is WAM/BrokerPlugin interference by security software or unsupported roaming of identity profile data. It does not prove any single cause.",
                  "Частые причины: сбой WAM/BrokerPlugin, блокировка защитным ПО или некорректный перенос данных профиля. Сам по себе код не доказывает причину.",
                  "גורמים אפשריים: WAM/BrokerPlugin, תוכנת אבטחה או נדידת פרופיל שאינה נתמכת."),
                T("Try Re-register WAM for the affected Windows user; inspect protection software and VDI/FSLogix profile exclusions. Do not erase TPM or disconnect the device.",
                  "Попробуйте Re-register WAM для проблемного пользователя. Проверьте защитное ПО и исключения для VDI/FSLogix. Не очищайте TPM и не отключайте устройство от Entra.",
                  "נסה רישום WAM מחדש למשתמש, ובדוק תוכנת אבטחה ופרופיל VDI. אל תמחק TPM ואל תנתק את המכשיר."),
                SignInIssueScope.LocalRepair),
            "AADSTS50155" => new(code,
                T("Entra device authentication rejected", "Entra отклоняет аутентификацию устройства", "אימות המכשיר ב-Entra נדחה"),
                T("The device identity cannot be authenticated to obtain a PRT. Device may be disabled, deleted, or not properly registered.",
                  "Устройство не может пройти проверку для получения PRT. Возможно, его запись отключена, удалена или регистрация нарушена.",
                  "זהות המכשיר לא אומתה לקבלת PRT; ייתכן שהרישום אינו תקין."),
                T("Ask IT to check the Entra device record and join type. WAM re-registration or Office cache resets cannot restore device trust.",
                  "Проверьте в IT запись устройства и тип Entra Join. Перерегистрация WAM и очистка кешей Office не восстановят доверие к устройству.",
                  "על התמיכה לבדוק את רישום המכשיר ואת סוג הצירוף. ניקוי מטמון Office לא יתקן זאת."),
                SignInIssueScope.Administrator),
            "AADSTS53000" or "AADSTS53001" or "AADSTS53003" or "AADSTS50131" => new(code,
                T("Conditional Access / device requirement", "Политика Conditional Access / требования к устройству", "דרישת מכשיר או גישה מותנית"),
                T("Sign-in is blocked by a tenant access policy, device compliance/join requirement, or related restriction.",
                  "Доступ ограничен политикой арендатора, соответствием устройства или требованием присоединения к домену/Entra.",
                  "מדיניות גישה, תאימות מכשיר או צירוף ארגוני חוסמים כניסה."),
                T("Review this event in Entra Sign-in Logs and its Conditional Access tab. Do not bypass MFA, compliance or the policy using local registry edits.",
                  "Откройте событие в Entra Sign-in Logs, вкладку Conditional Access. Не обходите MFA, требования соответствия или политики изменением реестра.",
                  "בדוק יומני כניסה ב-Entra ואת מדיניות הגישה. אין לעקוף MFA או מדיניות."),
                SignInIssueScope.Administrator),
            "AADSTS50076" or "AADSTS50079" or "AADSTS50072" or "AADSTS500121" => new(code,
                T("MFA verification or enrollment required", "Необходима проверка MFA или регистрация", "נדרש אימות MFA או רישום"),
                T("The user needs to satisfy the multifactor authentication prompt or finish registering security information.",
                  "Нужно завершить подтверждение MFA либо регистрацию способа проверки личности.",
                  "נדרש להשלים אימות רב-שלבי או רישום פרטי אבטחה."),
                T("Complete the approved MFA flow in the browser/app. If registration is blocked, contact IT. Resetting Office tokens does not remove MFA requirements.",
                  "Завершите MFA в приложении или браузере. Если регистрация запрещена — обратитесь в IT. Сброс токенов Office не отменяет MFA.",
                  "השלם אימות MFA. אם הרישום חסום, פנה לתמיכה. איפוס Office לא מבטל MFA."),
                SignInIssueScope.UserAction),
            "AADSTS50126" or "AADSTS50055" or "AADSTS50057" or "AADSTS50034" => new(code,
                T("Account, credentials or tenant problem", "Ошибка учётной записи, пароля или арендатора", "בעיה בחשבון, סיסמה או דייר"),
                T("The account may be missing in the tenant, disabled, have an expired password, or be using incorrect credentials; the exact cause depends on the code.",
                  "Причина зависит от кода: неверные данные входа, истёкший пароль, отключённый пользователь или отсутствие учётной записи в нужном tenant.",
                  "החשבון עשוי להיות חסום, הסיסמה שגויה או שפג תוקפה, או שהחשבון אינו קיים בדייר."),
                T("Check the username/UPN and correct tenant; renew expired password or ask IT to verify account status and directory sync. WAM reset is not the first fix.",
                  "Проверьте UPN и tenant, обновите истёкший пароль или попросите IT проверить статус и синхронизацию учётной записи. WAM сбрасывать сначала не нужно.",
                  "בדוק שם משתמש והדייר, חידוש סיסמה וסטטוס חשבון עם התמיכה."),
                SignInIssueScope.UserAction),
            "AADSTS50058" or "AADSTS70046" => new(code,
                T("Incomplete or expired sign-in session", "Неполный вход или истекшая сессия", "התחברות לא מלאה או שפג תוקפה"),
                T("A silent sign-in may lack an authenticated session, or session reauthentication is required. This can occur normally and is not proof that WAM is broken.",
                  "Не хватает подтверждённой сессии или требуется повторный вход. Это не всегда неисправность WAM.",
                  "ייתכן שנדרש אימות מחדש. אין בכך הוכחה לתקלה ב-WAM."),
                T("Sign in interactively to the affected app and complete any prompted verification. Check Entra sign-in logs only if the failure repeats.",
                  "Войдите в приложение вручную и завершите запрошенное подтверждение. При повторении проверьте журналы входа Entra.",
                  "התחבר מחדש ליישום והשלים את האימות. בדוק יומני Entra אם התקלה חוזרת."),
                SignInIssueScope.UserAction),
            "80090016" => new(code,
                T("TPM / keyset problem reported", "Возможная ошибка TPM / ключей", "בעיה אפשרית ב-TPM או במפתחות"),
                T("This HRESULT is often associated with a missing/unavailable cryptographic keyset. The code alone cannot identify whether the TPM, Windows Hello or profile is responsible.",
                  "Этот код часто связан с отсутствующим криптографическим набором ключей. Он не доказывает неисправность самого TPM.",
                  "הקוד עשוי להעיד על בעיית מפתחות, אך לא מוכיח תקלה ב-TPM."),
                T("Check Windows Hello, device identity and user profile with IT. Preserve BitLocker recovery keys. Do NOT clear the TPM, remove device registration or delete key containers automatically.",
                  "Проверьте Windows Hello, регистрацию устройства и профиль вместе с IT. Сохраните ключи восстановления BitLocker. Не очищайте TPM и не удаляйте криптографические контейнеры автоматически.",
                  "בדוק Windows Hello ורישום המכשיר עם התמיכה. שמור מפתחות BitLocker. אין לנקות TPM אוטומטית."),
                SignInIssueScope.Administrator),
            "80090011" => new(code,
                T("Cryptographic object not found", "Криптографический объект не найден", "אובייקט קריפטוגרפי חסר"),
                T("Windows reports NTE_NOT_FOUND. This alone does not identify the missing object or prove a physical TPM failure.",
                  "Windows сообщает NTE_NOT_FOUND. По коду нельзя определить объект или доказать поломку TPM.",
                  "הקוד מציין אובייקט חסר, לא בהכרח תקלה פיזית ב-TPM."),
                T("Review Windows Hello registration, user context and Entra device key diagnostics before modifying any key containers.",
                  "Проверьте Windows Hello, контекст пользователя и ключ устройства Entra. Не удаляйте контейнеры ключей.",
                  "בדוק Windows Hello ומפתח Entra ללא מחיקת מפתחות."),
                SignInIssueScope.Administrator),
            "80090030" => new(code,
                T("Cryptographic device not ready", "Криптографическое устройство не готово", "התקן קריפטוגרפי אינו מוכן"),
                T("NTE_DEVICE_NOT_READY means the required cryptographic device is not ready; it is not proof of permanent TPM hardware failure.",
                  "NTE_DEVICE_NOT_READY — необходимое криптографическое устройство не готово. Это не доказывает поломку TPM.",
                  "התקן ההצפנה אינו מוכן; אין הוכחה לכשל חומרה קבוע."),
                T("Check TPM readiness and firmware state with IT, plus KeySignTest when available. Do not clear TPM as an automatic fix.",
                  "Проверьте готовность TPM и прошивку, а также KeySignTest при наличии прав. Не очищайте TPM автоматически.",
                  "בדוק מוכנות TPM וקושחה; אין לנקות TPM אוטומטית."),
                SignInIssueScope.Administrator),
            "80090031" => new(code,
                T("TPM lockout / dictionary attack protection", "Блокировка TPM из-за защиты от подбора", "חסימת TPM בהגנה מפני ניחוש"),
                T("NTE_AUTHENTICATION_IGNORED indicates that the provider is enforcing dictionary-attack protection. This can be transient.",
                  "NTE_AUTHENTICATION_IGNORED означает срабатывание защиты от подбора. Блокировка бывает временной.",
                  "הופעלה הגנה מניסיונות התחברות חוזרים."),
                T("Stop repeated attempts and allow the documented cooldown; ask IT to check Get-Tpm LockedOut and tpm.msc. Never reset TPM, BitLocker protectors or Windows Hello keys automatically.",
                  "Прекратите повторные попытки, дождитесь снятия блокировки, проверьте Get-Tpm LockedOut и tpm.msc. Не сбрасывайте TPM и ключи автоматически.",
                  "המתן לסיום הנעילה ובדוק מצב TPM; אין למחוק מפתחות."),
                SignInIssueScope.Administrator),
            "80090034" => new(code,
                T("Cryptographic encryption failed", "Сбой криптографического шифрования", "כשל בהצפנה"),
                T("NTE_ENCRYPTION_FAILURE describes a failed encryption operation; it does not prove that TPM hardware, Windows Hello or WAM is the root cause.",
                  "NTE_ENCRYPTION_FAILURE — ошибка операции шифрования, которая сама по себе не указывает на TPM, Windows Hello или WAM.",
                  "ההצפנה נכשלה; הקוד לבדו אינו מזהה את המקור."),
                T("Review TPM readiness, device-key diagnostics and the affected Windows user's sign-in context. Preserve BitLocker recovery information. Do not disable WAM/ADAL or clear TPM.",
                  "Проверьте TPM, ключи устройства и сеанс пользователя. Сохраните BitLocker recovery. Не отключайте WAM/ADAL и не очищайте TPM.",
                  "בדוק TPM ומפתחות מכשיר; אל תכבה WAM/ADAL ואל תנקה TPM."),
                SignInIssueScope.Administrator),
            "AADSTS50127" => new(code,
                T("Broker application required", "Требуется компонент брокера авторизации", "נדרש רכיב Broker"),
                T("The requested sign-in requires a broker application that isn't available to the client.",
                  "Для входа требуется компонент брокера, который недоступен клиенту.",
                  "יישום Broker הדרוש להתחברות אינו זמין."),
                T("Check WAM/BrokerPlugin registration for the affected Windows user and try Re-register WAM if missing. Recheck the error afterward.",
                  "Проверьте регистрацию WAM/BrokerPlugin у проблемного пользователя; при отсутствии попробуйте Re-register WAM и повторите проверку.",
                  "בדוק WAM/BrokerPlugin ובמידת הצורך רשום אותו מחדש למשתמש."),
                SignInIssueScope.LocalRepair),
            _ => new(code.Length == 0 ? "UNKNOWN" : code,
                T("No verified match", "Код не определён достоверно", "לא נמצא קוד מזוהה"),
                T("Enter a displayed AADSTS code, 1001, or 0x800900xx code. A generic error message does not identify the cause.",
                  "Введите AADSTS-код, 1001 или 0x800900xx. Общая надпись не позволяет установить причину.",
                  "הזן קוד AADSTS, 1001 או 0x800900xx. הודעה כללית אינה מספיקה."),
                T("Use Check & Diagnose for local WAM/PRT signals. For a tenant-side failure ask IT for the matching Entra Sign-in Log entry.",
                  "Используйте Check & Diagnose для WAM/PRT. Для серверной ошибки запросите соответствующую запись Entra Sign-in Logs у IT.",
                  "בדוק WAM/PRT והיעזר ביומני כניסה של Entra."),
                SignInIssueScope.Investigation)
        };
    }

    // Codes supported by the local guide. Do not suggest unknown codes as
    // verified fixes, and never query a remote service as the user types.
    private static readonly string[] KnownCodes =
    [
        "1001", "80090011", "80090016", "80090030", "80090031", "80090034",
        "AADSTS50034", "AADSTS50055", "AADSTS50057", "AADSTS50058",
        "AADSTS50072", "AADSTS50076", "AADSTS50079", "AADSTS500121",
        "AADSTS50126", "AADSTS50127", "AADSTS50131", "AADSTS50155",
        "AADSTS53000", "AADSTS53001", "AADSTS53003", "AADSTS70046"
    ];

    internal static IReadOnlyList<string> MatchingCodes(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        var prefix = input.Trim().ToUpperInvariant();
        if (prefix.StartsWith("AADSTS", StringComparison.Ordinal))
            prefix = prefix["AADSTS".Length..];
        else if (prefix.StartsWith("0X", StringComparison.Ordinal))
            prefix = prefix[2..];
        // Avoid echoing or retaining pasted account data, tokens or logs.
        if (prefix.Length == 0 || prefix.Length > 8 ||
            !Regex.IsMatch(prefix, @"^\d{1,8}$", RegexOptions.CultureInvariant))
            return [];

        return KnownCodes
            .Where(x => (x.StartsWith("AADSTS", StringComparison.Ordinal)
                    ? x["AADSTS".Length..] : x).StartsWith(prefix, StringComparison.Ordinal))
            .Take(7).ToArray();
    }

    internal static string ExtractCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim();
        var aad = Regex.Match(normalized, @"(?i)\bAADSTS(?<code>\d{5,6})\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (aad.Success) return "AADSTS" + aad.Groups["code"].Value;
        // Only a *whole* 5- or 6-digit number is accepted without AADSTS prefix.
        if (Regex.IsMatch(normalized, @"^\d{5,6}$", RegexOptions.CultureInvariant))
            return "AADSTS" + normalized;
        var crypto = Regex.Match(normalized, @"(?i)(?:^|\b)(?:0x)?(?<code>800900(?:11|16|30|31|34))\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (crypto.Success) return crypto.Groups["code"].Value;
        if (Regex.IsMatch(normalized, @"(?i)(?:^1001$|(?:error|ошибка|שגיאה)\W*1001\b|\[\s*1001\s*\])",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            return "1001";
        return "";
    }

    internal static bool SelfTest()
    {
        return MatchingCodes("AADSTS530").Contains("AADSTS53003") &&
            MatchingCodes("530").Count == 3 &&
            MatchingCodes("AADSTS5007").Contains("AADSTS50076") &&
            MatchingCodes("0x8009").Contains("80090016") &&
            MatchingCodes("8009003").Contains("80090030") &&
            MatchingCodes("email@example.com").Count == 0 &&
            MatchingCodes("").Count == 0 &&
            ExtractCode("AADSTS53003: blocked") == "AADSTS53003" &&
            Explain("AADSTS53003: blocked").Scope == SignInIssueScope.Administrator &&
            Explain("AADSTS50076").Scope == SignInIssueScope.UserAction &&
            Explain("AADSTS50155").Scope == SignInIssueScope.Administrator &&
            Explain("Something went wrong [1001]").Scope == SignInIssueScope.LocalRepair &&
            Explain("0x80090016").Scope == SignInIssueScope.Administrator &&
            Explain("0x80090034").Scope == SignInIssueScope.Administrator &&
            Explain("0x80090031").NextStep.Length > 30 &&
            ExtractCode("Error code: 80090030") == "80090030" &&
            ExtractCode("0x80090011") == "80090011" &&
            Explain("AADSTS50127").Scope == SignInIssueScope.LocalRepair &&
            Explain("AADSTS50058").Scope == SignInIssueScope.UserAction &&
            Explain("person@example.com").Scope == SignInIssueScope.Investigation &&
            ExtractCode("12345 other details") == "" &&
            ExtractCode("1001") == "1001";
    }
}
