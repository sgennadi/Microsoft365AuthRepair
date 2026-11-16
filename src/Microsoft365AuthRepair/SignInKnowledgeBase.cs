namespace Microsoft365AuthRepair;

// Curated, offline Microsoft Learn-based troubleshooting paths.
// Source links document the described checks; they are not evidence that an
// individual machine has a particular problem. No account changes occur here.
internal sealed record SignInHelpArticle(
    string Key, string Title, string Symptom, string Check, string Cause,
    string SafeAction, string SourceName, string SourceUrl);

internal static class SignInKnowledgeBase
{
    private const string Wam = "https://learn.microsoft.com/en-us/troubleshoot/microsoft-365/admin/authentication/automatic-authentication-fails";
    private const string ErrorCodes = "https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes";
    private const string EntraDevices = "https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-device-dsregcmd";
    private const string EntraPrt = "https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-primary-refresh-token";
    private const string ConditionalAccess = "https://learn.microsoft.com/en-us/entra/identity/conditional-access/troubleshoot-conditional-access";
    private const string EntraSignInLogs = "https://learn.microsoft.com/en-us/entra/identity/monitoring-health/howto-troubleshoot-sign-in-errors";
    private const string OfficeLicense = "https://learn.microsoft.com/en-us/deployoffice/licensing-activation/vnextdiag";
    private const string SharedComputer = "https://learn.microsoft.com/en-us/microsoft-365-apps/licensing-activation/troubleshoot-shared-computer-activation";
    private const string Connectivity = "https://learn.microsoft.com/en-us/microsoft-365/enterprise/assessing-network-connectivity";
    private const string TpmTroubleshooting = "https://learn.microsoft.com/en-us/troubleshoot/windows-client/windows-security/bitlocker-and-tpm-other-known-issues";

    private static string T(string en, string ru, string he) => UpdateUiText.T(en, ru, he);

    internal static IReadOnlyList<SignInHelpArticle> All() =>
    [
        new("work-wam",
            T("Work account — WAM / BrokerPlugin", "Рабочая учётная запись — WAM / BrokerPlugin", "חשבון עבודה — WAM / BrokerPlugin"),
            T("Word, Outlook or Teams repeatedly asks to sign in or reports [1001].",
              "Word, Outlook или Teams постоянно запрашивает вход либо показывает [1001].",
              "יישומי Office מבקשים כניסה שוב ושוב או מציגים [1001]."),
            T("Check Microsoft.AAD.BrokerPlugin for the affected Windows user, not an elevated different administrator.",
              "Проверьте Microsoft.AAD.BrokerPlugin у проблемного пользователя Windows, а не другого администратора.",
              "בדוק Microsoft.AAD.BrokerPlugin בחשבון המשתמש הרלוונטי."),
            T("Package registration may be missing; security software or unsupported roaming of identity data can also interfere.",
              "Возможна потеря регистрации пакета, вмешательство защитного ПО или перенос данных авторизации между профилями.",
              "ייתכן שרישום החבילה חסר, או שתוכנת אבטחה/נדידת פרופיל מפריעה."),
            T("If registration is missing, choose Re-register WAM for that user. If present, review the application and security software first. Do not clear TPM or device registration.",
              "Если регистрация отсутствует, выполните Re-register WAM для этого пользователя. Иначе сначала проверьте приложение и защитное ПО. Не очищайте TPM.",
              "אם הרישום חסר, נסה לרשום מחדש WAM. אין לנקות TPM או לנתק את המכשיר."),
            "Microsoft Learn — Microsoft 365 authentication",
            Wam),
        new("personal-wam",
            T("Personal Microsoft account — CloudExperienceHost", "Личная Microsoft Account — CloudExperienceHost", "חשבון Microsoft אישי — CloudExperienceHost"),
            T("Personal Outlook.com/Hotmail account fails to sign in inside Windows applications.",
              "Личная учётная запись Outlook.com/Hotmail не входит в приложения Windows.",
              "חשבון Outlook.com אישי לא מצליח להתחבר ביישומי Windows."),
            T("Check Microsoft.Windows.CloudExperienceHost registration in the affected user's session.",
              "Проверьте регистрацию Microsoft.Windows.CloudExperienceHost в сеансе проблемного пользователя.",
              "בדוק רישום Microsoft.Windows.CloudExperienceHost אצל המשתמש."),
            T("The Live ID package can be missing or corrupt. This is not the same package as an Entra work account.",
              "Может отсутствовать компонент Live ID. Он отличается от BrokerPlugin рабочей учётной записи.",
              "ייתכן שרכיב Live ID חסר. הוא נפרד מ-BrokerPlugin לחשבונות ארגוניים."),
            T("Follow Microsoft's CloudExperienceHost registration instructions if the component is missing. Do not reset work-account WAM as a substitute.",
              "Если компонент отсутствует, используйте инструкцию Microsoft по его регистрации. Не подменяйте это сбросом WAM рабочей учётной записи.",
              "פעל לפי הנחיות Microsoft לרישום CloudExperienceHost; אל תאפס WAM ארגוני במקום."),
            "Microsoft Learn — Live ID / WAM components",
            Wam),
        new("device-trust",
            T("Entra device trust / AADSTS50155", "Доверие к устройству Entra / AADSTS50155", "אמון מכשיר Entra / AADSTS50155"),
            T("DeviceAuthStatus shows a failure or Entra refuses device authentication.",
              "DeviceAuthStatus показывает ошибку или Entra отклоняет аутентификацию устройства.",
              "DeviceAuthStatus מציג שגיאה או ש-Entra דוחה את המכשיר."),
            T("Check dsregcmd /status, whether the test was able to reach Entra, and the corresponding device record in the tenant.",
              "Проверьте dsregcmd /status, доступность проверки Entra и запись устройства в tenant.",
              "בדוק dsregcmd /status, תקשורת ל-Entra ורישום המכשיר בדייר."),
            T("The device may be disabled/deleted, incorrectly registered, or temporarily unable to verify its status.",
              "Устройство может быть отключено, удалено, некорректно зарегистрировано или временно не проходить сетевую проверку.",
              "ייתכן שהמכשיר חסום, נמחק, לא רשום נכון או שאין תקשורת."),
            T("Ask IT to verify device join type and registration before attempting supported device recovery. Neither WAM reset nor Office cache deletion repairs device trust.",
              "Попросите IT проверить тип подключения и регистрацию устройства перед восстановлением. Сброс WAM/Office не исправляет доверие устройства.",
              "פנה לתמיכה לבדיקת סוג צירוף ורישום לפני שחזור מכשיר."),
            "Microsoft Learn — device authentication",
            EntraDevices),
        new("prt",
            T("Primary Refresh Token (PRT)", "Основной токен обновления (PRT)", "אסימון רענון ראשי (PRT)"),
            T("AzureAdPrt shows NO for a user who expects Entra single sign-on.",
              "AzureAdPrt показывает NO, хотя пользователю необходим SSO через Entra.",
              "AzureAdPrt מציג NO למרות שנדרש SSO ארגוני."),
            T("Run dsregcmd /status as the affected, non-elevated user; verify expected join type and PRT acquisition diagnostics.",
              "Запустите dsregcmd /status от проблемного пользователя без повышения прав; проверьте тип подключения и диагностику PRT.",
              "הפעל dsregcmd /status כמשתמש הרלוונטי, ובדוק צירוף ונתוני PRT."),
            T("Windows sign-in, cloud/device trust, network or policy may prevent PRT acquisition. PRT can be absent by design on some configurations.",
              "Получению PRT могут мешать вход в Windows, сеть, доверие устройства или политики; иногда PRT не требуется по конфигурации.",
              "כניסה ל-Windows, רשת, אמון מכשיר או מדיניות עשויים להשפיע על PRT."),
            T("Verify connectivity and sign in again; ask IT to review PRT diagnostics if still missing. Do not automatically unregister the device.",
              "Проверьте сеть, повторите вход в Windows и при необходимости передайте диагностику PRT в IT. Не отключайте регистрацию автоматически.",
              "בדוק חיבור, התחבר מחדש ופנה לתמיכה אם PRT עדיין חסר."),
            "Microsoft Learn — PRT troubleshooting",
            EntraPrt),
        new("conditional-access",
            T("Conditional Access / compliance", "Conditional Access / соответствие", "גישה מותנית / תאימות"),
            T("AADSTS53000, 53001 or 53003 prevents issuance of an access token.",
              "AADSTS53000, 53001 или 53003 блокирует выдачу токена.",
              "AADSTS53000, 53001 או 53003 חוסמים כניסה."),
            T("Ask IT to inspect the matching Entra sign-in event's Conditional Access result and device compliance/join requirements.",
              "Попросите IT проверить соответствующую запись Sign-in Logs, применённые политики и требования к устройству.",
              "בדוק ביומני Entra מדיניות גישה ותאימות מכשיר."),
            T("The tenant's access policy, device compliance or join requirement is not satisfied.",
              "Не выполнены требования Conditional Access, соответствия или регистрации устройства.",
              "תנאי גישה, תאימות או רישום מכשיר אינם מתקיימים."),
            T("Follow approved enrollment/compliance steps or ask the administrator to review policy. Never disable Conditional Access or MFA from this tool.",
              "Исправьте соответствие/регистрацию разрешённым способом либо обратитесь к администратору. Программа не отключает Conditional Access и MFA.",
              "פעל לפי מדיניות הארגון. אין לעקוף גישה מותנית או MFA."),
            "Microsoft Learn — Conditional Access sign-in",
            ConditionalAccess),
        new("mfa",
            T("MFA registration or verification", "Проверка MFA / регистрация", "רישום או אימות MFA"),
            T("AADSTS50076, 50079 or 500121 during sign-in.",
              "AADSTS50076, 50079 или 500121 при входе.",
              "AADSTS50076, 50079 או 500121 בזמן כניסה."),
            T("Verify the interactive MFA prompt and registration state; if needed use the specific Entra sign-in error detail.",
              "Проверьте запрос MFA и состояние регистрации, при необходимости — запись ошибки в Entra.",
              "בדוק בקשת MFA ורישום, ובהמשך יומני Entra."),
            T("Additional verification or registration is required; a failed/pending MFA prompt is not evidence of a broken WAM package.",
              "Требуется дополнительная проверка или регистрация; ошибка MFA не доказывает неисправность WAM.",
              "נדרש MFA; אין בכך הוכחה לכשל ב-WAM."),
            T("Complete the approved MFA flow. If enrollment is blocked, contact IT. Clearing Office caches does not remove MFA requirements.",
              "Завершите MFA разрешённым способом. При невозможности регистрации обратитесь в IT. Очистка кеша Office не отменит MFA.",
              "השלם MFA לפי הארגון; ניקוי Office אינו מבטל MFA."),
            "Microsoft Learn — sign-in error troubleshooting",
            EntraSignInLogs),
        new("account",
            T("Account, password or tenant", "Учётная запись, пароль или tenant", "חשבון, סיסמה או דייר"),
            T("AADSTS50126, 50055, 50057 or 50034 occurs during login.",
              "AADSTS50126, 50055, 50057 или 50034 при входе.",
              "AADSTS50126, 50055, 50057 או 50034 בזמן כניסה."),
            T("Check exact error code in Entra Sign-in Logs and validate user UPN, account status and password expiry with authorized IT staff.",
              "Проверьте точный код в Sign-in Logs, UPN, состояние учётной записи и срок действия пароля.",
              "בדוק קוד מדויק, UPN, סטטוס חשבון ותוקף סיסמה."),
            T("Incorrect credentials, expired password, disabled account or missing user in the target tenant have different remedies.",
              "Неверный пароль, истёкший срок, отключённая запись или отсутствие пользователя в tenant — разные причины.",
              "סיסמה שגויה, פג תוקף, חסימת חשבון או חשבון חסר דורשים טיפול שונה."),
            T("Correct the UPN and sign-in flow or ask IT to recover the account. Resetting WAM cannot create a missing tenant account.",
              "Исправьте UPN/ввод либо попросите IT восстановить учётную запись. Сброс WAM не создаст пользователя в tenant.",
              "תקן פרטי כניסה או פנה לתמיכה. איפוס WAM לא ייצור חשבון."),
            "Microsoft Learn — Entra error-code reference",
            ErrorCodes),
        new("session",
            T("Incomplete or expired session", "Незавершённый вход или истёкшая сессия", "הפעלה לא הושלמה או שפג תוקפה"),
            T("AADSTS50058 or 70046 while silently signing in.",
              "AADSTS50058 или 70046 при фоновом входе.",
              "AADSTS50058 או 70046 במהלך כניסה שקטה."),
            T("Check whether the app can start interactive sign-in and whether tenant policies require reauthentication.",
              "Проверьте интерактивный вход и политики повторной аутентификации.",
              "בדוק כניסה אינטראקטיבית ודרישות אימות מחדש."),
            T("A silent session is missing or expired; this can be normal and does not by itself require repair.",
              "Фоновая сессия отсутствует или истекла; это не всегда неисправность.",
              "הפעלה שקטה חסרה או פג תוקפה, ולא בהכרח תקלה."),
            T("Sign in interactively and complete any prompt. Check tenant logs if it repeats; avoid blind cache deletion.",
              "Выполните обычный вход и подтвердите запрос. При повторении проверьте логи Entra; не удаляйте кеш наугад.",
              "בצע כניסה ואימות רגילים ובדוק יומנים אם התקלה נמשכת."),
            "Microsoft Learn — Entra sign-in errors",
            EntraSignInLogs),
        new("office-license",
            T("Microsoft 365 Apps activation", "Активация Microsoft 365 Apps", "הפעלת Microsoft 365 Apps"),
            T("Office opens but shows Unlicensed Product, cannot activate or repeatedly requests a license.",
              "Office запускается, но показывает Unlicensed Product, не активируется или постоянно просит лицензию.",
              "Office נפתח אך מבקש רישוי או מציג Unlicensed Product."),
            T("Check installed Office edition and presence of vnextdiag.ps1. For current Microsoft 365 Apps, run vnextdiag.ps1 -action list in the affected user's context to view the license state.",
              "Проверьте редакцию Office и наличие vnextdiag.ps1. Для современных Microsoft 365 Apps используйте vnextdiag.ps1 -action list от проблемного пользователя.",
              "בדוק מהדורת Office ואת vnextdiag.ps1. עבור Microsoft 365 Apps בדוק -action list בחשבון המשתמש."),
            T("Subscription, assignment, account or cached activation state may be the cause. A local registry hint cannot prove that a valid license is assigned.",
              "Причина может быть в подписке, назначении лицензии, учётной записи или состоянии активации. Реестр не доказывает наличие действующей лицензии.",
              "ייתכן שיש בעיית מנוי, שיוך רישוי או מצב הפעלה."),
            T("Review the license read-only with vnextdiag.ps1. Ask IT to verify subscription assignment before considering a documented, separately confirmed activation reset. Do not run ospp.vbs for modern subscription licensing.",
              "Проверьте лицензию через vnextdiag.ps1. Пусть IT проверит назначение лицензии перед отдельно подтверждённым сбросом активации. Не используйте ospp.vbs для современной подписки.",
              "בדוק רישוי עם vnextdiag.ps1 ואשר שיוך רישיון לפני כל איפוס."),
            "Microsoft Learn — vnextdiag license diagnostics",
            OfficeLicense),
        new("shared-activation",
            T("RDS / shared computer activation", "RDS / Shared Computer Activation", "RDS / הפעלה במחשב משותף"),
            T("Office licensing fails on RDS, shared Windows workstations or VDI.",
              "Не удаётся активировать Office на RDS, общих рабочих станциях или VDI.",
              "רישוי Office נכשל ב-RDS, עמדות משותפות או VDI."),
            T("Check HKLM Office ClickToRun Configuration SharedComputerLicensing and the user licensing-token location; verify the subscription supports SCA.",
              "Проверьте HKLM Office ClickToRun SharedComputerLicensing, размещение токенов лицензии и поддержку SCA подпиской.",
              "בדוק SharedComputerLicensing, מקום אסימוני רישוי ותמיכה ב-SCA."),
            T("SCA may be disabled or the subscription/token roaming and network environment may not meet its requirements.",
              "SCA может быть не включён, тариф не поддерживать режим, либо есть проблема с токеном/перемещением профиля.",
              "ייתכן שהגדרת SCA, סוג המנוי או אסימוני רישוי אינם מתאימים."),
            T("Use supported Office deployment/GPO configuration and license assignment with IT. Enabling SCA may require a restart and licensing state remediation; never silently change it.",
              "Настройте SCA через поддерживаемые Office Deployment/GPO средства и проверьте лицензии. Изменения могут потребовать перезагрузку; программа не меняет SCA автоматически.",
              "הגדר SCA לפי הנחיות Microsoft עם התמיכה; אין לבצע שינוי אוטומטי."),
            "Microsoft Learn — troubleshoot Shared Computer Activation",
            SharedComputer),
        new("network",
            T("Microsoft 365 connectivity / proxy", "Подключение Microsoft 365 / прокси", "תקשורת Microsoft 365 / שרת Proxy"),
            T("Sign-in succeeds on one network but fails on another, or TLS/DNS/proxy errors appear.",
              "На одной сети вход работает, а на другой — нет; возникают ошибки TLS/DNS/прокси.",
              "התחברות מצליחה ברשת אחת ונכשלת באחרת, או שיש שגיאות TLS/DNS/Proxy."),
            T("Use Microsoft's network connectivity assessment. Verify required Microsoft 365 endpoints and TLS/proxy policy with your network administrator.",
              "Используйте проверку сетевого подключения Microsoft и проверьте доступ к необходимым конечным точкам Microsoft 365.",
              "בדוק קישוריות של Microsoft ודרישות TLS/Proxy עם מנהל הרשת."),
            T("Proxy authentication, firewall blocks, TLS inspection or DNS issues can prevent service access.",
              "Прокси-аутентификация, блокировка межсетевым экраном, перехват TLS или DNS могут мешать подключению.",
              "Proxy, חומת אש, בדיקת TLS או DNS עלולים לחסום חיבור."),
            T("Follow the Microsoft connectivity test results and network policy. Do not disable the firewall, certificate validation or antivirus automatically.",
              "Используйте результаты Microsoft Connectivity Test. Не отключайте автоматически брандмауэр, проверку сертификатов или антивирус.",
              "פעל לפי בדיקת הקישוריות. אין לבטל הגנת רשת או בדיקת תעודות אוטומטית."),
            "Microsoft Learn — network assessment",
            Connectivity),
        new("tpm-lockout",
            T("TPM unavailable / temporary lockout", "TPM не готов / временная блокировка", "TPM לא מוכן / חסימה זמנית"),
            T("0x80090030 or 0x80090031 appears; TPM reports not ready or locked out.",
              "Ошибка 0x80090030 или 0x80090031; TPM не готов или заблокирован.",
              "שגיאת 0x80090030 או 0x80090031, או TPM נעול."),
            T("Read Get-Tpm TpmPresent/TpmReady/LockedOut and check tpm.msc. KeySignTest requires elevation and may be unavailable.",
              "Проверьте Get-Tpm: TpmPresent/TpmReady/LockedOut и tpm.msc. Для KeySignTest нужны права администратора.",
              "בדוק Get-Tpm ו-tpm.msc. בדיקת KeySignTest דורשת הרשאת מנהל."),
            T("TPM lockout can be a temporary anti-hammering state. A not-ready device can have hardware, firmware or provisioning causes; neither proves corruption of Office caches.",
              "Блокировка TPM может быть временной защитой от перебора. Неготовность может быть связана с прошивкой или инициализацией, но не доказывает проблему кеша Office.",
              "חסימת TPM עשויה להיות זמנית; אין הוכחה לבעיה במטמון Office."),
            T("Stop repeated key attempts; allow the cooldown and consult IT/vendor for supported recovery. Verify BitLocker recovery availability. Do not auto-clear TPM or reset PIN.",
              "Прекратите повторные попытки, дождитесь снятия блокировки и обратитесь к IT или производителю. Проверьте доступность BitLocker recovery. Не сбрасывайте TPM и PIN автоматически.",
              "יש להמתין לסיום החסימה ולפנות לתמיכה. אין לאפס TPM או PIN אוטומטית."),
            "Microsoft Learn — BitLocker and TPM known issues",
            TpmTroubleshooting),
        new("device-key",
            T("Windows Hello / device key issue", "Windows Hello / ключи устройства", "Windows Hello / מפתחות מכשיר"),
            T("Sign-in reports 0x80090011, 0x80090016 or 0x80090034; KeySignTest fails or AadRecoveryEnabled is YES.",
              "При входе появляется 0x80090011/16/34 либо KeySignTest=FAILED или AadRecoveryEnabled=YES.",
              "שגיאת מפתח או כשל בבדיקת מפתח המכשיר."),
            T("Inspect Windows Hello / device key health, recovery flags and KeySignTest with appropriate rights; preserve BitLocker recovery information.",
              "Проверьте Windows Hello, состояние ключей устройства, KeySignTest и ключи восстановления BitLocker.",
              "בדוק Windows Hello, מצב מפתחות ושמור מידע שחזור BitLocker."),
            T("A missing keyset is possible; this code does not prove that the physical TPM is broken.",
              "Может отсутствовать криптографический набор ключей; это не доказывает неисправность TPM.",
              "ייתכן שחסרים מפתחות, אך אין הוכחה לתקלה ב-TPM."),
            T("Escalate to IT for device-specific recovery. Do NOT clear the TPM, remove containers, or disconnect Entra registration automatically.",
              "Обратитесь в IT для восстановления устройства. Не очищайте TPM, контейнеры ключей и регистрацию Entra автоматически.",
              "פנה לתמיכה. אין לנקות TPM או לנתק רישום Entra אוטומטית."),
            "Microsoft Learn — dsregcmd device key diagnostics",
            EntraDevices),
        new("general",
            T("Sign-in still fails with healthy local signals", "Вход не работает при нормальных локальных проверках", "התחברות נכשלת למרות בדיקות מקומיות תקינות"),
            T("WAM and local Entra signals appear normal, but an Office/Teams/OneDrive login is still failing.",
              "WAM/Entra выглядят исправными, но Word, Teams или OneDrive всё ещё не входит.",
              "WAM ו-Entra תקינים אך הכניסה ליישום נכשלת."),
            T("Capture only the displayed error code and consult the corresponding Entra sign-in event. Also consider licensing, device compliance and network access.",
              "Проверьте код ошибки и соответствующую запись Sign-in Logs, а также лицензирование, политики устройства и сеть.",
              "בדוק קוד שגיאה ויומן Entra, רישוי, תאימות ורשת."),
            T("A healthy broker/PRT does not prove a successful app sign-in, valid license or tenant access permission.",
              "Исправные Broker/PRT не доказывают успешный вход в приложение, лицензию и доступ в tenant.",
              "Broker/PRT תקינים אינם מוכיחים רישוי או הרשאות כניסה."),
            T("Choose the matching problem in the guide before any reset. Avoid deleting Office sign-in data just because local prerequisites look healthy.",
              "Выберите подходящую проблему в справочнике до сброса. Не удаляйте кеши Office только из-за зелёного статуса WAM.",
              "בחר בעיה מתאימה במדריך לפני איפוס."),
            "Microsoft Learn — troubleshooting sign-in events",
            EntraSignInLogs)
    ];

    internal static SignInHelpArticle Get(string key) =>
        All().First(x => x.Key == key);

    internal static SignInHelpArticle? ByCode(string code) =>
        (code.ToUpperInvariant()) switch
        {
            "1001" or "AADSTS50127" => Get("work-wam"),
            "AADSTS50155" => Get("device-trust"),
            "AADSTS53000" or "AADSTS53001" or "AADSTS53002" or "AADSTS53003"
                or "AADSTS530035" => Get("conditional-access"),
            "AADSTS50076" or "AADSTS50079" or "AADSTS50072" or "AADSTS500121"
                => Get("mfa"),
            "AADSTS50126" or "AADSTS50055" or "AADSTS50057" or "AADSTS50034"
                => Get("account"),
            "AADSTS50058" or "AADSTS70046" => Get("session"),
            "80090011" or "80090016" or "80090034" => Get("device-key"),
            "80090030" or "80090031" => Get("tpm-lockout"),
            _ => null
        };

    // Suggestions are deterministic interpretations of *existing* read-only
    // signals. A positive diagnosis never authorizes an automatic repair.
    internal static SignInHelpArticle Recommend(WamHealthResult result, bool ownDesktop)
    {
        if (!ownDesktop) return Get("general");
        var signals = result.Signals.ToDictionary(x => x.Key, StringComparer.Ordinal);
        WamSignalState State(string key) =>
            signals.TryGetValue(key, out var value) ? value.State : WamSignalState.Unknown;

        if (State("DeviceAuthStatus") == WamSignalState.Attention)
            return Get("device-trust");
        if (State("EntraDeviceKey") == WamSignalState.Attention)
            return Get("device-key");
        if (State("BrokerPlugin") == WamSignalState.Attention)
            return Get("work-wam");
        if (State("AzureAdPrt") == WamSignalState.Attention)
            return Get("prt");
        return Get("general");
    }

    internal static bool IsMicrosoftLearn(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase);

    internal static bool SelfTest()
    {
        var all = All();
        var sample = new WamHealthResult(WamHealthLevel.AttentionRequired, "", "")
        {
            Signals = [
                new WamHealthSignal("BrokerPlugin", "", "", WamSignalState.Attention),
                new WamHealthSignal("DeviceAuthStatus", "", "", WamSignalState.Good)
            ]
        };
        var failedDevice = sample with
        {
            Signals = [
                new WamHealthSignal("BrokerPlugin", "", "", WamSignalState.Attention),
                new WamHealthSignal("DeviceAuthStatus", "", "", WamSignalState.Attention)
            ]
        };
        return all.Count >= 12 &&
            all.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == all.Count &&
            all.All(x => IsMicrosoftLearn(x.SourceUrl) &&
                !string.IsNullOrWhiteSpace(x.Symptom) &&
                !string.IsNullOrWhiteSpace(x.Check) &&
                !string.IsNullOrWhiteSpace(x.SafeAction)) &&
            ByCode("AADSTS53003")?.Key == "conditional-access" &&
            ByCode("AADSTS50076")?.Key == "mfa" &&
            ByCode("AADSTS50155")?.Key == "device-trust" &&
            ByCode("80090031")?.Key == "tpm-lockout" &&
            ByCode("80090034")?.Key == "device-key" &&
            ByCode("0x80090016") == null &&
            Recommend(sample, true).Key == "work-wam" &&
            Recommend(failedDevice, true).Key == "device-trust" &&
            Recommend(WamHealthAnalysis.Evaluate(
                "BrokerPlugin: MISSING\nKeySignTest: FAILED", true), true).Key ==
                "device-key" &&
            Recommend(sample, false).Key == "general" &&
            !IsMicrosoftLearn("https://evil.example.com/learn.microsoft.com");
    }
}
