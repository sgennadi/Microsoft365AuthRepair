# Build, deployment and verification

Technical information for **Microsoft 365 Auth Repair**. The [README](../README.md) contains the concise end-user overview.

## Build from source

The application uses .NET 10 and Windows Forms. Initialize the pinned shared modules and publish a self-contained binary:

```powershell
.\scripts\setup-modules.ps1
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-x64 --self-contained true
dotnet publish .\src\Microsoft365AuthRepair\Microsoft365AuthRepair.csproj -c Release -r win-arm64 --self-contained true
```

Shared modules are maintained in their canonical repositories, not forked here. See [MODULES.lock](../MODULES.lock) for reviewed pins. GitHub Actions checks out pinned public snapshots of UniversalUpdater and UniversalDiagnostics; `MODULES_READ_TOKEN`, when available, provides read-only access to canonical private snapshots. Do not add credentials, PATs or tokens to the source or bundled EXE.

## CI and release verification

The Windows workflow builds self-contained x64 and ARM64 executables, runs Windows x64 GUI/policy self-tests, parses the protected PowerShell fallback without executing it, rejects invalid background-worker arguments and checks both ZIP contents and SHA-256 hashes before publishing a Stable release.

For a read-only GUI/policy smoke test on Windows:

```powershell
.\Microsoft365AuthRepair.exe --ui-self-test
```

This is a structural smoke test. It does **not** prove successful Microsoft Entra authorization, Office sign-in, KMS/MAK activation, real domain-task execution or behavior on physical ARM64 hardware. Perform a pilot at 100%, 150% and 200% scaling, including Hebrew RTL.

## Anonymous diagnostic gateway

The local report contains 29 strictly enumerated categories from Entra/WAM, Office licensing and device security. Sending is disabled unless the existing shared Google Apps Script endpoint reports:

- `gateway_schema_version: 4`
- `microsoft365-auth-repair/v2`

The reviewed receiver is [Code.gs](https://github.com/sgennadi/TermsrvPatcherUnified/blob/main/integrations/google-apps-script/Code.gs). A GitHub merge does **not** deploy the running script. The script owner must update the **existing** Apps Script project and select **Deploy → Manage deployments → Edit → New version → Deploy**, then verify the existing `/exec` endpoint. Reuse the deployment URL and server-side tokens; do not publish them. The outer event envelope remains `universal-diagnostics/v1`.

An approved report creates a **public GitHub Issue**. No raw WMI/Graph responses, user or device identifiers, recovery keys, license keys, hostnames, file paths or account tokens are sent. Sending always requires explicit user review and the Send action; no automatic upload or retry.

## Repair and operating boundaries

- Initial read-only checks should run in the affected user's own interactive session, without UAC when possible.
- WAM re-registration differs from full Office sign-in cache reset. A full reset is disruptive and requires reauthentication.
- The All users wizard targets existing local profiles with narrowly controlled scheduled workers. Worker success is not proof of successful application authentication.
- Read-only TPM/BitLocker, Secure Boot, HVCI, DPAPI, domain/DC and Windows environment status can be incomplete or unknown. Neither UNKNOWN nor a successful local scan proves the root cause.
- Administrator-only BitLocker actions use explicit additional confirmation. TPM Clear is not automatically performed by this application.
- Optional Microsoft Graph Correlation ID lookup is read-only, using Device Code or a separate approved audit-only certificate application with `AuditLog.Read.All`. No ROPC and no reuse of more privileged apps.
- Office/Visio/Project Volume licensing tools distinguish installed products from licensing WMI templates, avoid unrelated products and require explicit review for key removal or replacement.

For user-visible changes consult [Stable release notes](releases/v1.0.3.md) and the [changelog](../CHANGELOG.md).
