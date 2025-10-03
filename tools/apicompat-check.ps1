<#
.SYNOPSIS
    Checks API compatibility between current build and baseline version
.PARAMETER BaselineVersion
    Version to compare against (default: last published)
.PARAMETER Strict
    Enable strict mode (fail on any change)
.EXAMPLE
    .\apicompat-check.ps1 -BaselineVersion "1.0.0-preview07"
.EXAMPLE
    .\apicompat-check.ps1 -BaselineVersion "1.0.0-preview07" -Strict
#>

param(
    [string]$BaselineVersion = "1.0.0-preview07",
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 API Compatibility Check" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Baseline Version: $BaselineVersion" -ForegroundColor Yellow
Write-Host "Strict Mode: $Strict" -ForegroundColor Yellow
Write-Host ""

# Ensure ApiCompat tool is installed
$toolName = "Microsoft.DotNet.ApiCompat.Tool"
Write-Host "Checking for $toolName..." -ForegroundColor Yellow

$toolInstalled = dotnet tool list --global | Select-String $toolName

if (-not $toolInstalled) {
    Write-Host "Installing $toolName..." -ForegroundColor Yellow
    dotnet tool install --global $toolName --version 8.0.*
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install $toolName"
    }
    Write-Host "✅ Tool installed successfully" -ForegroundColor Green
} else {
    Write-Host "✅ Tool already installed" -ForegroundColor Green
}

Write-Host ""

# Build current version
Write-Host "Building current version..." -ForegroundColor Yellow
$projectPath = "Microsoft.Azure.Cosmos.Encryption.Custom\src\Microsoft.Azure.Cosmos.Encryption.Custom.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project not found: $projectPath"
}

dotnet build $projectPath `
    --configuration Release `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
}

Write-Host "✅ Build succeeded" -ForegroundColor Green
Write-Host ""

# Download baseline package
$packagesDir = ".\packages-temp-apicompat"
$baselineDir = "$packagesDir\Microsoft.Azure.Cosmos.Encryption.Custom.$BaselineVersion"

if (Test-Path $packagesDir) {
    Write-Host "Cleaning up previous temp directory..." -ForegroundColor Gray
    Remove-Item $packagesDir -Recurse -Force
}

Write-Host "Downloading baseline package $BaselineVersion..." -ForegroundColor Yellow

# Check if nuget.exe is available
$nugetExe = Get-Command nuget -ErrorAction SilentlyContinue

if (-not $nugetExe) {
    Write-Host "NuGet.exe not found, using dotnet restore to fetch package..." -ForegroundColor Yellow
    
    # Create a temporary project to download the package
    $tempCsproj = "$packagesDir\temp.csproj"
    New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null
    
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Cosmos.Encryption.Custom" Version="$BaselineVersion" />
  </ItemGroup>
</Project>
"@ | Out-File $tempCsproj -Encoding UTF8

    dotnet restore $tempCsproj --packages $packagesDir\packages
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to download baseline package $BaselineVersion. Ensure the version exists on NuGet.org"
    }
    
    $baselineDir = Get-ChildItem -Path "$packagesDir\packages\microsoft.azure.cosmos.encryption.custom" -Directory | 
                   Where-Object { $_.Name -eq $BaselineVersion } | 
                   Select-Object -First 1 -ExpandProperty FullName
} else {
    nuget install Microsoft.Azure.Cosmos.Encryption.Custom `
        -Version $BaselineVersion `
        -OutputDirectory $packagesDir `
        -NonInteractive

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to download baseline package $BaselineVersion. Ensure the version exists on NuGet.org"
    }
}

Write-Host "✅ Package downloaded" -ForegroundColor Green
Write-Host ""

# Locate assemblies
$currentDll = "Microsoft.Azure.Cosmos.Encryption.Custom\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Encryption.Custom.dll"
$baselineDll = "$baselineDir\lib\netstandard2.0\Microsoft.Azure.Cosmos.Encryption.Custom.dll"

Write-Host "Locating assemblies..." -ForegroundColor Yellow
Write-Host "  Current:  $currentDll" -ForegroundColor Gray
Write-Host "  Baseline: $baselineDll" -ForegroundColor Gray

if (-not (Test-Path $currentDll)) {
    Write-Error "Current assembly not found: $currentDll"
}

if (-not (Test-Path $baselineDll)) {
    Write-Error "Baseline assembly not found: $baselineDll"
}

Write-Host "✅ Assemblies located" -ForegroundColor Green
Write-Host ""

# Check for suppression file
$suppressionFile = "Microsoft.Azure.Cosmos.Encryption.Custom\ApiCompatSuppressions.txt"
$useSuppression = Test-Path $suppressionFile

# Run ApiCompat
Write-Host "Running API compatibility check..." -ForegroundColor Yellow
Write-Host ""

$apiCompatArgs = @(
    "--left", $currentDll,
    "--right", $baselineDll
)

if ($useSuppression) {
    Write-Host "Using suppression file: $suppressionFile" -ForegroundColor Gray
    $apiCompatArgs += "--suppression-file"
    $apiCompatArgs += $suppressionFile
}

if ($Strict) {
    Write-Host "Strict mode enabled - any API change will fail" -ForegroundColor Yellow
    $apiCompatArgs += "--strict-mode"
}

Write-Host ""
Write-Host "Executing: apicompat $($apiCompatArgs -join ' ')" -ForegroundColor Gray
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan

try {
    # Run the apicompat tool (installed as global tool)
    & apicompat $apiCompatArgs
    
    $exitCode = $LASTEXITCODE
} catch {
    Write-Host "Error running apicompat: $_" -ForegroundColor Red
    Write-Host "Ensure the tool is installed: dotnet tool install --global Microsoft.DotNet.ApiCompat.Tool" -ForegroundColor Yellow
    $exitCode = 1
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Cleanup
Write-Host "Cleaning up temporary files..." -ForegroundColor Gray
Remove-Item $packagesDir -Recurse -Force -ErrorAction SilentlyContinue

# Report results
if ($exitCode -eq 0) {
    Write-Host "✅ No breaking API changes detected" -ForegroundColor Green
    Write-Host ""
    Write-Host "The current build is API-compatible with version $BaselineVersion" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Breaking API changes detected!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review the output above for details." -ForegroundColor Red
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the changes and determine if they are intentional" -ForegroundColor Gray
    Write-Host "  2. If intentional, document in docs/compatibility-testing/API-CHANGES.md" -ForegroundColor Gray
    Write-Host "  3. Add suppressions to $suppressionFile if needed" -ForegroundColor Gray
    Write-Host "  4. Update baseline version if this is a new major/minor release" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
