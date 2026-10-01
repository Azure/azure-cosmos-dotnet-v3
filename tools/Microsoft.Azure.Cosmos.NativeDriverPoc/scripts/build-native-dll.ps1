#requires -Version 7.0
<#
.SYNOPSIS
  Build the azure_data_cosmos_driver_native cdylib from PR #4515 and drop
  azurecosmosdriver.dll into the directory the V2 .NET POC consumes.

.DESCRIPTION
  Wraps the canonical Rust build steps so anyone on the team can produce
  the native DLL in one command. Matches the build recipe in the crate's
  README (sdk/cosmos/azure_data_cosmos_driver_native/README.md) and the
  drop-in convention used by Microsoft.Azure.Cosmos.NativeDriverPoc.csproj
  (the MSBuild target reads DriverNativeArtifactDir, default
  Q:\src\.poc-artifacts\azurecosmosdriver\).

.PARAMETER RustRepo
  Local clone of https://github.com/Azure/azure-sdk-for-rust. The script
  will fetch + checkout the feature branch in place.

.PARAMETER Branch
  Feature branch holding the native crate. Default is the PR #4515 branch.

.PARAMETER Configuration
  release (default) | debug. release is what the POC and language
  bindings consume; debug exists for quick local iteration.

.PARAMETER DropDir
  Where to copy the freshly-built azurecosmosdriver.dll. Must match the
  POC's DriverNativeArtifactDir MSBuild property.

.PARAMETER SkipFetch
  Don't run git fetch/checkout — useful when the repo is already on the
  right commit and you only want to rebuild.

.EXAMPLE
  pwsh .\build-native-dll.ps1
  # fetches PR #4515 branch, runs `cargo build --release`, copies
  # azurecosmosdriver.dll into Q:\src\.poc-artifacts\azurecosmosdriver\

.EXAMPLE
  pwsh .\build-native-dll.ps1 -Configuration debug -SkipFetch
  # local iteration loop

.NOTES
  Prereqs:
    * Rust toolchain via rustup (the crate pins channel 1.95 via
      rust-toolchain.toml — rustup will auto-install it on first cargo
      invocation).
    * Cargo on PATH. If `where.exe cargo` is empty, add
      $env:USERPROFILE\.cargo\bin to PATH for this session:
          $env:Path = "$env:USERPROFILE\.cargo\bin;$env:Path"
    * git on PATH.
    * Windows: no extra deps. The crate uses rustls (not native_tls)
      so OpenSSL is NOT required.
    * macOS / Linux: same script logic with the obvious tweaks
      (cargo writes libazurecosmosdriver.{dylib,so} instead).
#>

[CmdletBinding()]
param(
    [string]$RustRepo     = "Q:\src\azure-sdk-for-rust",
    [string]$Branch       = "users/kundadebdatta/4372_cosmos_driver_native_crate_async_impl",
    [ValidateSet('release', 'debug')]
    [string]$Configuration = 'release',
    [string]$DropDir      = "Q:\src\.poc-artifacts\azurecosmosdriver",
    [switch]$SkipFetch
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

function Resolve-Cargo {
    $cargo = Get-Command cargo -ErrorAction SilentlyContinue
    if ($cargo) { return $cargo.Source }
    $fallback = Join-Path $env:USERPROFILE '.cargo\bin\cargo.exe'
    if (Test-Path $fallback) {
        Write-Host "[info] cargo not on PATH, using $fallback" -ForegroundColor Yellow
        $env:Path = (Join-Path $env:USERPROFILE '.cargo\bin') + ';' + $env:Path
        return $fallback
    }
    throw "cargo not found. Install via https://rustup.rs/ and re-open the shell."
}

# ---------------------------------------------------------------------------
# 1. Validate prereqs
# ---------------------------------------------------------------------------
$cargoPath = Resolve-Cargo
Write-Host "[1/5] toolchain    : $(& $cargoPath --version)"

if (-not (Test-Path $RustRepo)) {
    Write-Host "[1/5] cloning      : $RustRepo" -ForegroundColor Yellow
    git clone https://github.com/Azure/azure-sdk-for-rust.git $RustRepo
}

# ---------------------------------------------------------------------------
# 2. Sync the feature branch
# ---------------------------------------------------------------------------
Push-Location $RustRepo
try {
    if (-not $SkipFetch) {
        Write-Host "[2/5] fetching     : $Branch"
        git fetch origin $Branch --quiet
        git checkout $Branch --quiet
        git pull --ff-only origin $Branch --quiet
    } else {
        Write-Host "[2/5] skipping fetch (current HEAD = $(git rev-parse --short HEAD))"
    }

    # ---------------------------------------------------------------------
    # 3. cargo build — produces:
    #      target\$Configuration\azurecosmosdriver.dll          (Windows)
    #      target\$Configuration\azurecosmosdriver.dll.lib      (import lib)
    #      target\$Configuration\azurecosmosdriver.pdb          (debug symbols)
    #    and regenerates sdk\cosmos\azure_data_cosmos_driver_native\include\azurecosmosdriver.h
    #    via the crate's build.rs (cbindgen 0.29.2 in [build-dependencies]).
    # ---------------------------------------------------------------------
    Write-Host "[3/5] building     : $Configuration cdylib for azure_data_cosmos_driver_native"
    $cargoArgs = @('build', '-p', 'azure_data_cosmos_driver_native')
    if ($Configuration -eq 'release') { $cargoArgs += '--release' }
    & $cargoPath @cargoArgs
    if ($LASTEXITCODE -ne 0) { throw "cargo build failed (exit $LASTEXITCODE)" }

    # ---------------------------------------------------------------------
    # 4. Locate the produced artifact (Windows-specific name).
    # ---------------------------------------------------------------------
    $dllSrc = Join-Path $RustRepo "target\$Configuration\azurecosmosdriver.dll"
    if (-not (Test-Path $dllSrc)) {
        throw "expected artifact not found: $dllSrc"
    }
    $dllInfo = Get-Item $dllSrc
    Write-Host "[4/5] built        : $dllSrc ($([math]::Round($dllInfo.Length/1MB, 2)) MB, $($dllInfo.LastWriteTime))"
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# 5. Drop next to the .NET POC's MSBuild target.
# ---------------------------------------------------------------------------
if (-not (Test-Path $DropDir)) {
    New-Item -ItemType Directory -Path $DropDir -Force | Out-Null
}
Copy-Item $dllSrc -Destination $DropDir -Force

# Optional companions: the import lib + the regenerated C header.
$libSrc    = Join-Path $RustRepo "target\$Configuration\azurecosmosdriver.dll.lib"
$pdbSrc    = Join-Path $RustRepo "target\$Configuration\azurecosmosdriver.pdb"
$headerSrc = Join-Path $RustRepo "sdk\cosmos\azure_data_cosmos_driver_native\include\azurecosmosdriver.h"
foreach ($extra in @($libSrc, $pdbSrc, $headerSrc)) {
    if (Test-Path $extra) { Copy-Item $extra -Destination $DropDir -Force }
}

Write-Host "[5/5] dropped to   : $DropDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Green
Write-Host "  dotnet build Q:\src\azure-cosmos-dotnet-v3\worktrees\poc-async-ffi\AsyncFfiPoc.sln"
Write-Host "  dotnet run --project Q:\src\azure-cosmos-dotnet-v3\worktrees\poc-async-ffi\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\"
