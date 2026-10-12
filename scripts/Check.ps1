param()
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet run --project tests/Veda.Checks -c Release -- assets/soundtracks/demo-void
    if ($LASTEXITCODE -ne 0) { throw "Verification failed." }
} finally { Pop-Location }
