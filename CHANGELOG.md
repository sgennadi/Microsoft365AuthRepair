## 1.0.2-preview.21 — 2026-11-16 — Preview

1. Corrected misleading 'applications may close' message for read-only Check & Diagnose.
3. Added a persistent live Activity panel under Ready, showing stage results/times with a Copy log link; no raw diagnostics, credentials or identity data enter the clipboard.
3. Run WAM/Entra, Office activation, TPM/BitLocker and HTTPS preflight concurrently with live stage count and elapsed time.
4. Bound total UI wait to 60 seconds; on timeout show available partial evidence and mark unavailable sections not verified.
5. Add Cancel check that stops waiting without changing authentication, credentials or TPM/BitLocker.
6. Reuse blocked native read-only scan tasks within the process; bound inherited PowerShell stdout/stderr draining.
7. Add diagnostic self-test and publish x64/ARM64 only after Windows CI. Stable 1.0.1 is unchanged.

[Preview.21 notes](docs/releases/v1.0.2-preview.21.md)

## 1.0.2-preview.20 — 2026-11-16 — Preview

1. Added read-only `KeySignTest`, `AadRecoveryEnabled`, `TpmProtected` and `NgcSet` Entra user/device health signals; unknown remains unknown.
3. Added local-only TPM lockout inspection using bounded `Get-Tpm`; no TPM reset, protector modification, owner authentication or secret export.
4. Prioritized failed Entra device-key signing/recovery before WAM re-registration, retaining Preview.19 TPM, DPAPI, RWDC and AIK paths.
5. Expanded offline cryptographic error guidance with 0x80090011/30/31/34, EN/RU/HE UI tiles and a privacy regression test for device key IDs.
6. Stable 1.0.1 unchanged; hardware, Entra, BitLocker, 200% DPI and Hebrew RTL pilot remains required.

[Preview.20 notes](docs/releases/v1.0.2-preview.20.md)

## 1.0.2-preview.19 — 2026-11-16 — Preview

1. Read-only TPM readiness, firmware class, OS-volume BitLocker protection/encryption, Memory Integrity and DPAPI ProtectionPolicy inspection; no keys or protectors accessed.
3. Distinguish Office/WAM keyset error 0x80090016, DPAPI encryption 0x80090034 and TPM readiness 0x80090030 from AIK/Autopilot attestation 0x80070490 and timeout 0x800705B4.
4. Advisory writable domain controller discovery for domain DPAPI key-backup issues; does not mutate ProtectionPolicy or treat network failure as TPM damage.
5. Existing AIK enrollment error (where recorded) and EK provisioning warning bit are checked without clearing TPM or issuing an enrollment request.
6. Add a responsive EN/RU/HE TPM & BitLocker inspection dialog within Smart Repair; no BitLocker key exports, TPM reset, Entra leave or automatic credential deletion.
7. Reviewed anonymous reports get only strict TPM, BitLocker, DPAPI, AIK and Windows release categories; regression tests enforce exclusion of recovery keys, firmware data, user and domain identifiers.
7. Version advances to 1.0.2-preview.19 for correct SemVer ordering above Stable 1.0.1. x64/ARM64 and GUI CI required; on-device TPM/BitLocker and RTL/200% DPI testing remain essential.

[Preview.19 notes](docs/releases/v1.0.2-preview.19.md)

## 1.0.1-preview.18 — 2026-11-16 — Preview

1. Introduced an evidence-based Smart Repair recommendation and read-only preflight (running apps, Microsoft HTTPS transport, clock and AppX policy hints).
3. Added separate 2024 Volume and Microsoft 365 subscription activation health. Optional Microsoft-supplied vnextdiag remains read-only and raw identity/license output is never kept.
4. Explicitly confirmed retry of a selected, verified failed WAM task only at the next sign-in; never auto-retry an Office identity reset.
5. Optional Entra sign-in audit via Graph Correlation ID, Device Code or certificate, adapted from BitKeyBridge. Enforces audit-only Graph permissions and memory-only session reuse.
6. Post-repair local recheck and distinct user-reported application sign-in verification, without claiming a cloud sign-in was proven.
7. Compact localized EN/RU/HE UI, DPI-aware Smart Repair and verification controls.
7. Added pure decision and security-policy self-tests. Stable 1.0.0 unchanged; live Microsoft 365/Entra and 100/150/200% DPI pilot still required.

## 1.0.1-preview.17 — 2026-11-15 — Preview

1. Added a localized read-only All users status viewer with refresh and wrapped HiDPI table.
3. Record per-task metadata in protected local ProgramData and cross-check per-user attempted/success/failure markers against current Task Scheduler state.
4. Treat absent tasks, missing receipts and scheduler-only completion as unconfirmed; a WAM worker success is never proof of Office sign-in.
5. Do not auto-clean error/attention tasks. Preserve queued/running workers and existing administrator-only explicit cleanup.
6. Rename the All users main check to “Check this account only” to avoid confusing account-scoped health diagnostics with bulk task tracking.
7. Add UI/logic smoke tests. Stable 1.0.0 remains untouched; 200% DPI, RTL, and live domain task execution require a Windows pilot.

## 1.0.1-preview.16 — 2026-11-15 — Preview

1. Removed unused Current user wizard, portable EXE self-deletion and cleanup helper command line; current-user repair stays direct.
3. Hid duplicate repair buttons in All users mode; its setup wizard remains the single bulk-operation entry point.
4. Added existing-profile counts and estimated immediate/next-logon task totals to the bulk-action confirmation, using the reviewed profile list for scheduling.
5. Automatically repeat read-only WAM/Office health checks after a successful direct current-user repair, never for queued all-user tasks.
6. Moved Open local logs into compact Diagnostics & reports links, removed one oversized main-screen button.
7. Renamed the wizard's passive choice to accurately describe session-only preferences, and included intended action in the summary.
7. Stable v1.0.0 unchanged. GUI/CI success is not proof of real Office sign-in or Task Scheduler completion.

## 1.0.1-preview.15 — 2026-11-15 — Preview

1. Live, offline error-code and problem-name search removes the oversized Explain button.
3. Show full copyable Office 2024 GVLK in its own row and surface hidden WMI Preview entries, with safe review/cleanup only for strictly identified Preview keys.
4. Replace Settings with scope-aware Setup: Current user includes opt-in portable cleanup; All users retains method, schedule and action in its mini wizard.
5. Add optional administrator-only review and cleanup of verified completed/expired per-user Task Scheduler jobs. Shared PS1/EXE workers can be removed only when no tasks remain; pending and running users stay protected.
6. Preserve independent repair/UAC/destructive-action approvals and Stable v1.0.0.

## 1.0.1-preview.14 — 2026-11-15 — Preview

1. Automatically suggest the official Microsoft KMS Client Setup Key (GVLK) for six verified Office, Visio and Project 2024 Volume editions.
3. Show the public GVLK in a read-only, copyable inline field; select it automatically when KMS mode is used.
4. Block GVLK suggestions for Retail, subscription, unknown Preview 2026 and mismatched Office product identities.
5. Automatically prioritize the activated Office record over unlicensed WMI template entries, and block unnecessary key changes when an edition is already licensed.
6. Make KMS discovery automatic, replace large Refresh/Find KMS buttons with compact links, and reduce unused dialog space.
7. Separate Settings and the labeled Language selector in the main header, with DPI-aware text wrapping.
7. Keep manual KMS configuration and explicit Preview-key removal consent; Stable v1.0.0 untouched.

## 1.0.1-preview.13 — 2026-11-15 — Preview

1. Added source-linked Microsoft Learn troubleshooting paths with a local sign-in-code and symptom browser.
3. Implemented C# read-only vNext, SCA and Click-to-Run licensing checks without VBS/PS1.
4. Added Office/Project/Visio 2024 Volume licensing inventory, optional DNS-based KMS discovery and manual KMS/MAK entry.
5. Added a strict Office-only identity gate, last-five Preview key recognition, explicit removal approval and second confirmation before removing a verified Preview key before KMS/MAK replacement.
6. Protected unrelated Office/Windows products, unsupported 2026 Preview and Microsoft 365 subscriptions from automatic key changes. Stable unchanged.

## 1.0.1-preview.12 — 2026-11-15 — Preview

1. Added a three-step All users setup wizard opened on scope selection, with EXE vs PowerShell, task timing, and a safe optional follow-on action for existing Windows profiles.
3. Continued the chosen action through UAC as a non-executing review intent, requiring the existing separate confirmation.
4. Added Collapse results / Expand results for the main Check & Diagnose panel without losing the last result or technical details.
5. Added an EN/RU/HE offline sign-in error-code explainer for WAM, PRT, MFA, Conditional Access, account, and TPM cases.
6. Preserved established repair, scheduling, updater and reporting paths and Stable 1.0.0.

## 1.0.1-preview.11 — 2026-11-15 — Preview

1. Added six compact diagnostic signal tiles with localized status text, without revealing device or user identifiers.
3. Represented absence of AD/Entra join as neutral (configuration-specific), and unavailable values as unknown rather than healthy.
4. Auto-sized Settings, Diagnostics & Reports, and Send anonymously to the monitor work area; wrapped long RU/HE text and consent.
5. Kept raw technical diagnostics behind Show technical details. No change to repair, reset, updater, Google receiver, or Stable v1.0.0.

## 1.0.1-preview.10 — 2026-11-15 — Preview

1. Make local report capture asynchronous to avoid freezing the WinForms diagnostics screen during AppX or dsregcmd checks.
3. Remove redundant inline raw JSON preview; Send anonymously remains the only exact JSON review screen.
4. Rename saved-report action for clarity and prevent simultaneous collection via the report window controls.
5. Preserve explicit user Send action, Google gateway contract, existing stable release and WAM repair paths.

## 1.0.1-preview.7 — 2026-11-15 — Preview

1. Added EXE versus standalone PowerShell worker choice for scheduled WAM re-registration across existing profiles.
3. Added "Only create tasks — run at next sign-in" mode so active users are not disturbed immediately.
4. PowerShell worker remains functional after deletion of the original EXE, supports only non-destructive re-registration and respects execution policy.
5. UAC handoff preserves task mode and deferred scheduling selection without auto-running.
6. Extended x64 GUI self-test and Windows CI with protected PowerShell resource/syntax checks; stable release unchanged.

## 1.0.1-preview.6 — 2026-11-15 — Preview

1. Restricted All users processing to Windows profiles already present on this PC; no first-ever sign-in repair.
3. Removed the unnecessary new-profile HKLM Run deployment and the --first-logon worker path.
4. Added safe legacy HKLM Run cleanup for administrator-initiated all-users operations.
5. Retained protected ProgramData worker EXE and SID-specific scheduled logon tasks, independent of the user's portable EXE path.
6. Extended CI to verify valid-looking legacy first-logon commands cannot run.

## 1.0.1-preview.5 — 2026-11-15 — Preview

1. Update replaces the EXE automatically with the canonical UniversalUpdater transaction, rollback and health-check before restart.
3. Privileged installations continue through explicit UAC and a protected admin-only helper, rather than forcing manual ZIP extraction.
4. All users supports future first-time sign-ins through an optional 90-day per-machine, one-time-per-new-profile campaign, excluding already-queued existing profiles.
5. Optional portable EXE self-deletion after a successful current-user operation; the protected shared worker remains available for offline users.
6. Added worker argument and safety self-tests, while keeping the stable release, diagnostic consent and normal non-admin current-user repair unchanged.

## 1.0.1-preview.4 — 2026-11-15 — Preview

1. Show the actual current desktop username beside the scope selector.
3. Rename All local Windows profiles to All users on this PC (including locally cached domain profiles).
4. Keep current-user repair/reset non-elevated. Request UAC only on a confirmed all-users operation; never execute immediately after elevation.
5. Clearly identify scope, rights and reauthentication impact in Advanced.
6. Add a read-only Check receiver and actionable reporting errors for missing Google Apps Script configuration; never mark an unconfirmed report as delivered.

## 1.0.1-preview.3 — 2026-11-15 — Preview

1. Replaced Download & verify with a one-click Update and same-user transactional updater helper.
3. Added post-update GUI health check, automatic relaunch and .bak rollback on failure.
4. Unified 16 modern rounded buttons across Main, Updates and Diagnostics, with HiDPI and keyboard accessibility.
5. Simplified channel switching and update checks; current-version changelog is available even without a new update.
6. No change to Stable v1.0.0, WAM recovery, or the consent requirement for diagnostics.

## 1.0.1-preview.2 — 2026-11-15

1. Moved Diagnostics & Reports beside Repair and fixed literal button ampersands.
3. Backward-compatible loading of original pending WAM report format.
4. Improved handling of locally saved GitHub delivery receipts.

## 1.0.1-preview.1 — 2026-11-15 — Preview

1. Added update progress (percentage, transferred MiB and Cancel).
3. Show installed version in title bars and available update on the main button.
4. Simplified Update Center; fixed Preview + Stable checks and short numbered What's New.
5. Integrated shared anonymous diagnostics submission with user consent, strict preview and legacy pending-report support.
6. Stable 1.0.0 remains unchanged.

# Changelog

## 1.0.0 — 2026-11-15 — Stable baseline
- Promoted `0.1.0-preview.2` to the first stable baseline without changing repair/reset behavior.
- Kept .NET 10 self-contained x64/ARM64 packaging and Windows WinForms GUI.
- Kept custom multi-size application icon and DPI-aware UI glyphs.
- Included UniversalUpdater and UniversalDiagnostics through pinned upstream snapshots, with canonical-source option.
- Added release assets and SHA-256 checks via GitHub Actions.
- Known limitations: functional WAM/Entra/domain and ARM64-hardware testing still pending; automatic updates still do not install.

## 0.1.0-preview.2
- Added branded ICO and vector action/status icons; CI GUI/icon smoke test.

## 0.1.0-preview.1
- Initial project with current/all-user repair workflow, diagnostics, updater staging and Windows x64/ARM64 CI.
