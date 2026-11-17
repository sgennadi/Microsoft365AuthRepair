# Microsoft 365 Auth Repair

**Stable v1.0.3** · Windows x64 / ARM64 · Portable .NET 10 application · English / Русский / עברית

A Windows utility for diagnosing **Microsoft Entra, Windows Account Manager (WAM), Office sign-in and Office licensing**. It explains what needs attention before offering a repair. Read-only checks do not change account, TPM or BitLocker settings.

## Download

| Platform | Download |
| --- | --- |
| Windows x64 (Intel / AMD) | [Microsoft365AuthRepair-win-x64.zip](https://github.com/sgennadi/Microsoft365AuthRepair/releases/download/v1.0.3/Microsoft365AuthRepair-win-x64.zip) |
| Windows ARM64 | [Microsoft365AuthRepair-win-arm64.zip](https://github.com/sgennadi/Microsoft365AuthRepair/releases/download/v1.0.3/Microsoft365AuthRepair-win-arm64.zip) |

[Latest Stable](https://github.com/sgennadi/Microsoft365AuthRepair/releases/latest) · [SHA-256 checksums](https://github.com/sgennadi/Microsoft365AuthRepair/releases/download/v1.0.3/SHA256SUMS.txt) · [Release notes](docs/releases/v1.0.3.md)

Extract the ZIP and run **`Microsoft365AuthRepair.exe`**. No installer or separate .NET runtime is required.

## Features

- **Check & Diagnose:** Entra device trust/PRT, WAM/BrokerPlugin, Office sign-in prerequisites, network and policy hints, with a recommended next action.
- **Repair:** Confirmed WAM re-registration for the current user; optional, disruptive Office sign-in reset. Administrator wizard for existing user profiles.
- **Office / Visio / Project:** Office 2024 Volume licensing inventory, KMS/MAK assistance and guarded handling of obsolete Preview keys. Microsoft 365 subscription indicators remain separate.
- **TPM & BitLocker:** Independent, read-only checks for TPM, system-drive encryption, Secure Boot, Memory Integrity and DPAPI/domain state. Advanced device actions require separate administrator approval.
- **Diagnostics & reports:** Local report preview and an optional, explicitly approved anonymous submission of fixed categorical indicators.
- **Usability:** English, Russian and Hebrew (RTL); high-DPI layout, automatic height within the monitor's work area, activity log and an update checker.

## Quick start

1. Run the EXE **as the affected Windows user** for current-user diagnostics; administrator privileges are not needed for the initial check.
2. Select **Check & Diagnose**. Review WAM/Office results, device-security status and the recommendation.
3. Try signing in to the affected Word, Outlook or Teams application. A healthy local check alone does **not** prove Microsoft 365 cloud sign-in.
4. If repair is recommended, review its impact and confirm the specific action. Test disruptive resets or All users actions in a controlled environment first.

## Safety and privacy

- Diagnosis is read-only. The app does **not** automatically clear TPM, disable BitLocker, run `dsregcmd /leave` or rejoin a device.
- **Reset Office sign-in** removes local identity caches and requires signing in again. BitLocker operations and recovery-key export are separately gated.
- Reports stay local unless you click **Send anonymously**. Approved reports create a **public GitHub Issue**, so review the exact JSON first. Raw logs, recovery/password keys, user/tenant identifiers and hostnames are not included.
- Anonymous sending requires the separately deployed **Google Apps Script Gateway v4** (`microsoft365-auth-repair/v2`). An older receiver cannot accept the expanded report.

## Documentation

[Stable v1.0.3 notes](docs/releases/v1.0.3.md) · [Changelog](CHANGELOG.md) · [Build, deployment and verification](docs/DEVELOPMENT.md)

**Stable** identifies the release channel, not a guarantee of tenant compatibility. Validate on a pilot device before broad deployment.
