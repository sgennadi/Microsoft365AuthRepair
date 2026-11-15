## 1.0.1-preview.7 — 2026-11-15 — Preview

1. Added EXE versus standalone PowerShell worker choice for scheduled WAM re-registration across existing profiles.
2. Added "Only create tasks — run at next sign-in" mode so active users are not disturbed immediately.
3. PowerShell worker remains functional after deletion of the original EXE, supports only non-destructive re-registration and respects execution policy.
4. UAC handoff preserves task mode and deferred scheduling selection without auto-running.
5. Extended x64 GUI self-test and Windows CI with protected PowerShell resource/syntax checks; stable release unchanged.

## 1.0.1-preview.6 — 2026-11-15 — Preview

1. Restricted All users processing to Windows profiles already present on this PC; no first-ever sign-in repair.
2. Removed the unnecessary new-profile HKLM Run deployment and the --first-logon worker path.
3. Added safe legacy HKLM Run cleanup for administrator-initiated all-users operations.
4. Retained protected ProgramData worker EXE and SID-specific scheduled logon tasks, independent of the user's portable EXE path.
5. Extended CI to verify valid-looking legacy first-logon commands cannot run.

## 1.0.1-preview.5 — 2026-11-15 — Preview

1. Update replaces the EXE automatically with the canonical UniversalUpdater transaction, rollback and health-check before restart.
2. Privileged installations continue through explicit UAC and a protected admin-only helper, rather than forcing manual ZIP extraction.
3. All users supports future first-time sign-ins through an optional 90-day per-machine, one-time-per-new-profile campaign, excluding already-queued existing profiles.
4. Optional portable EXE self-deletion after a successful current-user operation; the protected shared worker remains available for offline users.
5. Added worker argument and safety self-tests, while keeping the stable release, diagnostic consent and normal non-admin current-user repair unchanged.

## 1.0.1-preview.4 — 2026-11-15 — Preview

1. Show the actual current desktop username beside the scope selector.
2. Rename All local Windows profiles to All users on this PC (including locally cached domain profiles).
3. Keep current-user repair/reset non-elevated. Request UAC only on a confirmed all-users operation; never execute immediately after elevation.
4. Clearly identify scope, rights and reauthentication impact in Advanced.
5. Add a read-only Check receiver and actionable reporting errors for missing Google Apps Script configuration; never mark an unconfirmed report as delivered.

## 1.0.1-preview.3 — 2026-11-15 — Preview

1. Replaced Download & verify with a one-click Update and same-user transactional updater helper.
2. Added post-update GUI health check, automatic relaunch and .bak rollback on failure.
3. Unified 16 modern rounded buttons across Main, Updates and Diagnostics, with HiDPI and keyboard accessibility.
4. Simplified channel switching and update checks; current-version changelog is available even without a new update.
5. No change to Stable v1.0.0, WAM recovery, or the consent requirement for diagnostics.

## 1.0.1-preview.2 — 2026-11-15

1. Moved Diagnostics & Reports beside Repair and fixed literal button ampersands.
2. Backward-compatible loading of original pending WAM report format.
3. Improved handling of locally saved GitHub delivery receipts.

## 1.0.1-preview.1 — 2026-11-15 — Preview

1. Added update progress (percentage, transferred MiB and Cancel).
2. Show installed version in title bars and available update on the main button.
3. Simplified Update Center; fixed Preview + Stable checks and short numbered What's New.
4. Integrated shared anonymous diagnostics submission with user consent, strict preview and legacy pending-report support.
5. Stable 1.0.0 remains unchanged.

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
