# Microsoft 365 Auth Repair

A portable, self-contained **Windows .NET 10 WinForms** utility for diagnosing and safely recovering Microsoft Entra/WAM, `Microsoft.AAD.BrokerPlugin`, Office application sign-in and supported Office LTSC licensing. Current **Stable: 1.0.3** (x64 / ARM64).

## Download

The latest signed-off release and SHA-256 manifest are at [GitHub Releases](https://github.com/sgennadi/Microsoft365AuthRepair/releases/latest). Download `Microsoft365AuthRepair-win-x64.zip` for Intel/AMD or `Microsoft365AuthRepair-win-arm64.zip` for ARM64; extract the included single EXE. **No .NET installation is necessary.** Never run an unknown EXE without verifying its source and checksum.

## What it does

- **Smart Repair:** read-only WAM/BrokerPlugin, Entra device trust/PRT and Office inventory checks, producing an evidence-based next action. The program distinguishes a broken user broker from device trust, network, policy, Conditional Access and licensing issues. It never resets accounts because a single indicator is missing.
- **Preflight:** checks for active Office/Teams processes, Microsoft login HTTPS transport reachability, Windows clock discrepancy and relevant AppX policy hints. It does not attempt a Microsoft 365 sign-in or modify a tenant.
- **Current user:** direct, explicitly confirmed WAM re-registration or optional *full Office identity reset*. Full reset is disruptive; save Office work and close apps first. Launch without UAC when diagnosing the affected user.
- **All users:** one three-step administrator wizard for profiles **already existing on the device**. Schedules narrowly validated one-shot workers in each affected user's own interactive session. Task status is kept in protected machine-local history and per-user completion markers. A failed WAM registration may be retried **only** for the selected user after explicit approval, at the next sign-in; a full reset is never silently retried.
- **Office activation:** identifies installed Office / Visio / Project 2024 Volume editions using Click-to-Run inventory and WMI, separates KMS/MAK from Microsoft 365 subscription indicators, suggests only matching public Microsoft 2024 GVLKs, and allows explicitly approved changes for safely verified obsolete Preview keys. Active production licenses are preserved; unsupported 2026 Preview products are read-only.
- **Office WMI clarity:** multiple `SoftwareLicensingProduct` entries are *licensing records*, not multiple Office installations. The normal list shows one preferred installed edition; optional technical records (including unlicensed templates and Preview markers) are clearly labeled.
- **Optional Microsoft Graph:** one in-process Device Code or certificate authorization, adapted from BitKeyBridge's session handling for **AuditLog.Read.All only**. Correlation ID lookup is read-only and needs an approved audit-only Entra application and appropriate reader rights. No ROPC, Graph write access, BitLocker/LAPS role borrowing or disk-stored OAuth tokens.
- **Verify Repair:** automatically rechecks local prerequisites after successful direct user repair; actual Word, Outlook or Teams sign-in is considered confirmed only when the user says it worked.
- **Device security (read-only):** independently time-limited TPM 2.0, BitLocker OS-drive protection/encryption, Secure Boot, Memory Integrity, Windows VM status and DPAPI/domain evidence. Unknown, unverified or partial results are not presented as hardware failure. The main window shows WAM/Office results **before** the separate TPM/BitLocker card; both the activity log and WAM/Office sections can be expanded or collapsed. The window automatically fits its **height** to the visible content, up to the active monitor's work area, and preserves manually resized/maximized windows.
- **Optional advanced device actions:** a separately confirmed administrator-only window supports carefully restricted OS BitLocker operations and recovery-password export to a new protected local NTFS file. TPM Clear is **never automated**. Device security changes are not needed for ordinary WAM repair.
- **Diagnostics and reports:** generates a local report, then opens a separate review dialog with an explicit **Send anonymously** action. The JSON initially appears **unselected** for readability; users may select and copy it manually. The report stays local until Send is clicked. Approved uploads create **public GitHub Issues** containing only 29 fixed, categorical Entra/WAM, Office licensing and device-security indicators; no account names, SIDs, license or MAK keys, recovery passwords, hostnames, tenant identifiers, paths or raw WMI/Graph responses are sent. The shared Apps Script gateway must expose schema version 4 with `microsoft365-auth-repair/v2`; older gateways are rejected before any POST.
- **Interface and updates:** English, Russian and Hebrew, RTL support, PerMonitorV2 scaling, Windows High Contrast, compact native controls, numbered What's New, Stable/Preview SemVer precedence, validated architecture/SHA-256 and rollback-aware update helper.

## First test on a Windows pilot device

1. Extract the x64/ARM64 ZIP and run `Microsoft365AuthRepair.exe` as the affected **ordinary user**.
2. Click **Check & Diagnose**. Read the Smart recommendation and Office licensing summary. The check should not trigger UAC, logout, token deletion or account changes.
3. Open **Office / Visio / Project licensing**. If activated, no key replacement is needed. **Show technical license records** is diagnostic-only and can include duplicate activation entries, unlicensed templates or Preview records.
4. Open **Diagnostics & reports**. Review the *entire* anonymous JSON (including WAM, Office and device indicators) before choosing **Send anonymously** or **Keep local**. The JSON is not highlighted by default. You never need to share recovery or license keys.
5. Test targeted WAM repair on a disposable profile first, followed by an actual Office sign-in. Test All users tasks, task cleanup, activation changes and full identity reset **only** in an isolated controlled environment.

For a no-op GUI regression check, run `Microsoft365AuthRepair.exe --ui-self-test` on a Windows test machine. This checks layout creation, report-selection defaults, collapse rows and local policies; it does not prove Entra sign-in or KMS success.

## Anonymous diagnostic receiver

The Windows EXE checks the already-established shared Google Apps Script gateway before submitting. Its M365 route requires **gateway schema 4** and **`microsoft365-auth-repair/v2`** capability, corresponding to the reviewed [Code.gs](https://github.com/sgennadi/TermsrvPatcherUnified/blob/main/integrations/google-apps-script/Code.gs). Only the Google Apps Script owner can deploy it: **Deploy → Manage deployments → Edit → New version → Deploy** on the existing Web App. No URL or PAT needs changing. A GitHub merge does not deploy Apps Script. Until the deployed endpoint advertises v4, sending is disabled with a deployment explanation; generating and keeping reports locally remains available. No automatic upload or silent retry.

## Security and support boundaries

- **No `dsregcmd /leave`** or automatic device rejoin is performed. A device trust error needs separate Entra/Hybrid Join investigation.
- Full identity reset can sign the selected user out of Microsoft 365 applications and remove local sign-in caches; do not run on a healthy production profile.
- Actual Azure/Entra consent, KMS/MAK activation, real domain multi-user execution, hardware ARM64 runtime and visual verification at 100/150/200% DPI (including Hebrew RTL) are **not covered by Windows CI**. **Stable is a release channel, not enterprise certification.** Pilot before broad deployment.
- License keys, credentials and OAuth tokens are not needed for ordinary read-only diagnosis. Certificate auth requires a **separate audit-only** Entra app with approved permissions. Do not re-use BitKeyBridge's more privileged Graph app.
- Successful local WAM registration and a Task Scheduler completion receipt do not prove cloud Microsoft 365 authentication.

## Developer build

Both shared modules belong to their canonical repositories; they are **not forked** in this project:

```powershell
.\scripts\setup-modules.ps1
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-x64 --self-contained true
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-arm64 --self-contained true
```

CI checks out pinned public source snapshots from TermsrvPatcherUnified and BitKeyBridge unless it has the optional read-only `MODULES_READ_TOKEN` required for the canonical private source snapshots. See `MODULES.lock` for reviewed module commit pins. Do not place secrets in source or an EXE.

Windows GitHub Actions builds both architectures, runs x64 GUI/self-tests, parses the protected PowerShell fallback without executing it, rejects invalid worker requests and verifies that each release contains exactly one matching EXE with SHA-256 checks.

## Releases and history

- [Latest Stable](https://github.com/sgennadi/Microsoft365AuthRepair/releases/latest)
- [v1.0.3 release notes](docs/releases/v1.0.3.md)
- [Previous v1.0.2 release notes](docs/releases/v1.0.2.md)
- [Previous v1.0.1 release notes](docs/releases/v1.0.1.md)
- [Full changelog](CHANGELOG.md)

