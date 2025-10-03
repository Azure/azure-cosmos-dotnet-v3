<#
.SYNOPSIS
    Downloads a specific version of the package to a local directory for SxS testing
.DESCRIPTION
    This script downloads a specific version of Microsoft.Azure.Cosmos.Encryption.Custom
    from NuGet.org and extracts it to a local directory. Useful for inspecting package
    contents or ensuring packages are available for side-by-side testing.
.PARAMETER Version
    Package version to download (e.g., "1.0.0-preview07")
.PARAMETER OutputPath
    Where to extract the package (default: ./packages-sxs)
.PARAMETER Force
    Overwrite existing package if already downloaded
.EXAMPLE
    .\download-package-version.ps1 -Version "1.0.0-preview07"
.EXAMPLE
    .\download-package-version.ps1 -Version "1.0.0-preview06" -OutputPath "C:\temp\packages"
.EXAMPLE
    .\download-package-version.ps1 -Version "1.0.0-preview07" -Force
#>

param(
    [Parameter(Mandatory=$true, HelpMessage="Package version to download (e.g., 1.0.0-preview07)")]
    [string]$Version,
    
    [Parameter(HelpMessage="Where to extract the package")]
    [string]$OutputPath = ".\packages-sxs",
    
    [Parameter(HelpMessage="Overwrite existing package if already downloaded")]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "📦 Downloading Package for Side-by-Side Testing" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Output: $OutputPath" -ForegroundColor Yellow
Write-Host ""

$packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"
$packageDir = Join-Path $OutputPath "$packageId.$Version"

# Check if already downloaded
if ((Test-Path $packageDir) -and -not $Force) {
    Write-Host "⚠️  Package already downloaded at: $packageDir" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Use -Force to re-download" -ForegroundColor Gray
    Write-Host ""
    exit 0
}

# Verify nuget.exe is available
$nugetPath = Get-Command nuget.exe -ErrorAction SilentlyContinue

if (-not $nugetPath) {
    Write-Host "⚠️  nuget.exe not found in PATH" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Attempting to download nuget.exe..." -ForegroundColor Gray
    
    $nugetExe = Join-Path $PSScriptRoot "nuget.exe"
    
    if (-not (Test-Path $nugetExe)) {
        $nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
        
        try {
            Invoke-WebRequest -Uri $nugetUrl -OutFile $nugetExe -UseBasicParsing
            Write-Host "✅ Downloaded nuget.exe" -ForegroundColor Green
        } catch {
            Write-Host "❌ Failed to download nuget.exe: $_" -ForegroundColor Red
            Write-Host ""
            Write-Host "Please install NuGet CLI manually:" -ForegroundColor Yellow
            Write-Host "  https://www.nuget.org/downloads" -ForegroundColor Gray
            Write-Host ""
            exit 1
        }
    }
    
    $nugetPath = $nugetExe
} else {
    $nugetPath = $nugetPath.Source
}

Write-Host "Using nuget: $nugetPath" -ForegroundColor Gray
Write-Host ""

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
    Write-Host "Created output directory: $OutputPath" -ForegroundColor Gray
}

# Download package
Write-Host "Downloading $packageId version $Version..." -ForegroundColor Cyan

try {
    & $nugetPath install $packageId `
        -Version $Version `
        -OutputDirectory $OutputPath `
        -NonInteractive `
        -PackageSaveMode nuspec `
        -Source "https://api.nuget.org/v3/index.json"
    
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet install failed with exit code $LASTEXITCODE"
    }
} catch {
    Write-Host ""
    Write-Host "❌ Failed to download package: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify version exists: .\tools\discover-published-versions.ps1" -ForegroundColor Gray
    Write-Host "  2. Check network connectivity: Test-NetConnection api.nuget.org -Port 443" -ForegroundColor Gray
    Write-Host "  3. Clear NuGet cache: dotnet nuget locals all --clear" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Verify download
$dllPath = Join-Path $packageDir "lib\netstandard2.0\$packageId.dll"

if (Test-Path $dllPath) {
    Write-Host ""
    Write-Host "✅ Package downloaded successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "Package Location:" -ForegroundColor Cyan
    Write-Host "  Directory: $packageDir" -ForegroundColor Gray
    Write-Host "  DLL: $dllPath" -ForegroundColor Gray
    Write-Host ""
    
    # Display assembly info
    try {
        $assembly = [Reflection.Assembly]::LoadFrom($dllPath)
        
        Write-Host "Assembly Information:" -ForegroundColor Cyan
        Write-Host "  Full Name: $($assembly.FullName)" -ForegroundColor Gray
        Write-Host "  Version: $($assembly.GetName().Version)" -ForegroundColor Gray
        Write-Host "  Location: $($assembly.Location)" -ForegroundColor Gray
        Write-Host ""
        
        # List some key types
        Write-Host "Sample Public Types:" -ForegroundColor Cyan
        $publicTypes = $assembly.GetExportedTypes() | Select-Object -First 5
        foreach ($type in $publicTypes) {
            Write-Host "  • $($type.FullName)" -ForegroundColor Gray
        }
        
        $totalTypes = $assembly.GetExportedTypes().Count
        if ($totalTypes -gt 5) {
            Write-Host "  ... and $($totalTypes - 5) more" -ForegroundColor Gray
        }
        
    } catch {
        Write-Host "⚠️  Could not load assembly for inspection: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "❌ DLL not found at expected location: $dllPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Package contents:" -ForegroundColor Yellow
    Get-ChildItem $packageDir -Recurse | Select-Object FullName | Format-Table -AutoSize
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "─────────────────────────────────────" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Inspect package contents: explorer $packageDir" -ForegroundColor Gray
Write-Host "  2. Use in SxS tests: VersionLoader.Load(`"$Version`")" -ForegroundColor Gray
Write-Host "  3. Download another version: .\tools\download-package-version.ps1 -Version <version>" -ForegroundColor Gray
Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
