<#
.SYNOPSIS
    Local compatibility testing script
.DESCRIPTION
    Tests the Encryption.Custom library against published NuGet versions
.PARAMETER Version
    Specific version to test against. If omitted, tests against all versions in testconfig.json
.PARAMETER CurrentOnly
    Test against current branch build only
.PARAMETER UseLocalBuild
    Builds the Encryption.Custom project into a local NuGet package (with a suffix) and tests against it
.PARAMETER VersionSuffix
    When -UseLocalBuild is specified, append this suffix to the base version (default: -next)
.PARAMETER LocalFeed
    Output directory for locally built packages (default: artifacts/local-packages)
.EXAMPLE
    .\test-compatibility.ps1 -Version "1.0.0-preview08"
.EXAMPLE
    .\test-compatibility.ps1
#>

param(
    [string]$Version,
    [switch]$CurrentOnly,
    [switch]$UseLocalBuild,
    [string]$VersionSuffix = "-next",
    [string]$LocalFeed = "artifacts/local-packages"
)

$ErrorActionPreference = "Stop"
$testProject = "Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests"
$testPath = Join-Path $PSScriptRoot $testProject
$componentRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." )).Path
$repoRoot = (Resolve-Path (Join-Path $componentRoot ".." )).Path

function Get-GlobalPackagesPath {
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        try {
            $resolved = Resolve-Path $env:NUGET_PACKAGES -ErrorAction Stop
            return $resolved.Path
        } catch {
            return $env:NUGET_PACKAGES
        }
    }

    try {
        $localsOutput = dotnet nuget locals global-packages --list 2>$null
        if ($LASTEXITCODE -eq 0 -and $localsOutput) {
            foreach ($line in $localsOutput) {
                if ($line -match "global-packages:\s*(.+)") {
                    $candidate = $matches[1].Trim()
                    if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                        try {
                            $resolvedCandidate = Resolve-Path $candidate -ErrorAction Stop
                            return $resolvedCandidate.Path
                        } catch {
                            return $candidate
                        }
                    }
                }
            }
        }
    } catch {
        # Fall back to default location
    }

    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    return Join-Path $userProfile ".nuget\packages"
}

$globalPackagesPath = Get-GlobalPackagesPath
if (-not [string]::IsNullOrWhiteSpace($globalPackagesPath)) {
    $env:NUGET_PACKAGES = $globalPackagesPath
    Write-Host "Using global packages path: $globalPackagesPath" -ForegroundColor DarkGray
}

function Get-MatrixVersions {
    param([string]$TestProjectPath)

    $configPath = Join-Path $TestProjectPath "testconfig.json"
    if (-not (Test-Path $configPath)) {
        return @()
    }

    try {
        $content = Get-Content $configPath -Raw
        $config = $content | ConvertFrom-Json
        return @($config.versionMatrix.versions)
    } catch {
        Write-Host "⚠️  Unable to parse testconfig.json: $_" -ForegroundColor Yellow
        return @()
    }
}

function Invoke-PackageRestore {
    param(
        [string]$Version,
        [string]$TestProjectPath,
        [string[]]$Sources
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return
    }

    $packageDirectory = Join-Path $globalPackagesPath "microsoft.azure.cosmos.encryption.custom"
    $versionPath = Join-Path $packageDirectory $Version
    $libPath = Join-Path $versionPath "lib"
    $tfmPath = Join-Path $libPath "netstandard2.0"
    $packageDll = Join-Path $tfmPath "Microsoft.Azure.Cosmos.Encryption.Custom.dll"

    if (Test-Path $packageDll) {
        Write-Host "✔️  Cache hit for $Version" -ForegroundColor DarkGray
        return
    }

    Write-Host "↻ Restoring package version $Version" -ForegroundColor Gray

    $restoreArgs = @("restore", $TestProjectPath, "-p:TargetEncryptionCustomVersion=$Version")
    foreach ($source in $Sources) {
        if ([string]::IsNullOrWhiteSpace($source)) {
            continue
        }
        $restoreArgs += "--source"
        $restoreArgs += $source
    }

    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed while downloading package version $Version"
    }
}

Write-Host "🧪 Compatibility Testing for Encryption.Custom" -ForegroundColor Cyan
Write-Host ""

if ($UseLocalBuild -and $CurrentOnly) {
    throw "-UseLocalBuild cannot be combined with -CurrentOnly"
}

if ($UseLocalBuild -and $Version) {
    Write-Host "⚠️  Ignoring -Version because -UseLocalBuild builds a bespoke package version" -ForegroundColor Yellow
    Write-Host ""
    $Version = $null
}

if ($UseLocalBuild) {
    $buildScript = Join-Path $repoRoot "tools\build-local-encryption-custom-package.ps1"
    if (-not (Test-Path $buildScript)) {
        throw "Expected build script not found at $buildScript"
    }

    $buildArgs = @{"VersionSuffix" = $VersionSuffix}
    if ($LocalFeed) {
        $buildArgs["OutputPath"] = $LocalFeed
    }

    $buildResult = & $buildScript @buildArgs
    $localVersion = $buildResult.Version
    $localFeedPath = $buildResult.OutputPath

    Write-Host "Testing against locally built package version $localVersion" -ForegroundColor Yellow
    Write-Host "Feed: $localFeedPath" -ForegroundColor Gray
    Write-Host ""

    $nugetSource = "https://api.nuget.org/v3/index.json"
    $matrixVersions = Get-MatrixVersions -TestProjectPath $testPath

    foreach ($matrixVersion in $matrixVersions) {
        if ($matrixVersion -eq $localVersion) {
            continue
        }

        Invoke-PackageRestore -Version $matrixVersion -TestProjectPath $testPath -Sources @($nugetSource)
    }

    Invoke-PackageRestore -Version $localVersion -TestProjectPath $testPath -Sources @($localFeedPath, $nugetSource)

    dotnet test $testPath `
        -p:TargetEncryptionCustomVersion=$localVersion `
        --configuration Release `
        --logger "console;verbosity=normal" `
        --no-build `
        --no-restore

    exit $LASTEXITCODE
}

if ($CurrentOnly) {
    Write-Host "Testing against current branch build..." -ForegroundColor Yellow
    dotnet test $testPath --configuration Release --logger "console;verbosity=normal"
    exit $LASTEXITCODE
}

if ($Version) {
    Write-Host "Testing against version: $Version" -ForegroundColor Yellow
    dotnet test $testPath -p:TargetEncryptionCustomVersion=$Version --configuration Release --logger "console;verbosity=normal"
    exit $LASTEXITCODE
}

# Test against all versions in matrix
$configPath = Join-Path $testPath "testconfig.json"
$config = Get-Content $configPath | ConvertFrom-Json
$versions = $config.versionMatrix.versions

Write-Host "Testing against $($versions.Count) versions from matrix:" -ForegroundColor Yellow
$versions | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
Write-Host ""

$failed = @()
$passed = @()

foreach ($ver in $versions) {
    Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "Testing version: $ver" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
    
    dotnet test $testPath -p:TargetEncryptionCustomVersion=$ver --configuration Release --logger "console;verbosity=minimal" --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        $passed += $ver
        Write-Host "✅ PASSED: $ver" -ForegroundColor Green
    } else {
        $failed += $ver
        Write-Host "❌ FAILED: $ver" -ForegroundColor Red
    }
    Write-Host ""
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Passed: $($passed.Count)" -ForegroundColor Green
Write-Host "Failed: $($failed.Count)" -ForegroundColor Red

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed versions:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "✅ All compatibility tests passed!" -ForegroundColor Green
exit 0
