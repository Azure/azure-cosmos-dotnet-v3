<#
.SYNOPSIS
    Updates the test matrix with new versions
.PARAMETER Version
    Version to add to the matrix
.PARAMETER SetBaseline
    Set this version as the new baseline
.PARAMETER Remove
    Remove this version from the matrix
.EXAMPLE
    .\update-test-matrix.ps1 -Version "1.0.0-preview08"
.EXAMPLE
    .\update-test-matrix.ps1 -Version "1.0.0-preview08" -SetBaseline
.EXAMPLE
    .\update-test-matrix.ps1 -Version "1.0.0-preview04" -Remove
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [switch]$SetBaseline,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "🔧 Updating Test Matrix" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Action: $(if ($Remove) { 'Remove' } elseif ($SetBaseline) { 'Add & Set as Baseline' } else { 'Add' })" -ForegroundColor Yellow
Write-Host ""

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9\.]+)?$') {
    Write-Host "❌ Invalid version format: $Version" -ForegroundColor Red
    Write-Host ""
    Write-Host "Expected format: X.Y.Z or X.Y.Z-prerelease" -ForegroundColor Gray
    Write-Host "Examples: 1.0.0, 1.0.0-preview08" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Validate version exists on NuGet (unless removing)
if (-not $Remove) {
    Write-Host "Validating version exists on NuGet.org..." -ForegroundColor Gray
    $packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"
    $apiUrl = "https://api.nuget.org/v3-flatcontainer/$($packageId.ToLower())/index.json"

    try {
        $response = Invoke-RestMethod -Uri $apiUrl -Method Get -ErrorAction Stop
        $versions = $response.versions
        
        if ($versions -notcontains $Version) {
            Write-Host "❌ Version $Version not found on NuGet.org" -ForegroundColor Red
            Write-Host ""
            Write-Host "Available versions:" -ForegroundColor Gray
            $versions | Select-Object -Last 5 | ForEach-Object { Write-Host "  •  $_" -ForegroundColor Gray }
            Write-Host ""
            Write-Host "Use .\tools\discover-published-versions.ps1 to see all versions" -ForegroundColor Yellow
            Write-Host ""
            exit 1
        }
        
        Write-Host "✅ Version exists on NuGet.org" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host "⚠️  Failed to validate version on NuGet.org: $_" -ForegroundColor Yellow
        Write-Host "Continuing anyway..." -ForegroundColor Gray
        Write-Host ""
    }
}

# Update testconfig.json
$testConfigPath = "Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json"

if (-not (Test-Path $testConfigPath)) {
    Write-Host "❌ Test config not found: $testConfigPath" -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host "Updating $testConfigPath..." -ForegroundColor Gray

try {
    $config = Get-Content $testConfigPath -Raw | ConvertFrom-Json
    
    if ($Remove) {
        # Remove version
        if ($config.versionMatrix -contains $Version) {
            $config.versionMatrix = $config.versionMatrix | Where-Object { $_ -ne $Version }
            Write-Host "✅ Removed $Version from test matrix" -ForegroundColor Green
            
            # Check if removed version was baseline
            if ($config.baselineVersion -eq $Version) {
                Write-Host "⚠️  Removed version was the baseline!" -ForegroundColor Yellow
                Write-Host "   Setting new baseline to: $($config.versionMatrix[0])" -ForegroundColor Yellow
                $config.baselineVersion = $config.versionMatrix[0]
            }
        } else {
            Write-Host "ℹ️  Version $Version not in matrix (nothing to remove)" -ForegroundColor Cyan
        }
    } else {
        # Add version if not already present
        if ($config.versionMatrix -notcontains $Version) {
            # Add at the beginning (latest versions first)
            $newVersions = @($Version) + $config.versionMatrix
            $config.versionMatrix = $newVersions
            Write-Host "✅ Added $Version to test matrix" -ForegroundColor Green
        } else {
            Write-Host "ℹ️  Version $Version already in matrix" -ForegroundColor Cyan
        }
        
        # Update baseline if requested
        if ($SetBaseline) {
            $oldBaseline = $config.baselineVersion
            $config.baselineVersion = $Version
            Write-Host "✅ Updated baseline: $oldBaseline → $Version" -ForegroundColor Green
        }
    }
    
    # Save updated config
    $jsonContent = $config | ConvertTo-Json -Depth 10
    $jsonContent | Set-Content $testConfigPath -Encoding UTF8
    
    Write-Host ""
    Write-Host "─────────────────────────────────────" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Current test matrix:" -ForegroundColor Cyan
    $config.versionMatrix | ForEach-Object {
        $isBaseline = $_ -eq $config.baselineVersion
        if ($isBaseline) {
            Write-Host "  📌 $_ (baseline)" -ForegroundColor Yellow
        } else {
            Write-Host "  •  $_" -ForegroundColor Gray
        }
    }
    Write-Host ""
    Write-Host "Baseline version: $($config.baselineVersion)" -ForegroundColor Yellow
    Write-Host ""
    
} catch {
    Write-Host "❌ Failed to update testconfig.json: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}

# Remind about pipeline update
if (-not $Remove) {
    Write-Host "─────────────────────────────────────" -ForegroundColor Gray
    Write-Host ""
    Write-Host "⚠️  Don't forget to update the pipeline YAML!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   File: azure-pipelines-encryption-custom-compatibility.yml" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Actions needed:" -ForegroundColor Yellow
    if ($SetBaseline) {
        Write-Host "   1. Update 'BaselineVersion' variable to: $Version" -ForegroundColor Gray
        Write-Host "   2. Update Stage 1 (QuickCompatibilityCheck) TestVersion to: $Version" -ForegroundColor Gray
    }
    Write-Host "   3. Add new job in Stage 2 (FullMatrixCompatibility):" -ForegroundColor Gray
    Write-Host ""
    Write-Host "      - job: Test$(($Version -replace '\.', '') -replace '-', '')" -ForegroundColor Gray
    Write-Host "        displayName: 'Test vs $Version'" -ForegroundColor Gray
    Write-Host "        pool:" -ForegroundColor Gray
    Write-Host "          vmImage: 'windows-latest'" -ForegroundColor Gray
    Write-Host "        steps:" -ForegroundColor Gray
    Write-Host "          - template: templates/encryption-custom-compatibility-test-steps.yml" -ForegroundColor Gray
    Write-Host "            parameters:" -ForegroundColor Gray
    Write-Host "              BuildConfiguration: `$(BuildConfiguration)" -ForegroundColor Gray
    Write-Host "              TestVersion: '$Version'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   See MAINTENANCE.md for detailed instructions" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "─────────────────────────────────────" -ForegroundColor Gray
    Write-Host ""
    Write-Host "⚠️  Don't forget to remove the corresponding job from pipeline YAML!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   File: azure-pipelines-encryption-custom-compatibility.yml" -ForegroundColor Gray
    Write-Host "   Remove job: Test$(($Version -replace '\.', '') -replace '-', '')" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Update pipeline YAML (see above)" -ForegroundColor Gray
Write-Host "  2. Test locally: .\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\test-compatibility.ps1" -ForegroundColor Gray
Write-Host "  3. Commit changes: git add testconfig.json azure-pipelines-*.yml" -ForegroundColor Gray
Write-Host "  4. Push and verify pipeline runs" -ForegroundColor Gray
Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
Write-Host ""
