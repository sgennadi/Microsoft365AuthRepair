# Microsoft 365 Auth Repair

A Windows .NET 10 WinForms desktop repair utility for Microsoft Entra WAM / `Microsoft.AAD.BrokerPlugin`, Office sign-in caches and Microsoft 365 authentication. Version `0.1.0-preview.1` **SOURCE PREVIEW — NOT RELEASED OR TESTED ON WINDOWS**.

## Features

- Standard user: recommended **Repair sign-in** re-registers AAD BrokerPlugin for **that Windows user**, with no administrative prompt and no forced logout. This mirrors a successfully observed manual recovery. This may close the running BrokerPlugin.
- **Advanced -> Full identity reset** clears generic Microsoft/Office credentials (not domain/RDP credentials), Office identity HKCU keys, and LocalAppData WAM/IdentityCache/OneAuth/TokenBroker caches. May sign the user out of Microsoft 365 apps; action requires UI confirmation.
- Elevated admin: choose **Current desktop user** or **All local Windows profiles**. Admin console itself does NOT own the affected user's `HKCU`, Credential Manager or per-user AppX registration. The app instead schedules a one-time Task Scheduler worker using the selected user's interactive token, immediately if signed in or next logon if offline. No user passwords, no per-user login bypass.
- Read-only `dsregcmd /status` diagnostics including `AzureAdJoined`, `DomainJoined`, `DeviceAuthStatus`, `AzureAdPrt`, and `WamDefaultSet`. AADSTS50155 warns that cloud device registration is outside WAM cache repair.
- Local-only log files in `%LOCALAPPDATA%\Microsoft365AuthRepair\Logs`, plus current-user per-task completion markers.
- Modern compact WinForms GUI with PerMonitorV2 DPI, Windows High Contrast, EN / RU / HE interface (technical diagnostics remain English), and Advanced mode.
- **UniversalUpdater** from `sgennadi/UniversalUpdater` (pinned official core) handles GitHub Releases checks, stable/preview SemVer, SHA256 release metadata, download and staging validation, archive safety and PE architecture. In this preview **install is deliberately not wired** until safe close-and-replace helper + rollback are tested; `Download and verify` does not replace a running EXE.
- **UniversalDiagnostics** from `sgennadi/UniversalDiagnocticsModule` (pinned canonical VB embedded adapter) writes redacted schema v1 local reports only after a review and explicit click. No automatic HTTP sending; no GitHub tokens in the binary. A shared verified HTTPS gateway will be connected separately after deployment/security testing.

## Packaging

- .NET 10 WinForms **self-contained single-file**, `win-x64` and `win-arm64`. **WinForms does not support Native AOT**, so this is not an AOT build; no installed .NET runtime is required. Windows 10/11 x64 and supported ARM64 Windows versions are the targets.
- Project/EXE: `Microsoft365AuthRepair` / `Microsoft365AuthRepair.exe`.
- Releases (when published): `Microsoft365AuthRepair-win-x64.zip` and `Microsoft365AuthRepair-win-arm64.zip`, each containing exactly one `Microsoft365AuthRepair.exe`. Matching SHA256SUMS.txt in CI artifacts.
- No EXE binary is included in this source archive. GitHub Actions must build it on Windows with .NET 10 SDK.

## Checkout existing shared modules (no forks or duplicates)

Both common repositories are private. Use existing GitHub access (`gh auth login`), then:

```powershell
.\scripts\setup-modules.ps1
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-x64 --self-contained true
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-arm64 --self-contained true
```

The CI workflow requires a **GitHub Actions secret** `MODULES_READ_TOKEN` with **read-only Contents** access to the two private module repositories. Do not include this token in the EXE or a checked-in file. Source commit pins are in `MODULES.lock`. Windows runners are used to compile and smoke-test; no production cleanup runs in CI.

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
- The source was statically assembled in an environment without the .NET SDK or Windows GUI; compilation, GUI visual QA and actual WAM/AD behavior **remain unverified**.
