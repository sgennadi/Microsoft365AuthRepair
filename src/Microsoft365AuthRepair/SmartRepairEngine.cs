namespace Microsoft365AuthRepair;

// Deterministic, local-only recommendations. No local signal proves that the
// Microsoft 365 service accepted an actual application sign-in.
internal enum SmartRepairAction
{
    None, ReviewDeviceTrust, ReviewSession, ReviewNetwork,
    ReRegisterWam, ReviewOfficeLicensing, VerifyAppSignIn, ReviewPolicy,
    ReviewTpmState, ReviewTpmAttestation, ReviewWritableDc, ReviewBitLocker
}

internal sealed record SmartRepairDecision(
    SmartRepairAction Action, string Heading, string Explanation,
    string NextAction, bool SafeToOfferRepair, bool NeedsAdminReview);

internal static class SmartRepairEngine
{
    private static string T(string en, string ru, string he) =>
        UpdateUiText.T(en, ru, he);

    private static string Signal(WamHealthResult report, string key) =>
        report.Signals.FirstOrDefault(x => x.Key.Equals(key,
            StringComparison.Ordinal))?.Value ?? "";

    private static bool SignalIs(WamHealthResult report, string key, WamSignalState state) =>
        report.Signals.Any(x => x.Key == key && x.State == state);

    internal static SmartRepairDecision Evaluate(
        WamHealthResult wam, OfficeLicenseSnapshot? office,
        RepairPreflight? preflight, bool ownDesktopAccount,
        TpmBitLockerSnapshot? deviceProtection = null)
    {
        // Never repair another administrator's broker or identity by mistake.
        if (!ownDesktopAccount || preflight?.CurrentUserContext == false)
            return new(SmartRepairAction.ReviewSession,
                T("Wrong Windows session", "Другая учётная запись Windows", "הפעלה של משתמש אחר"),
                T("WAM/Office account checks belong to the process user, which may not be the desktop owner after UAC.",
                  "После UAC проверка может относиться к администратору, а не к пользователю с проблемой.",
                  "לאחר הרשאת מנהל הבדיקה עשויה להתייחס לחשבון אחר."),
                T("Launch without elevation as the affected user; do not reset another profile.",
                  "Запустите программу без повышения прав от нужного пользователя; не сбрасывайте чужой профиль.",
                  "הפעל כמשתמש הרלוונטי ללא הרשאות מנהל."), false, false);

        // TPM-not-ready is a local warning, not proof of TPM hardware failure.
        // In this case prioritize diagnosis over blind identity cache resets.
        if (deviceProtection?.NeedsTpmReview == true)
            return new(SmartRepairAction.ReviewTpmState,
                T("TPM readiness needs review", "Требуется проверка готовности TPM",
                    "יש לבדוק את מוכנות TPM"),
                T("Windows reported a TPM that is not ready or not enabled/owned. A missing Office cryptographic keyset can also be a profile or WAM problem; clearing TPM is not an automatic fix.",
                  "Windows сообщает, что TPM не готов либо не включён/инициализирован. Ошибка криптографических ключей Office может относиться к профилю или WAM. Очистка TPM не является автоматическим решением.",
                  "TPM אינו מוכן. ייתכן שהבעיה ב-WAM או בפרופיל; אין לנקות TPM."),
                T("Open TPM & BitLocker details and ask IT to review device protection, BitLocker and Entra trust before making changes.",
                  "Откройте сведения TPM и BitLocker и проверьте с IT состояние устройства и Entra перед изменениями.",
                  "בדוק TPM, BitLocker ו-Entra עם מנהל המערכת."), false, true);

        // Local WAM registration does not restore device trust.
        if (SignalIs(wam, "DeviceAuthStatus", WamSignalState.Attention))
            return new(SmartRepairAction.ReviewDeviceTrust,
                T("Entra device trust needs attention", "Проблема доверия устройства Entra", "בעיית אמון מכשיר Entra"),
                T("Device authentication is failing; WAM re-registration and cache deletion do not repair device identity.",
                  "Аутентификация устройства неуспешна. Очистка Office/WAM не восстановит регистрацию устройства.",
                  "איפוס WAM או Office אינו מתקן אימות מכשיר."),
                T("Ask IT to review hybrid join, device state and Entra sign-in logs.",
                  "Проверьте с IT Hybrid Join, состояние устройства и журналы Entra.",
                  "בדוק צירוף היברידי ויומני Entra."), false, true);

        // A previously failed AIK enrollment does not prove that local WAM or
        // the TPM hardware is broken. Keep Autopilot attestation separate.
        if (deviceProtection?.NeedsAttestationReview == true)
            return new(SmartRepairAction.ReviewTpmAttestation,
                T("TPM attestation needs investigation",
                  "Нужна проверка аттестации TPM", "נדרשת בדיקת אימות TPM"),
                T("A local AIK enrollment error was recorded. TPM readiness and Office sign-in are separate from EK/AIK attestation.",
                  "Обнаружена ошибка регистрации AIK. Готовность TPM, аттестация и вход Office — разные проверки.",
                  "נמצאה שגיאת AIK. אימות TPM וכניסה ל-Office הם תהליכים נפרדים."),
                T("Review Windows build, TPM firmware, EK readiness and manufacturer guidance. Do not clear TPM.",
                  "Проверьте версию Windows, прошивку TPM и признаки EK, затем рекомендации производителя. Не очищайте TPM.",
                  "בדוק גרסת Windows וקושחת TPM. אין לנקות TPM."), false, true);

        if (deviceProtection is { DomainJoinStatus: "YES", WritableDcStatus: "NOT_FOUND" })
            return new(SmartRepairAction.ReviewWritableDc,
                T("Writable domain controller not found",
                  "Не найден записываемый контроллер домена", "לא נמצא בקר תחום לכתיבה"),
                T("DPAPI can depend on a writable DC during key backup for domain users. This discovery result does not prove TPM failure or that this user's profile is affected.",
                  "Резервирование DPAPI-ключей доменных пользователей может требовать RWDC. Результат обнаружения не доказывает поломку TPM.",
                  "גיבוי DPAPI עשוי לדרוש בקר תחום זמין."),
                T("Check domain DNS, VPN, secure channel and RWDC access. Do not change ProtectionPolicy automatically.",
                  "Проверьте DNS, VPN, защищённый канал и доступ к RWDC. Не меняйте ProtectionPolicy автоматически.",
                  "בדוק DNS ו-VPN. אין לשנות ProtectionPolicy באופן אוטומטי."), false, true);

        if (deviceProtection?.NeedsBitLockerReview == true)
            return new(SmartRepairAction.ReviewBitLocker,
                T("BitLocker OS-volume protection needs review",
                  "Проверьте защиту системного диска BitLocker", "יש לבדוק את הגנת BitLocker"),
                T("Windows reports that an encrypted operating-system volume currently has protection OFF. This is a device security warning, not an Office sign-in diagnosis.",
                  "Windows сообщает, что системный раздел зашифрован, но защита OFF. Это предупреждение о защите устройства, а не диагноз Office.",
                  "כונן המערכת מוצפן אך ההגנה כבויה. זו אינה אבחנת Office."),
                T("Ask IT to review BitLocker policy and protectors without exposing recovery keys.",
                  "Обратитесь к IT для проверки политики BitLocker без отображения ключей восстановления.",
                  "בדוק מדיניות BitLocker בלי להציג מפתחות שחזור."), false, true);

        if (preflight?.ClockAcceptable == false)
            return new(SmartRepairAction.ReviewNetwork,
                T("Windows clock differs from HTTPS server", "Расхождение времени Windows", "שעת Windows אינה תואמת"),
                T("The HTTPS Date header differs from the Windows clock by over five minutes. The server header is an advisory signal, not an NTP authority.",
                  "Время в заголовке HTTPS отличается от Windows более чем на пять минут. Это индикатор, а не точная проверка NTP.",
                  "זוהה פער של מעל חמש דקות בשעון."),
                T("Review Windows Time synchronization before changing sign-in caches.",
                  "Сначала проверьте синхронизацию времени Windows.",
                  "בדוק סנכרון שעון Windows."), false, false);

        if (preflight?.MicrosoftLoginReachable == false)
            return new(SmartRepairAction.ReviewNetwork,
                T("Microsoft login HTTPS check unavailable", "Сервер входа Microsoft недоступен", "לא ניתן לבדוק גישת HTTPS"),
                T("The transport probe failed; it might be a proxy, DNS, firewall or temporary connection issue.",
                  "Проверка HTTPS не удалась. Возможны DNS, proxy, firewall или временная ошибка.",
                  "ייתכן שיש בעיית רשת, proxy או DNS."),
                T("Check the work network or VPN; retry diagnosis before cache reset.",
                  "Проверьте сеть/VPN и повторите диагностику до сброса кешей.",
                  "בדוק רשת או VPN והפעל שוב את הבדיקה."), false, false);

        if (SignalIs(wam, "BrokerPlugin", WamSignalState.Attention))
        {
            var blocked = preflight?.OfficeAppsRunning == true;
            return new(SmartRepairAction.ReRegisterWam,
                T("WAM broker registration needs repair", "Требуется восстановление WAM", "נדרש תיקון רישום WAM"),
                blocked
                    ? T("BrokerPlugin is missing. Office apps are running and may close during registration.",
                        "BrokerPlugin отсутствует. Приложения Office работают и могут закрыться.",
                        "BrokerPlugin חסר. יישומי Office פתוחים.")
                    : T("BrokerPlugin is missing for this account; a targeted re-registration is the least disruptive local repair.",
                        "У пользователя отсутствует BrokerPlugin. Точечная перерегистрация — наименее разрушительное исправление.",
                        "BrokerPlugin חסר. יש לבצע רישום מחדש."),
                blocked
                    ? T("Save work and close Office apps, then explicitly confirm Re-register WAM.",
                        "Сохраните документы и закройте Office, затем подтвердите Re-register WAM.",
                        "שמור עבודה וסגור יישומי Office לפני תיקון WAM.")
                    : T("Review and confirm Re-register WAM. Saved Office sign-in caches are not deleted.",
                        "Подтвердите Re-register WAM; кеши входа Office сохранятся.",
                        "אשר רישום WAM מחדש ללא מחיקת מטמון."),
                true, false);
        }

        if (preflight?.AppxDeploymentRestricted == true &&
            !SignalIs(wam, "BrokerPlugin", WamSignalState.Good))
            return new(SmartRepairAction.ReviewPolicy,
                T("AppX deployment policy may interfere", "Политика AppX может мешать", "מדיניות AppX עשויה להפריע"),
                T("A deployment restriction was found, but this alone does not prove WAM registration is blocked.",
                  "Найдена политика установки приложений, но это ещё не доказательство блокировки WAM.",
                  "נמצאה מדיניות AppX אך אין הוכחה לחסימה."),
                T("Ask IT to inspect AppX deployment events and enterprise policy.",
                  "Проверьте с IT события AppX и корпоративные политики.",
                  "בדוק מדיניות ואירועי AppX."), false, true);

        if (office is { Installed: true } &&
            office.ProductFamily.Contains("volume", StringComparison.OrdinalIgnoreCase))
            return new(SmartRepairAction.ReviewOfficeLicensing,
                T("Check Office Volume activation separately", "Проверьте активацию Office Volume", "בדוק הפעלת Office Volume"),
                T("Volume KMS/MAK activation is separate from WAM/Entra sign-in. Do not replace keys to fix a missing PRT.",
                  "Активация KMS/MAK не связана напрямую с WAM/PRT; замена ключа не исправит PRT.",
                  "KMS/MAK אינו תיקון לבעיית PRT."),
                T("Open Office / Visio / Project licensing for an exact read-only inventory before any key change.",
                  "Откройте лицензирование Office / Visio / Project и проверьте точный статус до замены ключей.",
                  "בדוק מלאי רישוי Office לפני החלפת מפתח."), false, false);

        if (SignalIs(wam, "AzureAdPrt", WamSignalState.Attention) ||
            SignalIs(wam, "WamDefaultSet", WamSignalState.Attention))
            return new(SmartRepairAction.ReviewSession,
                T("Account session needs review", "Проверьте сеанс пользователя", "יש לבדוק את הפעלת המשתמש"),
                T("PRT or default WAM account is missing; this alone does not prove the broker is broken.",
                  "PRT или учётная запись WAM отсутствует, но это не доказывает неисправность BrokerPlugin.",
                  "חסר PRT או חשבון WAM, אך אין הוכחה לתקלה ברכיב."),
                T("Review work account, network/VPN and Windows sign-in. Try signing in to an affected Office app.",
                  "Проверьте рабочую учётную запись, сеть/VPN и вход Windows, затем откройте Office.",
                  "בדוק חשבון עבודה ורשת ונסה כניסה ל-Office."), false, false);

        if (wam.Level == WamHealthLevel.Healthy)
            return new(SmartRepairAction.VerifyAppSignIn,
                T("Local sign-in prerequisites look healthy", "Локальные компоненты выглядят исправными", "רכיבי ההתחברות נראים תקינים"),
                T("Local WAM/Entra indicators are present. This does not prove Microsoft 365 app authentication.",
                  "Локальные WAM/Entra выглядят исправными, но реальная авторизация приложения не проверена.",
                  "הסימנים המקומיים תקינים אך כניסה לא אומתה."),
                T("Open Word, Outlook or Teams and confirm actual sign-in. Use Entra sign-in logs for cloud-side errors.",
                  "Проверьте вход в Word, Outlook или Teams. При ошибке изучите журналы Entra.",
                  "נסה כניסה ליישומי Office ובדוק יומני Entra במקרה של תקלה."), false, false);

        return new(SmartRepairAction.None,
            T("Further diagnosis needed", "Нужна дополнительная диагностика", "נדרש אבחון נוסף"),
            T("One or more prerequisites could not be confirmed. Avoid speculative key changes or token deletion.",
              "Не удалось подтвердить часть параметров. Не удаляйте токены и не меняйте ключи наугад.",
              "אין למחוק טוקנים או לשנות מפתחות ללא אבחון."),
            T("Review the technical details and relevant sign-in error, then ask IT if needed.",
              "Изучите технические подробности и код ошибки, при необходимости обратитесь к IT.",
              "בדוק את פרטי השגיאה ופנה לתמיכה."), false, false);
    }

    internal static bool SelfTest()
    {
        const string healthy = "BrokerPlugin: PRESENT\nAzureAdJoined: YES\nDomainJoined: YES\n" +
            "DeviceAuthStatus: SUCCESS\nAzureAdPrt: YES\nWamDefaultSet: YES";
        var baseResult = WamHealthAnalysis.Evaluate(healthy, true);
        var basePreflight = new RepairPreflight(true, true, true, false, "", null, "OK");
        var missingBroker = WamHealthAnalysis.Evaluate(healthy.Replace(
            "BrokerPlugin: PRESENT", "BrokerPlugin: MISSING"), true);
        var deviceFailed = WamHealthAnalysis.Evaluate(
            healthy.Replace("DeviceAuthStatus: SUCCESS", "DeviceAuthStatus: FAILED")
                   .Replace("BrokerPlugin: PRESENT", "BrokerPlugin: MISSING"), true);
        var fakeVolume = new OfficeLicenseSnapshot(true, "Office volume / LTSC", "16.0.1",
            null, null, true, null, null, null, false);
        return Evaluate(baseResult, null, basePreflight, true).Action == SmartRepairAction.VerifyAppSignIn &&
            Evaluate(missingBroker, null, basePreflight, true).Action == SmartRepairAction.ReRegisterWam &&
            Evaluate(deviceFailed, null, basePreflight, true).Action == SmartRepairAction.ReviewDeviceTrust &&
            !Evaluate(deviceFailed, null, basePreflight, true).SafeToOfferRepair &&
            Evaluate(baseResult, null, basePreflight with { ClockAcceptable = false }, true)
                .Action == SmartRepairAction.ReviewNetwork &&
            Evaluate(baseResult, fakeVolume, basePreflight, true)
                .Action == SmartRepairAction.ReviewOfficeLicensing &&
            Evaluate(missingBroker, null, basePreflight, false).Action == SmartRepairAction.ReviewSession &&
            Evaluate(baseResult, null, basePreflight, true,
                TpmBitLockerSnapshot.Unknown with { AikEnrollmentError = "NOT_FOUND" })
                .Action == SmartRepairAction.ReviewTpmAttestation &&
            Evaluate(missingBroker, null, basePreflight, true,
                TpmBitLockerSnapshot.Unknown with { DomainJoinStatus = "YES",
                    WritableDcStatus = "NOT_FOUND" }).Action == SmartRepairAction.ReviewWritableDc &&
            Evaluate(baseResult, null, basePreflight, true,
                TpmBitLockerSnapshot.Unknown with { OsVolumeProtection = "OFF",
                    OsVolumeConversion = "FULLY_ENCRYPTED" }).Action == SmartRepairAction.ReviewBitLocker &&
            Evaluate(missingBroker, null, basePreflight, true,
                TpmBitLockerSnapshot.Unknown with { TpmPresent = "YES", TpmReady = false })
                .Action == SmartRepairAction.ReviewTpmState &&
            !Evaluate(missingBroker, null, basePreflight, true,
                TpmBitLockerSnapshot.Unknown with { TpmPresent = "YES", TpmReady = false })
                .SafeToOfferRepair &&
            Evaluate(baseResult, null, basePreflight with { MicrosoftLoginReachable = false }, true)
                .Action == SmartRepairAction.ReviewNetwork;
    }
}
