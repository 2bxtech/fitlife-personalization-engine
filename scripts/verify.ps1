[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [switch]$SkipAudit
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $repoRoot "fitlife-web"
$npmExecutable = if ($IsWindows -or $env:OS -eq "Windows_NT") { "npm.cmd" } else { "npm" }

function Assert-LastExitCode {
    param([Parameter(Mandatory = $true)][string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Repository: $repoRoot"
Write-Host "Commit: $(git -C $repoRoot rev-parse --short HEAD)"
Write-Host ".NET SDK: $(dotnet --version)"
Write-Host "Node: $(node --version)"
Write-Host "npm: $(& $npmExecutable --version)"
Assert-LastExitCode "npm version check"

Push-Location $repoRoot
try {
    dotnet restore FitLife.sln
    Assert-LastExitCode "dotnet restore"

    dotnet build FitLife.sln --configuration Release --no-restore
    Assert-LastExitCode "dotnet build"

    dotnet test FitLife.sln --configuration Release --no-build --verbosity minimal
    Assert-LastExitCode "dotnet test"
}
finally {
    Pop-Location
}

Push-Location $webRoot
try {
    if (-not $SkipInstall) {
        & $npmExecutable ci --legacy-peer-deps
        Assert-LastExitCode "npm ci"
    }

    & $npmExecutable run lint
    Assert-LastExitCode "npm run lint"

    & $npmExecutable test
    Assert-LastExitCode "npm test"

    & $npmExecutable run build
    Assert-LastExitCode "npm run build"

    if (-not $SkipAudit) {
        & $npmExecutable audit --omit=dev --audit-level=high
        Assert-LastExitCode "npm audit"
    }
}
finally {
    Pop-Location
}

Write-Host "FitLife verification completed successfully."
