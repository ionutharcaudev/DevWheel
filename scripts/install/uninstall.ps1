<#
.SYNOPSIS
    Removes DevWheel for the current user: autostart entry, Start Menu shortcut and program files.

.DESCRIPTION
    Settings in %APPDATA%\DevWheel are kept unless -RemoveSettings is given.

.EXAMPLE
    ./uninstall.ps1
.EXAMPLE
    ./uninstall.ps1 -RemoveSettings
#>
[CmdletBinding()]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\DevWheel'
$settingsDir = Join-Path $env:APPDATA 'DevWheel'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'DevWheel.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Get-Process -Name DevWheel -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-ItemProperty -Path $runKey -Name DevWheel -ErrorAction SilentlyContinue
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }

if ($RemoveSettings) {
    if (Test-Path $settingsDir) { Remove-Item $settingsDir -Recurse -Force }
} elseif (Test-Path (Join-Path $settingsDir 'settings.json')) {
    # Leave settings, but make sure a later manual run does not re-add the Run value on save.
    $path = Join-Path $settingsDir 'settings.json'
    $settings = Get-Content $path -Raw | ConvertFrom-Json
    $settings | Add-Member -NotePropertyName StartWithWindows -NotePropertyValue $false -Force
    [IO.File]::WriteAllText($path, ($settings | ConvertTo-Json -Depth 10))
}

Write-Host 'DevWheel removed.'
if (-not $RemoveSettings) { Write-Host "Settings kept in $settingsDir (use -RemoveSettings to delete them)." }
