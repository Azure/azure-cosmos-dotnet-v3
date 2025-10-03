<#
.SYNOPSIS
    Quick local API compatibility test
.PARAMETER Baseline
    Baseline version to compare against (default: 1.0.0-preview07)
.EXAMPLE
    .\test-api-compat-local.ps1
.EXAMPLE
    .\test-api-compat-local.ps1 -Baseline "1.0.0-preview06"
#>

param(
    [string]$Baseline = "1.0.0-preview07"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "🧪 Local API Compatibility Test" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "Step 1: Building current version..." -ForegroundColor Yellow
Write-Host ""

$projectPath = "Microsoft.Azure.Cosmos.Encryption.Custom\src\Microsoft.Azure.Cosmos.Encryption.Custom.csproj"

dotnet build $projectPath -c Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Build failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run 'dotnet restore' first or check for compilation errors." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "✅ Build succeeded" -ForegroundColor Green
Write-Host ""

# Run API compat
Write-Host "Step 2: Running API compatibility check..." -ForegroundColor Yellow
Write-Host ""

$scriptPath = Join-Path $PSScriptRoot "apicompat-check.ps1"

& $scriptPath -BaselineVersion $Baseline

$exitCode = $LASTEXITCODE

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "✅ API compatibility check passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your changes are backward compatible with version $Baseline" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "❌ API compatibility issues found" -ForegroundColor Red
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the changes reported above" -ForegroundColor Gray
    Write-Host "  2. Determine if the changes are intentional" -ForegroundColor Gray
    Write-Host ""
    Write-Host "If changes are intentional:" -ForegroundColor Yellow
    Write-Host "  a. Document in docs/compatibility-testing/API-CHANGES.md" -ForegroundColor Gray
    Write-Host "  b. Add suppressions to Microsoft.Azure.Cosmos.Encryption.Custom\ApiCompatSuppressions.txt" -ForegroundColor Gray
    Write-Host "  c. Update the baseline version if this is a major/minor release" -ForegroundColor Gray
    Write-Host ""
    Write-Host "If changes are unintentional:" -ForegroundColor Yellow
    Write-Host "  a. Revert the breaking changes" -ForegroundColor Gray
    Write-Host "  b. Find alternative implementations that maintain compatibility" -ForegroundColor Gray
    Write-Host ""
}

exit $exitCode
