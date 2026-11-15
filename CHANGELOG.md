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
