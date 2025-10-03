<#!
.SYNOPSIS
    Builds a local Microsoft.Azure.Cosmos.Encryption.Custom NuGet package from source.
.DESCRIPTION
    Packs the Encryption.Custom project using the current source tree so it can be consumed
    by the compatibility test suite (or any other local consumer) without publishing to NuGet.
    The script appends a configurable suffix (default: -next) to the version number to avoid
    clashing with published packages.
.PARAMETER VersionSuffix
    Optional suffix appended to the base version (defaults to "-next").
.PARAMETER OutputPath
    Directory where the generated package should be written. Relative paths are resolved
    from the repository root. Defaults to "artifacts/local-packages".
.EXAMPLE
    .\build-local-encryption-custom-package.ps1
.EXAMPLE
    .\build-local-encryption-custom-package.ps1 -VersionSuffix "-nightly-$(Get-Date -Format yyyyMMdd)"
.EXAMPLE
    .\build-local-encryption-custom-package.ps1 -OutputPath "c:\temp\custom-feed"
#>

[CmdletBinding()]
param(
    [string]$VersionSuffix = "-next",
    [string]$OutputPath = "artifacts/local-packages"
)

$ErrorActionPreference = "Stop"

function Get-RepositoryRoot {
    $current = Resolve-Path $PSScriptRoot
    return (Resolve-Path (Join-Path $current ".."))
}

function Get-ComponentRoot {
    param([string]$RepoRoot)
    return (Join-Path $RepoRoot "Microsoft.Azure.Cosmos.Encryption.Custom")
}

$repoRoot = (Get-RepositoryRoot).Path
$componentRoot = Get-ComponentRoot -RepoRoot $repoRoot
$projectPath = Join-Path $componentRoot "src\Microsoft.Azure.Cosmos.Encryption.Custom.csproj"
$packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"

if (-not (Test-Path $projectPath)) {
    throw "Could not locate project file at $projectPath"
}

$versionPropsPath = Join-Path $repoRoot "Directory.Build.props"
if (-not (Test-Path $versionPropsPath)) {
    throw "Unable to find Directory.Build.props at repository root ($repoRoot)."
}

[xml]$propsXml = Get-Content $versionPropsPath
$baseVersion = $propsXml.Project.PropertyGroup.CustomEncryptionVersion
if ([string]::IsNullOrWhiteSpace($baseVersion)) {
    throw "CustomEncryptionVersion was not found in Directory.Build.props"
}

if ([string]::IsNullOrWhiteSpace($VersionSuffix)) {
    $VersionSuffix = ""
} elseif ($VersionSuffix[0] -ne '-' -and $VersionSuffix[0] -ne '+') {
    $VersionSuffix = "-" + $VersionSuffix
}

$packageVersion = "$baseVersion$VersionSuffix"

if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $resolvedOutput = $OutputPath
} else {
    $resolvedOutput = Join-Path $repoRoot $OutputPath
}

if (-not (Test-Path $resolvedOutput)) {
    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
}

$resolvedOutput = (Resolve-Path $resolvedOutput).Path
$packagePattern = Join-Path $resolvedOutput "$packageId.$packageVersion*.nupkg"
$snupkgPattern = Join-Path $resolvedOutput "$packageId.$packageVersion*.snupkg"

Get-ChildItem -Path $packagePattern -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $snupkgPattern -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "";
Write-Host "🏗️  Building local Encryption.Custom package" -ForegroundColor Cyan
Write-Host "────────────────────────────────────────────" -ForegroundColor Cyan
Write-Host "Source:   $projectPath" -ForegroundColor Gray
Write-Host "Version:  $packageVersion" -ForegroundColor Gray
Write-Host "Output:   $resolvedOutput" -ForegroundColor Gray
Write-Host "";

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "dotnet CLI was not found on the PATH"
}

$packArgs = @(
    "pack", $projectPath,
    "--configuration", "Release",
    "-p:CustomEncryptionVersion=$packageVersion",
    "-p:Version=$packageVersion",
    "-p:PackageVersion=$packageVersion",
    "-p:IsPreview=true",
    "--output", $resolvedOutput
)

& $dotnet.Source @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE"
}

$packagePath = Join-Path $resolvedOutput "$packageId.$packageVersion.nupkg"
if (-not (Test-Path $packagePath)) {
    throw "Expected package was not created at $packagePath"
}

Write-Host "✅ Package created: $packagePath" -ForegroundColor Green
Write-Host "";
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  • Use as a restore source (RestoreSources) when running compatibility tests" -ForegroundColor Gray
Write-Host "  • Optionally add $resolvedOutput to a local NuGet.config" -ForegroundColor Gray
Write-Host "";

[pscustomobject]@{
    Version     = $packageVersion
    OutputPath  = $resolvedOutput
    PackagePath = (Resolve-Path $packagePath).Path
}
