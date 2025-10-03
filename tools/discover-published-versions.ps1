<#
.SYNOPSIS
    Discovers all published versions of Microsoft.Azure.Cosmos.Encryption.Custom on NuGet.org
.PARAMETER Top
    Number of latest versions to show (default: 10)
.EXAMPLE
    .\discover-published-versions.ps1
.EXAMPLE
    .\discover-published-versions.ps1 -Top 20
#>

param(
    [int]$Top = 10
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "🔍 Discovering Published Versions" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "Package: Microsoft.Azure.Cosmos.Encryption.Custom" -ForegroundColor Yellow
Write-Host ""

# Query NuGet API
$packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"
$apiUrl = "https://api.nuget.org/v3-flatcontainer/$($packageId.ToLower())/index.json"

Write-Host "Querying NuGet.org API..." -ForegroundColor Gray

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Get -ErrorAction Stop
    $versions = $response.versions | Sort-Object { [version]($_ -replace '-.*$', '') } -Descending
    
    $latestVersions = $versions | Select-Object -First $Top
    
    Write-Host "✅ Query successful" -ForegroundColor Green
    Write-Host ""
    Write-Host "Latest $Top published versions:" -ForegroundColor Cyan
    Write-Host ""
    
    # Check current baseline from testconfig.json
    $testConfigPath = "Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json"
    $baseline = "1.0.0-preview07"  # Default
    
    if (Test-Path $testConfigPath) {
        try {
            $config = Get-Content $testConfigPath -Raw | ConvertFrom-Json
            $baseline = $config.baselineVersion
        } catch {
            Write-Host "⚠️  Could not read baseline from testconfig.json" -ForegroundColor Yellow
        }
    }
    
    $latestVersions | ForEach-Object {
        $version = $_
        $isBaseline = $version -eq $baseline
        $isLatest = $version -eq $versions[0]
        
        if ($isBaseline -and $isLatest) {
            Write-Host "  📌 $version (current baseline & latest)" -ForegroundColor Green
        }
        elseif ($isBaseline) {
            Write-Host "  📌 $version (current baseline)" -ForegroundColor Yellow
        }
        elseif ($isLatest) {
            Write-Host "  ⭐ $version (latest)" -ForegroundColor Green
        }
        else {
            Write-Host "  •  $version" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "Total versions published: $($versions.Count)" -ForegroundColor Gray
    Write-Host "Package URL: https://www.nuget.org/packages/$packageId" -ForegroundColor Gray
    Write-Host ""
    
    # Check against testconfig.json
    if (Test-Path $testConfigPath) {
        Write-Host "─────────────────────────────────────" -ForegroundColor Gray
        Write-Host ""
        
        try {
            $config = Get-Content $testConfigPath -Raw | ConvertFrom-Json
            $testedVersions = $config.versionMatrix
            
            Write-Host "Versions in test matrix:" -ForegroundColor Cyan
            $testedVersions | ForEach-Object {
                $isBaseline = $_ -eq $baseline
                if ($isBaseline) {
                    Write-Host "  📌 $_" -ForegroundColor Yellow
                } else {
                    Write-Host "  •  $_" -ForegroundColor Gray
                }
            }
            Write-Host ""
            
            # Find versions not in test matrix (from top 10)
            $notTested = $latestVersions | Where-Object { $testedVersions -notcontains $_ }
            
            if ($notTested) {
                Write-Host "⚠️  Recent versions NOT in test matrix:" -ForegroundColor Yellow
                $notTested | ForEach-Object {
                    Write-Host "  •  $_" -ForegroundColor Yellow
                }
                Write-Host ""
                Write-Host "Consider adding these versions to testconfig.json" -ForegroundColor Gray
                Write-Host "Use: .\tools\update-test-matrix.ps1 -Version <version> -SetBaseline" -ForegroundColor Gray
                Write-Host ""
            } else {
                Write-Host "✅ Test matrix is up to date" -ForegroundColor Green
                Write-Host ""
            }
            
            # Check if baseline is latest
            if ($baseline -ne $versions[0]) {
                Write-Host "💡 Baseline is not the latest version" -ForegroundColor Cyan
                Write-Host "   Current baseline: $baseline" -ForegroundColor Gray
                Write-Host "   Latest version:   $($versions[0])" -ForegroundColor Gray
                Write-Host ""
                Write-Host "   Consider updating baseline when ready:" -ForegroundColor Gray
                Write-Host "   .\tools\update-test-matrix.ps1 -Version $($versions[0]) -SetBaseline" -ForegroundColor Gray
                Write-Host ""
            }
            
        } catch {
            Write-Host "⚠️  Error reading testconfig.json: $_" -ForegroundColor Yellow
            Write-Host ""
        }
    } else {
        Write-Host "⚠️  testconfig.json not found at: $testConfigPath" -ForegroundColor Yellow
        Write-Host ""
    }
    
} catch {
    Write-Host "❌ Failed to query NuGet API" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "  • Network connectivity issues" -ForegroundColor Gray
    Write-Host "  • NuGet.org API is down" -ForegroundColor Gray
    Write-Host "  • Package does not exist" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "Done." -ForegroundColor Cyan
Write-Host ""
