param()
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet run --project src/Veda.App -c Debug -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "The app exited with an error." }
} finally { Pop-Location }
