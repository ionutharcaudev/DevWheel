<#
.SYNOPSIS
    Tests, publishes and packages DevWheel as a single self-contained win-x64 .exe.

.DESCRIPTION
    1. dotnet test (includes the SEC-01 network-reference and source-rule checks)
    2. dotnet publish: single file, self-contained, win-x64
    3. Optional code signing with signtool (SEC-09; required before sharing with the team)
    4. Zip (the .exe plus the install kit from scripts/install) plus SHA-256 checksums of the .exe and the zip, so colleagues can verify what they received

.EXAMPLE
    ./scripts/publish.ps1
.EXAMPLE
    ./scripts/publish.ps1 -CertThumbprint ABC123... -TimestampUrl http://timestamp.example.internal
#>
[CmdletBinding()]
param(
    [string]$Dotnet = $(if (Get-Command dotnet -ErrorAction SilentlyContinue) { 'dotnet' } else { Join-Path $env:USERPROFILE '.dotnet\dotnet.exe' }),
    [string]$CertThumbprint,
    [string]$TimestampUrl,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\publish'
$dist = Join-Path $root 'artifacts\dist'

Push-Location $root
try {
    if (-not $SkipTests) {
        & $Dotnet test DevWheel.sln -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not publishing.' }
    }

    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    & $Dotnet publish src/DevWheel.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -o $out
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    $exe = Join-Path $out 'DevWheel.exe'

    if ($CertThumbprint) {
        if (-not $TimestampUrl) { throw 'Signing needs -TimestampUrl (the company timestamp server).' }
        & signtool sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $exe
        if ($LASTEXITCODE -ne 0) { throw 'Signing failed.' }
        & signtool verify /pa $exe
        if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
    } else {
        Write-Warning 'Not signed. Fine for personal testing; sign before sharing with the team (SEC-09).'
    }

    New-Item -ItemType Directory -Force $dist | Out-Null
    $version = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
    $zip = Join-Path $dist "DevWheel-$version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    # Install kit next to the .exe: install.cmd / uninstall.cmd (per user, autostart, Start Menu shortcut).
    $kit = Get-ChildItem (Join-Path $PSScriptRoot 'install') -File | Select-Object -ExpandProperty FullName
    Compress-Archive -Path (@($exe) + $kit) -DestinationPath $zip

    $lines = foreach ($file in @($exe, $zip)) {
        '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 $file).Hash.ToLowerInvariant(), (Split-Path -Leaf $file)
    }
    $sums = Join-Path $dist 'SHA256SUMS.txt'
    $lines | Set-Content -Encoding utf8 $sums

    Write-Host ''
    Write-Host "Executable: $exe"
    Write-Host "Package:    $zip"
    Write-Host "Checksums:  $sums"
    $lines | ForEach-Object { Write-Host "  $_" }
}
finally {
    Pop-Location
}
