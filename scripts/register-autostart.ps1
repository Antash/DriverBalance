<#
.SYNOPSIS
  Registers (or removes) a per-user Scheduled Task that starts DriverBalance at logon.
.DESCRIPTION
  Runs %LOCALAPPDATA%\DriverBalance\app\DriverBalance.Web.exe with that folder as working directory.
  The exe is a console program, so the task starts it through "conhost.exe --headless" and no window appears at logon.
  No administrator rights are needed. Safe to run repeatedly.
.PARAMETER Unregister
  Removes the "DriverBalance" task instead.
#>
[CmdletBinding()]
param([switch]$Unregister)

$ErrorActionPreference = 'Stop'
$taskName = 'DriverBalance'

if ($Unregister) {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-Host "Removed scheduled task '$taskName'."
    } else {
        Write-Host "Scheduled task '$taskName' is not registered."
    }
    return
}

$appDir = Join-Path $env:LOCALAPPDATA 'DriverBalance\app'
$exe = Join-Path $appDir 'DriverBalance.Web.exe'
if (-not (Test-Path $exe)) {
    throw "Not found: $exe. Publish first: dotnet publish src/DriverBalance.Web -c Release -o `"$appDir`""
}

$user = "$env:USERDOMAIN\$env:USERNAME"
# conhost --headless runs the console exe without creating a visible console window.
$action = New-ScheduledTaskAction -Execute 'conhost.exe' -Argument "--headless `"$exe`"" -WorkingDirectory $appDir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal `
    -Settings $settings -Description 'DriverBalance web app (http://localhost:5180)' -Force | Out-Null
Write-Host "Registered scheduled task '$taskName' (runs at logon): $exe"
