param([ValidateSet("Debug", "Release")][string]$Configuration = "Debug")
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet build src/Veda.App/Veda.App.csproj -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "Windows build failed." }
} finally { Pop-Location }
