<#
.SYNOPSIS
    Builds the current source code as a NuGet package for compatibility testing
.DESCRIPTION
    This script builds the current Encryption.Custom source code into a NuGet package
    that can be tested alongside published versions. The package is versioned with
    a timestamp suffix to ensure it's always treated as the latest.
.PARAMETER Force
    Force rebuild even if a package already exists
.EXAMPLE
    .\build-current-source.ps1
.EXAMPLE
    .\build-current-source.ps1 -Force
#>

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "🔨 Building Current Source for Compatibility Testing" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$projectPath = Join-Path $repoRoot "Microsoft.Azure.Cosmos.Encryption.Custom/src/Microsoft.Azure.Cosmos.Encryption.Custom.csproj"
$outputPath = Join-Path $repoRoot "artifacts/local-packages"

Write-Host "Repository Root: $repoRoot" -ForegroundColor Gray
Write-Host "Project Path:    $projectPath" -ForegroundColor Gray
Write-Host "Output Path:     $outputPath" -ForegroundColor Gray
Write-Host ""

# Verify project exists
if (-not (Test-Path $projectPath)) {
    Write-Host "❌ Project not found: $projectPath" -ForegroundColor Red
    exit 1
}

# Create output directory
if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    Write-Host "Created output directory: $outputPath" -ForegroundColor Gray
}

# Check if package already exists (unless Force)
if (-not $Force) {
    $existingPackages = Get-ChildItem -Path $outputPath -Filter "Microsoft.Azure.Cosmos.Encryption.Custom.*current*.nupkg" -ErrorAction SilentlyContinue
    if ($existingPackages) {
        $latestPackage = $existingPackages | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $ageMinutes = (Get-Date) - $latestPackage.LastWriteTime
        
        if ($ageMinutes.TotalMinutes -lt 5) {
            Write-Host "✅ Recent package already exists (built $([math]::Round($ageMinutes.TotalMinutes, 1)) minutes ago)" -ForegroundColor Green
            Write-Host "   $($latestPackage.FullName)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Use -Force to rebuild" -ForegroundColor Yellow
            Write-Host ""
            exit 0
        }
    }
}

# Generate version suffix with timestamp
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$versionSuffix = "current-$timestamp"
$fullVersion = "1.0.0-$versionSuffix"

Write-Host "Building package..." -ForegroundColor Yellow
Write-Host "  Full version: $fullVersion" -ForegroundColor Gray
Write-Host ""

# Build the package
try {
    # First restore to ensure all dependencies are available
    Write-Host "Restoring dependencies..." -ForegroundColor Gray
    dotnet restore $projectPath --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE"
    }
    
    # Pack the project with custom version
    # Note: We override CustomEncryptionVersion to include our timestamp
    $fullVersion = "1.0.0-$versionSuffix"
    Write-Host "Packing project..." -ForegroundColor Gray
    dotnet pack $projectPath `
        --configuration Release `
        --output $outputPath `
        -p:CustomEncryptionVersion=$fullVersion `
        --no-restore `
        --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        throw "Pack failed with exit code $LASTEXITCODE"
    }
    
} catch {
    Write-Host ""
    Write-Host "❌ Build failed: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}

# Find the built package
$packageFiles = Get-ChildItem -Path $outputPath -Filter "Microsoft.Azure.Cosmos.Encryption.Custom.$fullVersion.nupkg" | 
    Where-Object { -not $_.Name.EndsWith(".symbols.nupkg") }

if ($packageFiles.Count -eq 0) {
    Write-Host ""
    Write-Host "❌ Package not found after build" -ForegroundColor Red
    Write-Host ""
    exit 1
}

$package = $packageFiles[0]
$packageName = $package.Name
$packageSize = [math]::Round($package.Length / 1KB, 2)

Write-Host ""
Write-Host "✅ Package built successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Package Details:" -ForegroundColor Cyan
Write-Host "  Name:     $packageName" -ForegroundColor Gray
Write-Host "  Size:     $packageSize KB" -ForegroundColor Gray
Write-Host "  Location: $($package.FullName)" -ForegroundColor Gray
Write-Host ""

# Extract version from package name
$version = $packageName -replace 'Microsoft\.Azure\.Cosmos\.Encryption\.Custom\.', '' -replace '\.nupkg$', ''
Write-Host "  Version:  $version" -ForegroundColor Yellow
Write-Host ""

Write-Host "─────────────────────────────────────" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Run compatibility tests: .\test-compatibility.ps1" -ForegroundColor Gray
Write-Host "  2. The 'current' version in testconfig.json will use this package" -ForegroundColor Gray
Write-Host "  3. Tests will validate current source against published versions" -ForegroundColor Gray
Write-Host ""
Write-Host "Package is ready for testing! 🎉" -ForegroundColor Green
Write-Host ""
