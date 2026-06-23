# Self-contained, single-file publish of the desktop GUI for one or more
# runtimes. The matching coderev engine binary is bundled next to the app so the
# GUI finds it without PATH setup (BinaryLocator: CODEREV_BIN -> bundled -> PATH).
#
# Usage:
#   ./publish.ps1                       # current OS (win-x64)
#   ./publish.ps1 -Rid linux-x64        # Linux build (from any OS)
#   ./publish.ps1 -Rid win-x64,linux-x64

param(
    [string[]]$Rid = @("win-x64"),
    [string]$EngineDir = "..",          # where the coderev(.exe) engine lives
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$app = "src/CodeRev.App/CodeRev.App.csproj"

foreach ($r in $Rid) {
    Write-Host "==> Publishing $r" -ForegroundColor Cyan
    $out = "publish/$r"
    dotnet publish $app -c $Configuration -r $r --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $out

    # Bundle the engine binary for this runtime, if present.
    $engineName = if ($r -like "win-*") { "coderev.exe" } else { "coderev" }
    $engineSrc = Join-Path $EngineDir $engineName
    if (Test-Path $engineSrc) {
        Copy-Item $engineSrc (Join-Path $out $engineName) -Force
        Write-Host "    bundled engine: $engineName" -ForegroundColor Green
    } else {
        Write-Host "    NOTE: engine '$engineSrc' not found; build it first (go build -o $engineName ./cmd/coderev) or rely on PATH/CODEREV_BIN." -ForegroundColor Yellow
    }
    Write-Host "    output: $out"
}
