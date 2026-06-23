<#
.SYNOPSIS
  Per-user (no admin, no certificate) installer for coderev — the CLI engine,
  the desktop GUI, or both. Builds from source, installs into a per-user
  folder, puts the CLI on PATH, and creates a Start Menu shortcut for the GUI.

.DESCRIPTION
  Designed for small, internal distribution. Nothing is code-signed, so on the
  first GUI launch Windows SmartScreen may warn ("More info" -> "Run anyway").

.PARAMETER Component
  cli | gui | all  (default: all)

.PARAMETER Prefix
  Install location (default: %LOCALAPPDATA%\coderev)

.PARAMETER Rid
  .NET runtime identifier for the GUI publish (default: win-x64)

.PARAMETER NoPath       Do not modify the user PATH.
.PARAMETER NoShortcut   Do not create a Start Menu shortcut.
.PARAMETER Uninstall    Remove a previous installation (folder, PATH, shortcut).

.EXAMPLE
  ./install.ps1                 # install both
  ./install.ps1 -Component cli  # just the command-line tool
  ./install.ps1 -Component gui  # just the desktop app
  ./install.ps1 -Uninstall      # remove everything
#>
[CmdletBinding()]
param(
    [ValidateSet('cli', 'gui', 'all')] [string]$Component = 'all',
    [string]$Prefix = "$env:LOCALAPPDATA\coderev",
    [string]$Rid = 'win-x64',
    [switch]$NoPath,
    [switch]$NoShortcut,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$BinDir = Join-Path $Prefix 'bin'
$GuiDir = Join-Path $Prefix 'gui'
$ShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'coderev.lnk'

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Require-Tool($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "'$name' nem található a PATH-on. $hint"
    }
}

function Add-UserPath($dir) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($cur -split ';') -notcontains $dir) {
        $new = if ([string]::IsNullOrEmpty($cur)) { $dir } else { "$cur;$dir" }
        [Environment]::SetEnvironmentVariable('Path', $new, 'User')
        Write-Host "    PATH-hoz adva (User): $dir  (új terminál kell hozzá)" -ForegroundColor Green
    }
}
function Remove-UserPath($dir) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($cur) {
        $new = ($cur -split ';' | Where-Object { $_ -ne $dir }) -join ';'
        [Environment]::SetEnvironmentVariable('Path', $new, 'User')
    }
}

# ---- Uninstall ----------------------------------------------------------
if ($Uninstall) {
    Write-Step "Eltávolítás"
    if (Test-Path $Prefix) { Remove-Item $Prefix -Recurse -Force; Write-Host "    törölve: $Prefix" }
    if (Test-Path $ShortcutPath) { Remove-Item $ShortcutPath -Force; Write-Host "    parancsikon törölve" }
    Remove-UserPath $BinDir
    Write-Host "Kész. (A PATH-változás új terminálban érvényesül.)" -ForegroundColor Green
    return
}

$doCli = $Component -in @('cli', 'all')
$doGui = $Component -in @('gui', 'all')

# ---- CLI ----------------------------------------------------------------
if ($doCli) {
    Write-Step "CLI fordítása (Go)"
    Require-Tool 'go' 'Telepítsd a Go-t: https://go.dev/dl/'
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
    Push-Location $RepoRoot
    try { & go build -o (Join-Path $BinDir 'coderev.exe') ./cmd/coderev }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "go build sikertelen." }
    Write-Host "    telepítve: $BinDir\coderev.exe" -ForegroundColor Green
    if (-not $NoPath) { Add-UserPath $BinDir }
}

# ---- GUI ----------------------------------------------------------------
if ($doGui) {
    Write-Step "GUI publikálása (.NET, self-contained, $Rid)"
    Require-Tool 'dotnet' 'Telepítsd a .NET SDK-t: https://dotnet.microsoft.com/download'
    Require-Tool 'go' 'A GUI-hoz is kell a Go (a motor binárist mellécsomagoljuk).'

    $app = Join-Path $RepoRoot 'coderev-desktop/src/CodeRev.App/CodeRev.App.csproj'
    & dotnet publish $app -c Release -r $Rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $GuiDir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish sikertelen." }

    # Bundle the engine next to the GUI so it is found without PATH setup.
    Write-Step "Motor mellécsomagolása a GUI-hoz"
    Push-Location $RepoRoot
    try { & go build -o (Join-Path $GuiDir 'coderev.exe') ./cmd/coderev }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "go build (GUI-hoz) sikertelen." }
    Write-Host "    telepítve: $GuiDir\CodeRev.App.exe (+ mellékelt coderev.exe)" -ForegroundColor Green

    if (-not $NoShortcut) {
        $exe = Join-Path $GuiDir 'CodeRev.App.exe'
        $sh = New-Object -ComObject WScript.Shell
        $lnk = $sh.CreateShortcut($ShortcutPath)
        $lnk.TargetPath = $exe
        $lnk.WorkingDirectory = $GuiDir
        $lnk.IconLocation = "$exe,0"
        $lnk.Description = 'coderev — AI-alapú PR review'
        $lnk.Save()
        Write-Host "    Start menü parancsikon: $ShortcutPath" -ForegroundColor Green
    }
}

Write-Host ""
Write-Step "Kész"
if ($doCli) { Write-Host "  CLI:  nyiss ÚJ terminált, majd:  coderev <branch>   (vagy: coderev --version)" }
if ($doGui) { Write-Host "  GUI:  Start menü -> 'coderev', vagy futtasd: $GuiDir\CodeRev.App.exe" }
Write-Host "  Eltávolítás:  ./install.ps1 -Uninstall"
