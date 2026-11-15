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
