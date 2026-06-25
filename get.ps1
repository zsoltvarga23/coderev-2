<#
.SYNOPSIS
  Download-and-install the coderev CLI from GitHub Releases — no Go, no .NET, no
  build tools. Per-user, no admin. The opposite of install.ps1 (which builds
  from source); this just fetches a pre-built, checksum-verified binary.

.DESCRIPTION
  Resolves the latest release, downloads the Windows CLI asset and checksums.txt,
  verifies the SHA-256, installs to %LOCALAPPDATA%\coderev\bin\coderev.exe, and
  puts that folder on the user PATH. After this, `coderev update` keeps it current.

  For the desktop GUI, download and run the Velopack installer
  (CodeRev-win-Setup.exe) from the same Releases page instead — it self-updates.

.PARAMETER Version
  Specific tag to install (e.g. v1.2.0). Default: latest.

.PARAMETER Repo
  GitHub owner/repo. Default: zsoltvarga23/coderev-2.

.PARAMETER NoPath
  Do not modify the user PATH.

.EXAMPLE
  irm https://raw.githubusercontent.com/zsoltvarga23/coderev-2/main/get.ps1 | iex
  ./get.ps1 -Version v1.2.0
#>
[CmdletBinding()]
param(
    [string]$Version = 'latest',
    [string]$Repo = 'zsoltvarga23/coderev-2',
    [switch]$NoPath
)

$ErrorActionPreference = 'Stop'
$asset = 'coderev-windows-amd64.exe'
$BinDir = Join-Path $env:LOCALAPPDATA 'coderev\bin'
$Target = Join-Path $BinDir 'coderev.exe'

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

# Resolve the release (latest or a specific tag).
$api = if ($Version -eq 'latest') {
    "https://api.github.com/repos/$Repo/releases/latest"
} else {
    "https://api.github.com/repos/$Repo/releases/tags/$Version"
}
Write-Step "Resolving release ($Version)"
$headers = @{ 'Accept' = 'application/vnd.github+json'; 'User-Agent' = 'coderev-get' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN" }
$release = Invoke-RestMethod -Uri $api -Headers $headers
$tag = $release.tag_name
Write-Host "    $tag"

$binUrl = ($release.assets | Where-Object { $_.name -eq $asset }).browser_download_url
$sumUrl = ($release.assets | Where-Object { $_.name -eq 'checksums.txt' }).browser_download_url
if (-not $binUrl) { throw "Release $tag has no asset '$asset'." }
if (-not $sumUrl) { throw "Release $tag has no checksums.txt (refusing to install unverified)." }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("coderev-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$binTmp = Join-Path $tmp $asset
$sumTmp = Join-Path $tmp 'checksums.txt'

Write-Step "Downloading $asset"
Invoke-WebRequest -Uri $binUrl -OutFile $binTmp -Headers @{ 'User-Agent' = 'coderev-get' }
Invoke-WebRequest -Uri $sumUrl -OutFile $sumTmp -Headers @{ 'User-Agent' = 'coderev-get' }

# Verify SHA-256 against the published checksum.
Write-Step "Verifying checksum"
$want = (Get-Content $sumTmp | ForEach-Object {
        $f = $_ -split '\s+'
        if ($f.Count -ge 2 -and ($f[-1] -replace '^\*', '') -eq $asset) { $f[0] }
    } | Select-Object -First 1)
if (-not $want) { throw "checksums.txt has no entry for $asset." }
$got = (Get-FileHash -Algorithm SHA256 $binTmp).Hash
if ($got -ne $want.ToUpper()) { throw "Checksum mismatch: expected $want, got $got." }
Write-Host "    OK" -ForegroundColor Green

Write-Step "Installing"
New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
Copy-Item $binTmp $Target -Force
Remove-Item $tmp -Recurse -Force
Write-Host "    installed: $Target" -ForegroundColor Green

if (-not $NoPath) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($cur -split ';') -notcontains $BinDir) {
        $new = if ([string]::IsNullOrEmpty($cur)) { $BinDir } else { "$cur;$BinDir" }
        [Environment]::SetEnvironmentVariable('Path', $new, 'User')
        Write-Host "    added to user PATH (open a new terminal): $BinDir" -ForegroundColor Green
    }
}

Write-Host ""
Write-Step "Done"
Write-Host "  Open a NEW terminal, then:  coderev <branch>   (or: coderev --version)"
Write-Host "  Update later with:          coderev update"
