## Preview.21 — v1.0.2-preview.21 (test only)

**Check & Diagnose** now shows explicitly read-only progress (0/4 to 4/4 and elapsed seconds), a temporary **Cancel check** control, and a **60-second total UI deadline**. WAM/Entra, Office activation, TPM/BitLocker and network preflight are checked concurrently. A blocked WMI provider no longer hides completed results: unfinished sections display **Not verified**, never "healthy". A diagnostic check **does not close Microsoft apps or change WAM, Office sign-in, TPM or BitLocker state**. If a native read-only WMI query hangs, the UI returns while that provider may finish later; repeated scans in the same process reuse its existing pending query.

[Preview.21 release notes](docs/releases/v1.0.2-preview.21.md)

## Preview.20 — v1.0.2-preview.20 (test only)

This preview extends the existing read-only TPM/BitLocker inspector with local TPM **lockout state** and adds **Entra device-key signing/recovery** and **Windows Hello enrollment** indicators in Check & Diagnose. A failed KeySignTest or AadRecoveryEnabled=YES directs IT to device-trust review instead of a blind WAM reset; KeySignTest requires elevation and absence is **UNKNOWN**, not failure. No key IDs, TPM owner data, Windows Hello secrets or TPM lockout state are uploaded in anonymous reports. [Preview.20 notes](docs/releases/v1.0.2-preview.20.md).

## Preview.19 — v1.0.2-preview.19 (test only)

TPM & BitLocker read-only diagnostics are available from **Check & Diagnose → TPM & BitLocker details**. Inspect TPM readiness, system-drive encryption/protection, Memory Integrity, DPAPI policy, writable DC discovery and existing AIK enrollment/EK provisioning status. The inspector explains `0x80090016`, `0x80090034`, `0x80090030`, `0x80070490` (NOT_FOUND) and `0x800705B4` (timeout) as distinct issues.

No TPM clear, BitLocker disable/suspend, recovery key enumeration, NGC/BrokerPlugin deletion, TPM attestation enrollment, Entra leave, firmware change or DPAPI policy modification can be triggered from this viewer. The reviewed anonymous report contains only coarse allowlisted states, not keys, DC names, firmware identifiers or other sensitive material. Windows WMI indicators may show UNKNOWN without administrative access. Pilot test on a BitLocker-protected Windows computer before promoting to Stable.

Version **1.0.2-preview.19** is intentionally newer than **Stable 1.0.1** under SemVer. The program does not install previews for Stable users unless Preview is explicitly selected in Update Center.

[Preview.19 release notes](docs/releases/v1.0.2-preview.19.md)

## Preview v1.0.1-preview.18 — Smart Repair Center

Seven integrated improvements: evidence-driven Smart Repair (without automatic destructive changes); read-only preflight; separate Office Volume versus Microsoft 365 subscription activation evidence; selected confirmed-failed WAM task retry after explicit approval, at next sign-in only; optional authorized Microsoft Graph sign-in audit with **AuditLog.Read.All only** using BitKeyBridge-style Device Code or certificate session; post-repair read-only check with honest user-reported Word/Outlook/Teams sign-in confirmation; compact DPI-aware EN/RU/HE UI.

The Graph sign-in feature does not read BitLocker/LAPS keys, create app registrations, grant permissions or save tokens. Certificate mode requires a dedicated audit-only Entra application. Device Code requires an approved public client application with delegated audit scope and a sign-in log reader role. No ROPC or passwords.

The app does not infer Office sign-in success from WAM registration, Task Scheduler completion, HTTP reachability or Graph sign-in metadata. Real Windows and tenant/Office pilots are required before Stable promotion.

[Preview.18 release notes](docs/releases/v1.0.1-preview.18.md)

## Preview v1.0.1-preview.17 — transparent All users task status

1. **View task status (read-only):** an administrator can review pending, running, worker-reported success, failure, attention and unconfirmed tasks. It does not execute, retry, delete or repair anything; a manual Refresh is available.
2. **Truthful evidence:** an administrator-owned local history is recorded after each task is registered, with only task identity, SID, method, action, timestamp and start request. Worker receipts live in each user's LocalAppData; no credentials, tokens or keys are recorded. The disappearance of a one-time task does **not** mean repair success.
3. **Safety:** errors are never silently grouped with successfully completed tasks for cleanup; no unknown worker is automatically deleted. A successful WAM registration/reset worker is **not** an Office application sign-in test.
4. **Clear scope:** **Check this account only** in All users mode checks the account running the app, not all users. Bulk operations remain restricted to the existing three-step wizard and explicit UAC/confirmation.
5. **Verification:** CI covers x64/ARM64, localized GUI initialization and status logic. A real Windows domain and 100/150/200% DPI/HE RTL pilot is required before any Stable promotion.

[Preview.17 release notes](docs/releases/v1.0.1-preview.17.md)

## Preview v1.0.1-preview.16 — simplify Current user and make All users predictable

1. **Current user needs no setup or cleanup:** Removed the duplicate Current user wizard and the entire portable EXE self-deletion helper/CLI. For the signed-in user, Check & Diagnose, Re-register WAM, and Reset Office sign-in are direct, separately confirmed main-screen actions; the app EXE remains in place.
2. **All users is wizard-only:** Selecting All users opens the existing three-step wizard. A small All users setup link allows revisiting method, timing and action choices. The duplicate Re-register/Reset buttons are hidden while All users is selected; normal current-user buttons return when the scope is changed back.
3. **Explicit targeting:** Before confirming a bulk action, the dialog shows counts of existing Windows profiles, online users that would run immediately, and profiles waiting for next sign-in. The reviewed profile list is frozen so profiles added during confirmation are not silently included. No account without an existing profile is enrolled.
4. **Less work after repair:** A successful **direct non-elevated Current user** operation automatically triggers a read-only WAM/Entra/Office prerequisite recheck. This remains a prerequisite check, **not** proof of an actual Word/Outlook/Teams/OneDrive login. Scheduled bulk/elevated tasks are never prematurely marked completed.
5. **Cleaner navigation:** Removed the main-screen Open local logs button. The Diagnostics & reports dialog has compact **Open report queue** and **Open local repair logs** links. All users wizard now labels its passive choice “No action — keep choices for this session”, and its summary includes the exact selected action; it does not promise to save settings across restarts.
6. **Safety retained:** Separate consent, UAC for all-users, pending-task safeguards, no secret reports, supported 2024 Office volume licensing and Stable 1.0.0 remain unchanged. Windows CI validates the UI and binary; live per-user scheduling, logon, and high-DPI validation still require pilot testing.

[Preview.16 release notes](docs/releases/v1.0.1-preview.16.md)

## Preview v1.0.1-preview.15 — guided repair and safe worker cleanup

1. **One Setup entry:** Removed the redundant Settings dialog. The compact **Setup** link opens the All users three-step wizard for scheduled execution, or the Current user wizard for action selection and optional portable EXE cleanup. Nothing runs on Finish alone; independent repair/UAC confirmations remain.
2. **Current-user cleanup:** Only the standard user's own portable Microsoft365AuthRepair.exe is eligible for one-time removal after successful repair and application exit. Installed, network, shared and elevated worker copies are protected.
3. **All-users worker maintenance:** The last wizard step includes on-demand read-only inspection of our Task Scheduler folder. After a separate confirmation, an administrator can delete only completed or expired tasks whose principal and action match the protected app. Pending/running/unknown tasks are retained. ProgramData PS1/EXE are removable only when **no tasks of any kind** remain in our folder. PS1 is launched from the protected `C:\ProgramData\Microsoft365AuthRepair\WamReregisterWorker.ps1` using Task Scheduler under the affected user's interactive token; its own worker attempts to remove its task after execution.
4. **Instant sign-in help:** Removed the large Explain code button. Typing AADSTS, 1001 or an Office sign-in problem immediately provides local matching error explanations and documentation; no report is sent.
5. **Complete KMS key & Preview status:** The full public GVLK occupies a separate responsive line. Hidden WMI Preview records are summarized even when a valid Office 2024 license is already active. Known obsolete Preview keys can be reviewed for independent cleanup **only after** a distinct active production license is verified. Keyless Preview entries are labeled as requiring no removal. No ambiguous or unverified keys are automatically deleted.
6. **Preview-only:** Stable v1.0.0 is untouched. Windows CI checks compile and launch; real KMS/MAK operations, worker removal on a domain workstation and 100–200% DPI EN/RU/HE must still be tested before Stable.

[Preview.15 release notes](docs/releases/v1.0.1-preview.15.md)

## Preview v1.0.1-preview.14 — automatic Office 2024 KMS key recommendations

1. **No manual GVLK lookup:** selecting a verified Office LTSC 2024 Professional Plus or Standard, Visio LTSC 2024 Professional or Standard, or Project 2024 Professional or Standard Volume edition displays the matching Microsoft-published KMS Client Setup Key in a copyable read-only field.
2. **No extra buttons:** the suggested GVLK appears under the existing KMS/MAK choice and is automatically used when the administrator confirms the existing KMS activation flow. The KMS host remains separate and can be detected or entered manually.
3. **Safety:** no GVLK is proposed to unknown 2026 Preview, Retail or Microsoft 365 Apps subscription editions, mismatched installed product IDs, or unsupported Preview licenses. Switching to MAK hides the suggestion and requires the organization's confidential key.
4. **Unchanged consent:** Office LTSC 2024 Preview key removal still requires explicit checkbox and independent destructive confirmation, exact Office product identity and full post-operation license recheck. Stable v1.0.0 remains untouched.
5. **Active Office wins:** Click-to-Run inventory is matched to Office WMI entries, and an already activated Volume license is shown first. Duplicate unlicensed MAK/KMS templates are hidden behind a small “Show other WMI records” link. Active editions and unrelated template records cannot be re-keyed accidentally.
6. **Compact UI:** Settings and the language selector now occupy separately aligned header rows. Office license refresh/KMS discovery use small links rather than large buttons; KMS discovery runs automatically when the dialog opens.
7. Windows x64/ARM64 CI and GUI smoke tests verify the mapping, activated-record selection, responsive header and the new UI. Real KMS/MAK and 100–200% DPI tests still require pilot verification. [Preview.14 release notes](docs/releases/v1.0.1-preview.14.md).

## Preview v1.0.1-preview.13 — Microsoft-guided diagnosis and native Office licensing

1. **Microsoft Learn troubleshooting:** the offline sign-in guide includes symptom, check, possible cause, safe action, and the Microsoft Learn reference for WAM, PRT, Entra, MFA, Conditional Access, network, personal CloudExperienceHost, Office licensing and RDS/VDI SCA. No passwords or logs are uploaded.
2. **Native read-only Office vNext diagnostics:** checks Office Click-to-Run edition, local vNext/SCA indicators and license tool availability using C#/.NET. It never executes ospp.vbs, vnextdiag.ps1 or any external licensing script and does not claim local token files prove a valid subscription.
3. **Office / Visio / Project Volume licensing:** a separate on-demand dialog shows product-specific WMI license status and the last five characters of installed keys. KMS lookup reads the configured/discovered Office host or DNS `_vlmcs._tcp`, with manual host and port (default 1688).
4. **2024 Preview key replacement:** for a strictly matched Office LTSC/Visio/Project Volume 2024 SKU and Preview key, an administrator can explicitly approve removing the old Preview key *before* installing the official Microsoft 2024 GVLK (KMS) or a private MAK. The operation requests a second confirmation, verifies the chosen Office WMI record, does not remove Windows keys, and reports when activation remains unverified.
5. **Safety:** unknown Office/Visio/Project 2026 Preview, Retail, Microsoft 365 subscriptions and unsupported product IDs remain read-only. MAK values are masked and never intentionally saved or included in reports. No bulk or scheduled key removal is available.
6. Windows x64/ARM64 CI and GUI smoke checks must pass. **Live KMS/MAK, UAC, RTL and 100–200% DPI field tests remain required before Stable promotion.** Stable v1.0.0 is unchanged. [Preview.13 release notes](docs/releases/v1.0.1-preview.13.md).

## Preview v1.0.1-preview.12 — All users wizard, collapsible diagnosis and sign-in error guide

1. **All users mini wizard:** Choosing All users opens a 3-step guide: protected EXE or PowerShell (WAM only), start for active sessions or defer to next sign-in, then Save only / Re-register WAM / Reset Office sign-in. The default is Save only; Reset is available with EXE only.
2. **Explicit confirmation after UAC:** A user can continue directly to a repair from the wizard, including for existing domain/local profiles. Elevation carries only a validated request to show the confirmation dialog. Neither the wizard nor UAC auto-runs the repair. Profiles without any Windows profile on the machine are excluded.
3. **Collapse Check & Diagnose:** Collapse results hides all six signal tiles and optional technical details while preserving a compact status line; Expand restores the result. No new diagnostics or uploads are performed when toggling.
4. **Offline sign-in code guide:** Explain sign-in error accepts codes such as AADSTS53003, 1001, and 0x80090016. It distinguishes local repair suggestions from MFA, Conditional Access, tenant/device, and TPM scenarios; never requests credentials or uploads the pasted code.
5. The existing updater, universal diagnostics gateway, scheduling, WAM re-registration and full-reset implementations are unchanged. **Stable v1.0.0** remains untouched.
6. Windows x64/ARM64 CI and UI smoke tests are mandatory; real UAC, scheduling and 100/150/200% RTL/DPI pilot tests are still needed. [Preview.12 release notes](docs/releases/v1.0.1-preview.12.md).

## Preview v1.0.1-preview.11 — readable diagnostic signals and adaptive dialogs

1. **Check & Diagnose:** six localized Entra/WAM signal tiles show AAD BrokerPlugin, Entra join, AD domain, device authentication, Azure AD PRT, and WAM default account. Positive, attention, neutral, and unknown states are visually distinguished and explicitly labeled.
2. **No misleading success:** an unjoined AD/Entra device is presented as neutral rather than automatically broken. Missing or unavailable signals are never interpreted as healthy. The summary does not claim to test real Office/Teams/OneDrive login.
3. **Responsive dialogs:** Settings, Diagnostics & reports, and Send anonymously now use monitor-aware initial sizes. Translated descriptions and consent text wrap instead of clipping at higher DPI.
4. Existing **Preview-first, then Stable** update checks and GitHub/SHA-256 verification remain unchanged. No modification to WAM repair/reset or Google Apps Script report transport. Stable v1.0.0 is untouched.
5. Windows x64/ARM64 CI and x64 GUI smoke tests are required. Live 100/150/200% scaling, Hebrew RTL, and real Entra/WAM field validation still require testing. [Preview.11 notes](docs/releases/v1.0.1-preview.11.md).

## Preview v1.0.1-preview.10 — responsive local report capture and single review

1. **Check & Diagnose** now displays an immediately visible diagnostic summary card in the main window: condition, key findings and clear next steps. Technical details are collapsed, not stretched into a giant empty textbox. The initial main-window height also adapts to the monitor.
2. **Generate report** performs the read-only Entra/WAM collection outside the UI thread, keeping the report window responsive even when AppX or dsregcmd are slow.
2. The redundant raw JSON text panel is removed from **Diagnostics & reports**. The exact allowlisted JSON appears only in the separate **Send anonymously** confirmation dialog.
3. **Send saved report** opens a previously saved report for review. Report creation still never sends anything automatically. The approval checkbox remains initially checked, but explicit **Send anonymously** is required.
4. The compact report window preserves its local queue and status messaging. EN/RU/HE, responsive layout, x64/ARM64 build checks, and the same Google gateway remain in place.
5. Does **not** alter WAM repair/reset behavior, GitHub issue schema, updater installation, or **Stable v1.0.0**. [Preview.10 notes](docs/releases/v1.0.1-preview.10.md).

## Preview v1.0.1-preview.9 — Settings, guided WAM diagnosis and one-step report review

1. Replaces the almost-empty **Advanced** checkbox with a compact **Settings** dialog. All-users scheduling options (EXE / PowerShell, run now / next logon) and optional portable EXE deletion have clear descriptions; destructive reset stays visible on the main window.
2. **Check & Diagnose** now explains read-only AAD BrokerPlugin, device authentication, Azure AD PRT and WAM account signals, their limitations and what to do next. It does not claim to test a real Office login or fix Entra registration.
3. **Generate report** immediately opens a separate reviewed **Send anonymously** dialog. The user-requested **I approve** checkbox defaults checked; **Send anonymously must still be clicked explicitly**. No data is transmitted when opening or generating the report.
4. Redundant **Check receiver** buttons have been removed. Report receipts are validated against GitHub Issues; a failed attempt remains in the local queue with a specific, nonsecret error category.
5. EN / RU / HE UI, accessibility and HiDPI support are included. Stable v1.0.0, WAM repair algorithms, Google gateway and existing tokens are unchanged. [Preview.9 notes](docs/releases/v1.0.1-preview.9.md).

## Preview v1.0.1-preview.8 — clearer repair actions and Windows-style buttons

1. The ambiguous `Repair sign-in` button is now `Re-register WAM`, explicitly separated from a read-only `Check Entra / WAM` action and a destructive `Reset Office sign-in` action.
2. Each action explains whether it only checks status, re-registers AAD BrokerPlugin without removing sign-in caches, or deletes Office/WAM cached identity data and may require signing in again.
3. A current-user WAM re-registration now asks for confirmation before changing AppX registration. Existing UAC, all-users scheduled tasks, and full-reset confirmations remain in place.
4. The reset button is no longer hidden in Advanced; it is shown in a separate cautionary section. Advanced continues to hold optional portable-EXE cleanup.
5. Shared action buttons now paint their entire background to eliminate black rounded corners, with a restrained Windows-inspired 3D/bevel effect and less aggressive corner radius. Icons, keyboard focus, high contrast, RTL, and DPI support remain.
6. Existing repair algorithms are unchanged; GitHub diagnostics Issue #1 confirmed the shared receiver, and all six reported Entra/WAM signals were healthy. This does not test actual Office/Teams/OneDrive sign-in.

Stable v1.0.0 is unchanged. Preview requires Windows visual and behavior QA at 100–200% DPI before stable promotion.

## Preview v1.0.1-preview.7 — scheduled-task execution choice

- Choose between **EXE** (default, Repair + Full reset) and an independent **PowerShell script** (WAM re-registration only) when the scope is All users on this PC.
- Select **Only create tasks — run at next sign-in** to defer even currently logged-on users; uncheck to run immediately for active profiles and queue offline profiles.
- The protected standalone `WamReregisterWorker.ps1` is embedded inside the self-contained app, staged under `%ProgramData%\Microsoft365AuthRepair` with an administrator-owned ACL and run under the actual user's InteractiveToken. It does not require the copied worker EXE to remain present. Script execution policy and availability of AppX cmdlets still apply.
- UAC elevation preserves both chosen task options and never starts an operation without a second explicit confirmation.
- Tasks are only for existing Windows profiles; no first-logon trigger is created for nonexistent users.
- Stable v1.0.0 is unchanged. See [Preview.7 release notes](docs/releases/v1.0.1-preview.7.md).

## Preview v1.0.1-preview.6 — existing Windows profiles only

1. **All users** now targets **only real profiles already registered on this PC**, including offline local/domain accounts with a present user profile directory. New accounts that have never signed in have no old user WAM/Office cache to repair.
2. Preview.5's optional machine-wide first-sign-in campaign is removed. The program no longer accepts `--first-logon`, and an elevated All users run safely deletes its own legacy HKLM Run entry if installed, without touching unrelated startup entries.
3. Already present users get SID-specific Task Scheduler jobs. If online, an interactive user job starts immediately; if offline, their task runs at the next sign-in. The job runs in the **actual user's Windows token**, not SYSTEM/admin, and requires no password.
4. Existing tasks point to a protected worker at `%ProgramData%\Microsoft365AuthRepair\Microsoft365AuthRepair.exe`, **not** a user Downloads copy. Removing the portable app therefore does not break pending jobs. If the *protected worker itself* is deleted, no scheduled task can work without restoring a trusted copy; a GPO-managed deployment can provide that recovery path.
5. A standalone PowerShell script is **not** the recommended fallback for this project because AppX repair already requires the AppX cmdlet and some environments block PowerShell. A dedicated, signed .NET worker plus a managed recovery policy is preferable if stronger resilience is required.
6. The separate checkbox for opt-in deletion of a current user's portable EXE remains available. This must never delete the protected worker while jobs are queued.
7. Stable v1.0.0 remains untouched. Before broad deployment verify actual per-user scheduled tasks on controlled x64/ARM64 workstations, including an offline domain user.

## Preview v1.0.1-preview.5 — automatic EXE update and first-sign-in user support

1. **Update** now performs end-to-end EXE replacement for same-user writable installs, with SHA-256 and PE architecture validation, a trusted out-of-process helper, `.bak` rollback, post-replacement GUI health test and automatic restart. Does not require the operator to extract or copy the EXE manually.
2. **Protected installs**: if a standard user can't replace the EXE in Program Files or our protected ProgramData worker folder, the app asks for UAC. After explicit acceptance, a separate elevated Update Center independently downloads and validates the release then continues the replacement using a **protected helper directory**, never a user-writable elevated helper. No WAM repair is launched by updating. Other nonstandard protected paths fail closed rather than escalating arbitrary code.
3. **All users**: existing Windows profiles (local and domain) use per-SID interactive logon scheduled tasks; active sessions start immediately, offline users run at next sign-in. New profiles that never existed on this PC cannot have WAM repaired before first sign-in.
4. **Include users who have never signed in**: optional (enabled when All users selected), explicit confirmation and administrator rights create a 90-day first-sign-in HKLM Run campaign. Its protected EXE runs within the new user's interactive session, never as SYSTEM/admin, once per new profile. Existing profiles are excluded to prevent double resets. No passwords are stored. No operation runs on a user who never signs in.
5. **Optional one-time portable cleanup**: Advanced can delete *this user's own portable EXE* after a successful current-user repair/reset using an unelevated child helper after process exit; disabled by default. The protected shared EXE is **never** deleted while offline-user tasks may still need it.
6. Window version, progress, concise What’s New, EN/RU/HE and diagnostics preview/consent remain in place.
7. **Stable v1.0.0 unchanged**. Preview CI verifies Windows x64/ARM64 build and GUI contract; live UAC, first-logon task scheduling, EXE replacement and real Microsoft Entra/WAM repair still require pilot testing.

## Preview v1.0.1-preview.4 — user scope and diagnostics repair

1. The scope picker now shows `Current user — DOMAIN\username` (the actual desktop owner, not an unrelated elevated administrator) and `All users on this PC`. The latter means local **and domain** accounts that already have a Windows profile on this PC.
2. Current-user WAM Repair and Full identity reset work as that user **without administrator rights**. Full reset still asks confirmation because it removes that user's Office/WAM sign-in caches.
3. **All users** is selectable without elevation, but executing it shows a scope/UAC warning. Only after operator consent does Windows open an elevated instance with All users selected. Nothing is repaired or reset until the operator clicks the action and confirms again.
4. The elevated process uses per-user scheduled tasks to run actual BrokerPlugin registration/identity-reset in each user's Windows security context, rather than operating under the administrator's SID. Active sessions may be interrupted; offline profiles execute their queued job at their next logon.
5. Advanced displays the target Windows account (or all-user scope), likely sign-out impact, and whether administrator rights are needed. Labels are available in English, Russian and Hebrew.
6. Diagnostics & Reports adds `Check receiver` (read-only GET, no token/report sent) to verify that the **live** shared Google Apps Script deployment advertises gateway schema 3. It distinguishes a missing `GITHUB_M365_ISSUES_TOKEN` script property from old gateway code. Reports stay pending if GitHub delivery is unconfirmed.
7. Stable `v1.0.0` remains unchanged. Preview requires field QA of UAC, per-user scheduled tasks, actual WAM repair, public GitHub Issue creation, and ARM64 runtime.

## Preview 1.0.1-preview.3

1. **Update** replaces "Download & verify": one click downloads the matching x64/ARM64 release with live progress, validates SHA-256/ZIP/architecture, hands off to a copy of the app, waits for the original process to exit, installs with a .bak rollback checkpoint, runs a GUI health check, and restarts.
2. Auto-install **never silently elevates**. When running elevated, from a network location, or from a protected directory, automatic replacement is blocked with an actionable message; the already verified package remains available in Open folder.
3. New consistent rounded Windows buttons for the main app, updater and diagnostics (16 actions), with accessible focus/hover/pressed states, keyboard use, High Contrast fallback and DPI-aware sizing. All three windows verify button styling in CI.
4. Larger main and updater window defaults, Update as the prominent primary action, current-version What's New shown even when there is no newer release, and automatic release checks when opening the dialog or changing channels.
5. Report delivery remains user-approved only. GitHub Issues must confirm delivery; if no issue exists there is no confirmed report.
6. The **Stable v1.0.0** release remains unchanged. This is a preview requiring real-world Windows testing, particularly elevated/protected installs and ARM64.

## Preview 1.0.1-preview.2

1. Fix the Diagnostics & Reports button text (& is no longer interpreted as a mnemonic).
2. Accept the original 0.1.0-preview.1 pending UniversalDiagnostics JSON component label, while validating its original fingerprint and normalizing only the outgoing public metadata.
3. Preserve confirmed GitHub receipt when updating the local queue fails.
4. Continue to show version, update badge, download progress, cancel, and numbered What's New.
5. Stable 1.0.0 is not modified; automatic EXE replacement remains disabled.

## Preview 1.0.1-preview.2 (in development)

- **Stable v1.0.0 is unchanged.** All new features are committed to the preview branch and require QA before promotion.
- Main window title and Update Center title show the full installed application version.
- Background update check highlights newer Stable versions; **Preview + Stable** chooses the highest compatible version across both channels.
- Update Center has native streaming percentage/MiB progress, Cancel, a compact numbered What's New list (up to eight items), and a button to open the verified download folder.
- The common UniversalUpdater.Core performs the actual download and SHA-256/architecture checks. **Automatic in-place EXE replacement is not enabled** until the trusted installer helper is tested.
- Diagnostics & Reports offers local report creation, loading previously queued JSON, strict outgoing JSON preview, an explicit consent checkbox, and optional anonymous submission via the existing Google Apps Script web app.
- The common Google receiver source must be redeployed manually and configured with a separate `GITHUB_M365_ISSUES_TOKEN` secret to write Issues in this repository. The existing Termsrv token and legacy routes remain unchanged.
- Only typed Microsoft Entra/WAM states are sent; free-text summaries, credentials, usernames and device IDs stay local. Public GitHub Issues are not private or authenticated. No automatic send or retry.
- Reference: `sgennadi/UniversalUpdater/docs/WINDOWS_UPDATER_UI_DEFAULT.md` for our shared Windows updater UX standard.

# Microsoft 365 Auth Repair

A Windows .NET 10 WinForms desktop repair utility for Microsoft Entra WAM / `Microsoft.AAD.BrokerPlugin`, Office sign-in caches and Microsoft 365 authentication. Version `1.0.0` **Stable baseline (source and binary release)**. The Windows GUI and x64/ARM64 builds have CI coverage, but real Microsoft Entra/WAM recovery, ARM64 hardware runtime and all-users reset have not yet received field QA. Test on pilot PCs before broad deployment.

## Features

- Standard user: recommended **Repair sign-in** re-registers AAD BrokerPlugin for **that Windows user**, with no administrative prompt and no forced logout. This mirrors a successfully observed manual recovery. This may close the running BrokerPlugin.
- **Advanced -> Full identity reset** clears generic Microsoft/Office credentials (not domain/RDP credentials), Office identity HKCU keys, and LocalAppData WAM/IdentityCache/OneAuth/TokenBroker caches. May sign the user out of Microsoft 365 apps; action requires UI confirmation.
- Elevated admin: choose **Current desktop user** or **All local Windows profiles**. Admin console itself does NOT own the affected user's `HKCU`, Credential Manager or per-user AppX registration. The app instead schedules a one-time Task Scheduler worker using the selected user's interactive token, immediately if signed in or next logon if offline. No user passwords, no per-user login bypass.
- Read-only `dsregcmd /status` diagnostics including `AzureAdJoined`, `DomainJoined`, `DeviceAuthStatus`, `AzureAdPrt`, and `WamDefaultSet`. AADSTS50155 warns that cloud device registration is outside WAM cache repair.
- Local-only log files in `%LOCALAPPDATA%\Microsoft365AuthRepair\Logs`, plus current-user per-task completion markers.
- Modern compact WinForms GUI with PerMonitorV2 DPI, Windows High Contrast, EN / RU / HE interface (technical diagnostics remain English), and Advanced mode.
- Multi-size 16/32/48/64/256 px Windows EXE icon (custom shield/check branding), embedded window/taskbar icons, DPI-redrawn vector action and status glyphs throughout main window, Update Center and Diagnostics, with tooltips and accessibility descriptions. Icon presence is checked in CI UI smoke test.
- **UniversalUpdater** from `sgennadi/UniversalUpdater` (pinned official core) handles GitHub Releases checks, stable/preview SemVer, SHA256 release metadata, download and staging validation, archive safety and PE architecture. In this stable baseline **install is deliberately not wired** until safe close-and-replace helper + rollback are tested; `Download and verify` does not replace a running EXE.
- **UniversalDiagnostics** from `sgennadi/UniversalDiagnocticsModule` (pinned canonical VB embedded adapter) writes redacted schema v1 local reports only after a review and explicit click. No automatic HTTP sending; no GitHub tokens in the binary. A shared verified HTTPS gateway will be connected separately after deployment/security testing.

## Stable baseline 1.0.0

- The first stable release freezes the `preview.2` features and branding; no new reset behavior is introduced.
- Assets: `Microsoft365AuthRepair-win-x64.zip`, `Microsoft365AuthRepair-win-arm64.zip` and `SHA256SUMS.txt`, attached to GitHub Releases under tag `v1.0.0` upon successful release workflow.
- **Do not treat the "Stable" label as enterprise certification.** Test individual Repair, Advanced identity reset and admin all-profiles scheduled tasks on controlled Windows endpoints before deployment.
- Full reset may sign users out of Microsoft 365 services; normal Repair sign-in does not clear identity caches.
- The Update Center supports Stable and Preview checks but **automatic EXE installation remains disabled** pending updater-helper QA.

## Verified CI build

- **Preview.2 reference CI:** [successful x64/ARM64 build and x64 GUI/icon initialization test](https://github.com/sgennadi/Microsoft365AuthRepair/actions/runs/43706415667). Stable release builds perform the same checks.
- Download the **Microsoft365AuthRepair-preview-x64-arm64** artifact from the workflow run (GitHub sign-in may be required).
- The artifact contains `Microsoft365AuthRepair-win-x64.zip`, `Microsoft365AuthRepair-win-arm64.zip` and `SHA256SUMS.txt`. Each architecture ZIP contains one self-contained `Microsoft365AuthRepair.exe`.
- This verifies compilation, icon embedding and basic GUI construction **only**. ARM64 actual-device execution, real Microsoft Entra sign-in repair, all-users scheduling and full identity reset have NOT yet been tested. Do not deploy the full reset to production before pilot validation.
- Release availability is confirmed by the GitHub Releases page; a passing build alone does not create a release.

## Packaging

- .NET 10 WinForms **self-contained single-file**, `win-x64` and `win-arm64`. **WinForms does not support Native AOT**, so this is not an AOT build; no installed .NET runtime is required. Windows 10/11 x64 and supported ARM64 Windows versions are the targets.
- Project/EXE: `Microsoft365AuthRepair` / `Microsoft365AuthRepair.exe`.
- Releases (when published): `Microsoft365AuthRepair-win-x64.zip` and `Microsoft365AuthRepair-win-arm64.zip`, each containing exactly one `Microsoft365AuthRepair.exe`. Matching SHA256SUMS.txt in CI artifacts.
- No EXE binary is included in this source archive. GitHub Actions must build it on Windows with .NET 10 SDK.

## Checkout existing shared modules (no forks or duplicates)

Both canonical repositories are private. For an authorized canonical-source build, use existing GitHub access (`gh auth login`), then:

```powershell
.\scripts\setup-modules.ps1
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-x64 --self-contained true
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-arm64 --self-contained true
```

CI can build without private credentials: it checks out **pinned public snapshots** already published in `sgennadi/TermsrvPatcherUnified` and `sgennadi/BitKeyBridge`. These copies may differ from the private canonical versions. For a canonical-source build, optionally configure Actions secret `MODULES_READ_TOKEN` with read-only Contents access to both private repositories. Never embed this token in EXE or source. Private commit pins are in `MODULES.lock`; public snapshot pins are in `.github/workflows/build.yml`. The Windows runner compiles x64/ARM64 and smoke-tests the x64 GUI; actual ARM64 and domain/Entra repair still require manual QA.

## GitHub publication

The source is published to `sgennadi/Microsoft365AuthRepair`. Existing UniversalUpdater and UniversalDiagnostics repositories are not modified. For future deployments, an authorized operator can run:

```powershell
.\scripts\publish-repo.ps1 -Visibility public
```

This script only creates `sgennadi/Microsoft365AuthRepair` when absent; it is not needed after initial publication. `-Visibility private` is supported, but a private repo's unauthenticated GitHub Releases are not accessible from standard-user updater clients; do not bundle credentials to work around this. Nothing is published automatically by generating these files.

## Safe first testing

1. Compile and run `Microsoft365AuthRepair.exe --ui-self-test` on Windows x64 and ARM64. This checks GUI initialization, not real repair.
2. Test **Diagnose** and **Repair sign-in** as a normal non-admin domain user. Confirm it does not request another admin account.
3. Test admin desktop-user selection under different UAC identities; verify worker runs under the intended SID.
4. Test Advanced full reset in an expendable test profile only; check credential filtering, partial-delete failures, package registration and rerun behavior.
5. Test all-users tasks with offline and multiple RDP sessions; inspect per-user logs rather than treating task creation as a successful repair.
6. Test Update Center against approved test Releases, SHA256, wrong-architecture, rollback and resource locks before enabling installation or publishing stable.

**No `dsregcmd /leave` is called.** Local AD/Entra device membership is not intentionally modified. `DeviceAuthStatus: FAILED` requires separate cloud/Hybrid Join investigation.

## Security limitations

- Full identity reset is intentionally disruptive and cannot guarantee restoration of session tokens, Credential Manager secrets or application caches; the recommended non-destructive ReRegister mode should be tried first.
- The admin all-users workflow depends on Task Scheduler InteractiveToken and requires Windows account sessions. A queued task is not a verified successful repair. Offline profiles are processed upon next sign-in, subject to OS policy.
- Source was initially assembled without a Windows/.NET build environment. Check GitHub Actions for actual compilation status; Windows UI visual QA, ARM64 runtime and real AD/WAM repair are not yet verified.
