# Explicit local opt-in: requires gh auth login and GitHub permission to create repositories.
[CmdletBinding()]
param([ValidateSet('private','public')][string]$Visibility = 'public')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
  & gh auth status
  if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login first.' }
  & gh repo view 'sgennadi/Microsoft365AuthRepair' --json name 2>$null
  if ($LASTEXITCODE -eq 0) { throw 'Repository already exists. Refusing to overwrite or force-push.' }
  if (-not (Test-Path '.git')) { & git init -b main }
  & git add .
  & git commit -m 'Initial Microsoft 365 Auth Repair .NET 10 WinForms preview'
  if ($LASTEXITCODE -ne 0) { throw 'Commit failed. Set Git author name/email and retry.' }
  & gh repo create 'sgennadi/Microsoft365AuthRepair' "--$Visibility" --source . --remote origin --push
  if ($LASTEXITCODE -ne 0) { throw 'GitHub repository creation failed.' }
  Write-Host 'Published: https://github.com/sgennadi/Microsoft365AuthRepair'
} finally { Pop-Location }
