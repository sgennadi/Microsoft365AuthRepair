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
