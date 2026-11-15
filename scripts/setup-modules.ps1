# Bootstrap the two EXISTING shared private repositories; does not create alternate modules.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modules = Join-Path $root 'modules'
New-Item -ItemType Directory -Path $modules -Force | Out-Null
$repos = @(
  @{ Repo='sgennadi/UniversalUpdater'; Folder='UniversalUpdater'; Commit='a909a7dd033799f5e0783fb8085478c3e3b52c42' },
  @{ Repo='sgennadi/UniversalDiagnocticsModule'; Folder='UniversalDiagnocticsModule'; Commit='16ce43a34fc99002f69bbf86673fb8e7c451c662' }
)
foreach ($r in $repos) {
  $destination = Join-Path $modules $r.Folder
  if (-not (Test-Path (Join-Path $destination '.git'))) {
    & gh repo clone $r.Repo $destination -- --no-checkout
    if ($LASTEXITCODE -ne 0) { throw "Cannot clone $($r.Repo). Configure gh auth login with read access." }
  }
  & git -C $destination fetch origin $r.Commit
  if ($LASTEXITCODE -ne 0) { throw "Cannot fetch pinned revision for $($r.Repo)." }
  & git -C $destination checkout --detach $r.Commit
  if ($LASTEXITCODE -ne 0) { throw "Cannot checkout pinned revision for $($r.Repo)." }
  Write-Host "Module ready: $($r.Repo) $($r.Commit)"
}
