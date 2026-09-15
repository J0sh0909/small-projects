<#
.SYNOPSIS
    Builds the two distributable flavours of DesktopStats into .\dist.

.DESCRIPTION
    Maintainer script: end users never run this; they download a release asset and
    run "DesktopStats.exe --install". Compiling needs the .NET SDK, which the exe
    itself obviously cannot do, so this is the one thing left in PowerShell.

    Produces, under artifacts\dist:
      DesktopStats.exe            Self-contained single file. No .NET runtime needed.
      DesktopStats-portable\      Framework-dependent binaries. Needs the .NET 8 Desktop Runtime.
      DesktopStats-portable.zip   The above, zipped for release upload.

    Everything is written relative to this script, so it works from any checkout
    location and under any user account.

.PARAMETER Runtime
    Target RID. Defaults to win-x64. Use win-arm64 for ARM devices.

.PARAMETER SkipZip
    Skip producing the portable .zip.

.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -Runtime win-arm64
#>
# PositionalBinding is off so a mistyped flag (--SkipZip instead of -SkipZip) reports
# itself rather than being silently bound to -Runtime and failing with a ValidateSet
# error about a parameter the caller never mentioned.
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string]$Runtime = 'win-x64',

    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'

$root      = $PSScriptRoot
$project   = Join-Path $root 'DesktopStats.csproj'
$artifacts = Join-Path $root 'artifacts'
$dist      = Join-Path $artifacts 'dist'
$portable  = Join-Path $dist 'DesktopStats-portable'

if (-not (Test-Path $project)) {
    throw "DesktopStats.csproj not found next to build.ps1 (looked in '$root')."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK ("dotnet") is not on PATH. Install the .NET 8 SDK or newer from https://dotnet.microsoft.com/download'
}

Write-Host "Building DesktopStats for $Runtime" -ForegroundColor Cyan

# Start from a clean slate so stale output never ships in a release, and so the
# artifacts folder doesn't accumulate configurations and RIDs you no longer build.
# Locked files mean a previous overlay is still running; --install stops it for you.
# bin\ and obj\ are only here to sweep up output from before Directory.Build.props.
foreach ($stale in $artifacts, (Join-Path $root 'bin'), (Join-Path $root 'obj'), (Join-Path $root 'dist')) {
    if (-not (Test-Path $stale)) { continue }
    try {
        Remove-Item $stale -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Warning "Could not fully remove '$stale' - a running DesktopStats.exe may be locking it."
    }
}
New-Item -ItemType Directory -Path $dist -Force | Out-Null

# ---------------------------------------------------------------- single exe
Write-Host "`n[1/2] Self-contained single-file exe..." -ForegroundColor Cyan
$selfContained = Join-Path $dist '_sc'
dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $selfContained `
    -v minimal
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed (exit $LASTEXITCODE)." }

Move-Item (Join-Path $selfContained 'DesktopStats.exe') (Join-Path $dist 'DesktopStats.exe') -Force
Remove-Item $selfContained -Recurse -Force

# ----------------------------------------------------------------- portable
Write-Host "`n[2/2] Framework-dependent portable build..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained false `
    -p:DebugType=none `
    -o $portable `
    -v minimal
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed (exit $LASTEXITCODE)." }

if (-not $SkipZip) {
    $zip = Join-Path $dist 'DesktopStats-portable.zip'
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
}

# ------------------------------------------------------------------ summary
$exe = Join-Path $dist 'DesktopStats.exe'
Write-Host "`nDone." -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object {
    if ($_.PSIsContainer) {
        $size = (Get-ChildItem $_.FullName -Recurse -File | Measure-Object Length -Sum).Sum
    } else {
        $size = $_.Length
    }
    '  {0,-30} {1,8:N1} MB' -f $_.Name, ($size / 1MB)
}
Write-Host "`nSingle exe: $exe" -ForegroundColor Green
Write-Host "Install at logon with:  $exe --install" -ForegroundColor Green
