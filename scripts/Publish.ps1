param()
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet publish src/Veda.App/Veda.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/windows-x64
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    Write-Host "Local application folder: artifacts/windows-x64. Test Veda.exe with all accompanying files."
} finally { Pop-Location }
