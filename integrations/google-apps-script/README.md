# Microsoft365AuthRepair — independent Google Apps Script receiver

This is the extracted receiver previously bundled inside TermsrvPatcherUnified. It retains strict v1 (6 fields), expanded (26 fields), and v2 (29 fields) allowlists, duplicate protection, daily cap of 12 issues, and the dedicated GitHub Issues destination.

## Migration — do not break the currently deployed shared endpoint

1. Create a **new** Apps Script project dedicated to Microsoft365AuthRepair and paste `Code.gs`. Deploy as a web app executing as the owner, with anonymous access if reports are to remain anonymous.
2. Configure Script Property `GITHUB_M365_ISSUES_TOKEN` with a fine-grained GitHub PAT limited to **sgennadi/Microsoft365AuthRepair**, Issues write. Never commit the PAT.
3. GET the new `/exec` URL and confirm `service: Microsoft365AuthRepair diagnostics` and capabilities v1/v2. Test one consented report and verify an Issue in the Microsoft365AuthRepair repository.
4. Update the **Microsoft365AuthRepair application's reporting endpoint** to the new URL, publish and verify delivery. The repository currently uses `src/Microsoft365AuthRepair/GoogleDiagnosticsGateway.cs`; inspect its configuration before changing it.
5. Only after all deployed Microsoft365AuthRepair clients have migrated should the shared Termsrv Apps Script deployment be replaced by Termsrv-only code. Otherwise existing clients will lose report delivery.

This commit only adds standalone receiver source. It does **not** deploy Apps Script, migrate existing clients, change public URLs, or revoke tokens.
