<#
.SYNOPSIS
  Per-user (no admin, no certificate) installer for coderev — the CLI engine,
  the desktop GUI, or both. Builds from source, installs into a per-user
  folder, puts the CLI on PATH, and creates a Start Menu shortcut for the GUI.

.DESCRIPTION
  Intended for small, internal distribution. Nothing is code-signed, so on the
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
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$VersionFile = Join-Path $RepoRoot 'VERSION'
$Version = if (Test-Path $VersionFile) { (Get-Content $VersionFile -Raw).Trim() } else { 'dev' }
$BinDir = Join-Path $Prefix 'bin'
$GuiDir = Join-Path $Prefix 'gui'
$ShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'coderev.lnk'

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Require-Tool($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "'$name' was not found on PATH. $hint"
    }
}

function Add-UserPath($dir) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($cur -split ';') -notcontains $dir) {
        $new = if ([string]::IsNullOrEmpty($cur)) { $dir } else { "$cur;$dir" }
        [Environment]::SetEnvironmentVariable('Path', $new, 'User')
        Write-Host "    added to user PATH: $dir  (open a new terminal)" -ForegroundColor Green
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
    Write-Step "Uninstalling"
    if (Test-Path $Prefix) { Remove-Item $Prefix -Recurse -Force; Write-Host "    removed: $Prefix" }
    if (Test-Path $ShortcutPath) { Remove-Item $ShortcutPath -Force; Write-Host "    shortcut removed" }
    Remove-UserPath $BinDir
    Write-Host "Done. (The PATH change takes effect in a new terminal.)" -ForegroundColor Green
    return
}

$doCli = $Component -in @('cli', 'all')
$doGui = $Component -in @('gui', 'all')

# ---- CLI ----------------------------------------------------------------
if ($doCli) {
    Write-Step "Building the CLI (Go)"
    Require-Tool 'go' 'Install Go: https://go.dev/dl/'
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
    Push-Location $RepoRoot
    try { & go build -ldflags "-X main.version=$Version" -o (Join-Path $BinDir 'coderev.exe') ./cmd/coderev }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "go build failed." }
    Write-Host "    installed: $BinDir\coderev.exe" -ForegroundColor Green
    if (-not $NoPath) { Add-UserPath $BinDir }
}

# ---- GUI ----------------------------------------------------------------
if ($doGui) {
    Write-Step "Publishing the GUI (.NET, self-contained, $Rid)"
    Require-Tool 'dotnet' 'Install the .NET SDK: https://dotnet.microsoft.com/download'
    Require-Tool 'go' 'Go is also needed for the GUI (the engine binary is bundled).'

    $app = Join-Path $RepoRoot 'coderev-desktop/src/CodeRev.App/CodeRev.App.csproj'
    & dotnet publish $app -c Release -r $Rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $GuiDir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    # Bundle the engine next to the GUI so it is found without PATH setup.
    Write-Step "Bundling the engine with the GUI"
    Push-Location $RepoRoot
    try { & go build -ldflags "-X main.version=$Version" -o (Join-Path $GuiDir 'coderev.exe') ./cmd/coderev }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "go build (for the GUI) failed." }
    Write-Host "    installed: $GuiDir\CodeRev.App.exe (+ bundled coderev.exe)" -ForegroundColor Green

    if (-not $NoShortcut) {
        $exe = Join-Path $GuiDir 'CodeRev.App.exe'
        $sh = New-Object -ComObject WScript.Shell
        $lnk = $sh.CreateShortcut($ShortcutPath)
        $lnk.TargetPath = $exe
        $lnk.WorkingDirectory = $GuiDir
        $lnk.IconLocation = "$exe,0"
        $lnk.Description = 'coderev - AI-powered PR review'
        $lnk.Save()
        Write-Host "    Start Menu shortcut: $ShortcutPath" -ForegroundColor Green
    }
}

Write-Host ""
Write-Step "Done"
if ($doCli) { Write-Host "  CLI:  open a NEW terminal, then:  coderev <branch>   (or: coderev --version)" }
if ($doGui) { Write-Host "  GUI:  Start Menu -> 'coderev', or run: $GuiDir\CodeRev.App.exe" }
Write-Host "  Uninstall:  ./install.ps1 -Uninstall"
