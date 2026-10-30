<#
.SYNOPSIS
    Installs DevWheel for the current user and starts it with Windows. No admin rights needed.

.DESCRIPTION
    1. Copies DevWheel.exe (next to this script) to %LOCALAPPDATA%\Programs\DevWheel
    2. Turns on "Start with Windows": the HKCU Run value the app itself manages, plus the
       StartWithWindows setting so saving settings later keeps it on
    3. Adds a Start Menu shortcut
    4. Starts DevWheel

    Running it again upgrades in place; settings in %APPDATA%\DevWheel are kept.

.EXAMPLE
    ./install.ps1
.EXAMPLE
    ./install.ps1 -NoAutostart
#>
[CmdletBinding()]
param(
    [switch]$NoAutostart,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'DevWheel.exe'
if (-not (Test-Path $source)) { throw "DevWheel.exe not found next to this script ($PSScriptRoot)." }

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\DevWheel'
$exe = Join-Path $installDir 'DevWheel.exe'
$settingsPath = Join-Path $env:APPDATA 'DevWheel\settings.json'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'DevWheel.lnk'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# Upgrade: the running copy locks the .exe.
Get-Process -Name DevWheel -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item $source $exe -Force
Unblock-File $exe

# Same value name and format as StartupRegistration.cs, so the Settings checkbox stays in sync.
if ($NoAutostart) {
    Remove-ItemProperty -Path $runKey -Name DevWheel -ErrorAction SilentlyContinue
} else {
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -Path $runKey -Name DevWheel -Value "`"$exe`""
}

# The app rewrites the Run value from this setting on every save, so the two must agree.
$settings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
$settings | Add-Member -NotePropertyName StartWithWindows -NotePropertyValue (-not $NoAutostart) -Force
New-Item -ItemType Directory -Force (Split-Path $settingsPath) | Out-Null
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 10))

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $installDir
$link.Description = 'DevWheel developer tool wheel'
$link.Save()

if (-not $NoLaunch) { Start-Process $exe }

Write-Host "DevWheel installed to $installDir"
Write-Host $(if ($NoAutostart) { 'Start with Windows: off' } else { 'Start with Windows: on' })
Write-Host 'Press Ctrl+Alt+Space to open the wheel. Uninstall with uninstall.cmd.'
