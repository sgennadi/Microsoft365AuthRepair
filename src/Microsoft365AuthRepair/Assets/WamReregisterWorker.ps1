# PowerShell fallback for Microsoft365AuthRepair. Re-register WAM only.
# Executes under the affected user's InteractiveToken via Task Scheduler.
# Requires Windows PowerShell Appx cmdlets. Do not bypass enterprise policy.
# No passwords, tokens, domain names or account details are sent anywhere.
#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-f0-9]{32}$')]
    [string] $RunId,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^S-1-5-21-(?:\d+-){3}\d+$')]
    [string] $ExpectedSid,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^WAM_S_1_5_21_[0-9_]+_[a-f0-9]{8}$')]
    [string] $TaskName
)
$ErrorActionPreference = 'Stop'
$rc = 2
$markerCreated = $false
$logs = $null
try {
    $actualSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $session = (Get-Process -Id $PID -ErrorAction Stop).SessionId
    if ($actualSid -cne $ExpectedSid -or $session -eq 0) {
        throw 'Worker user token/session does not match the assigned profile.'
    }

    $expectedTaskName = 'WAM_' + $ExpectedSid.Replace('-', '_') + '_' + $RunId.Substring(0, 8)
    if ($TaskName -cne $expectedTaskName) {
        throw 'Invalid scheduled-task identity.'
    }

    $base = Join-Path $env:LOCALAPPDATA 'Microsoft365AuthRepair'
    $logs = Join-Path $base 'Logs'
    $markers = Join-Path $base 'Completed'
    [void][IO.Directory]::CreateDirectory($logs)
    [void][IO.Directory]::CreateDirectory($markers)

    $attempted = Join-Path $markers ($RunId + '.attempted')
    $complete = Join-Path $markers ($RunId + '.ok')
    $alreadyAttempted = $false
    try {
        $once = [IO.File]::Open($attempted, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write, [IO.FileShare]::None)
        $once.Dispose()
        $markerCreated = $true
    } catch [System.IO.IOException] {
        # May be a second logon trigger: never repeat even a failed operation.
        $alreadyAttempted = $true
        $rc = if (Test-Path -LiteralPath $complete) { 0 } else { 2 }
    }

    if (-not $alreadyAttempted) {
        $manifest = Join-Path $env:SystemRoot 'SystemApps\Microsoft.AAD.BrokerPlugin_cw5n1h2txyewy\AppxManifest.xml'
        if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
            throw 'System AAD BrokerPlugin manifest is not present.'
        }

        $command = Get-Command Add-AppxPackage -ErrorAction Stop
        $arguments = @{
            Path = $manifest
            Register = $true
            DisableDevelopmentMode = $true
            ErrorAction = 'Stop'
        }
        if ($command.Parameters.ContainsKey('ForceTargetApplicationShutdown')) {
            $arguments.ForceTargetApplicationShutdown = $true
        } elseif ($command.Parameters.ContainsKey('ForceApplicationShutdown')) {
            $arguments.ForceApplicationShutdown = $true
        }
        Add-AppxPackage @arguments | Out-Null
        if (-not (Get-AppxPackage -Name 'Microsoft.AAD.BrokerPlugin' -ErrorAction Stop)) {
            throw 'AAD BrokerPlugin did not appear for this Windows user.'
        }

        [IO.File]::WriteAllText($complete, [DateTimeOffset]::UtcNow.ToString('O'))
        $rc = 0
    }
} catch {
    $rc = 2
} finally {
    if ($markerCreated -and $logs) {
        try {
            $line = if ($rc -eq 0) { 'WAM_REPAIR_OK' } else { 'WAM_REPAIR_FAILED' }
            $path = Join-Path $logs ('repair_ps_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '.log')
            [IO.File]::WriteAllText($path, $line)
        } catch { }
    }
    if ($markerCreated -and $rc -ne 0) {
        # Keep the result receipt independent of the optional repair log.
        try {
            [IO.File]::WriteAllText((Join-Path $markers ($RunId + '.failed')),
                [DateTimeOffset]::UtcNow.ToString('O'))
        } catch { }
    }
    # A non-elevated task may lack permission to remove an admin-created task.
    # The attempt marker prevents repeat execution; Task Scheduler also expires.
    try {
        $service = New-Object -ComObject Schedule.Service
        $service.Connect()
        $folder = $service.GetFolder('\Microsoft365AuthRepair')
        $folder.DeleteTask($TaskName, 0)
    } catch { }
}
exit $rc
