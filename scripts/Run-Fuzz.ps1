<#
.SYNOPSIS
    Runs SharpFuzz + libfuzzer-dotnet against one or all Cosmos SDK fuzz targets,
    organizes outputs under .fuzz-runs/<timestamp>/<target>/, and prints a summary.

.DESCRIPTION
    This is the RECOMMENDED way to run fuzz tests locally. It handles:
      * Building Release config of the FuzzTests project
      * Instrumenting the SDK DLL with sharpfuzz (only once per run)
      * Copying seeds to a fresh corpus folder (so the seed folder is never polluted)
      * Running libfuzzer-dotnet with consistent flags
      * Capturing stdout/stderr to a log file
      * Collecting crash-*, oom-*, timeout-*, slow-unit-* files into per-target folders
      * Printing a final summary table

.PARAMETER Target
    One of: SqlQueryParserFuzz, JsonNavigatorFuzz, CosmosElementFuzz, FeedResponseFuzz,
            ErrorResponseFuzz, PartitionKeyFuzz, ResourceIdentifierFuzz, all

.PARAMETER Seconds
    Per-target fuzz duration (default 300 = 5 minutes).

.PARAMETER Mode
    'fuzz' (default) = full coverage-guided fuzzing
    'validate'       = run seeds through the non-instrumented harness only (fast PR check)

.EXAMPLE
    .\scripts\Run-Fuzz.ps1 -Target JsonNavigatorFuzz -Seconds 60

.EXAMPLE
    .\scripts\Run-Fuzz.ps1 -Target all -Seconds 120

.EXAMPLE
    .\scripts\Run-Fuzz.ps1 -Mode validate -Target all
#>
[CmdletBinding()]
param(
    [ValidateSet('SqlQueryParserFuzz','JsonNavigatorFuzz','CosmosElementFuzz','FeedResponseFuzz',
                 'ErrorResponseFuzz','PartitionKeyFuzz','ResourceIdentifierFuzz','all')]
    [string]$Target = 'all',

    [int]$Seconds = 300,

    [ValidateSet('fuzz','validate')]
    [string]$Mode = 'fuzz',

    [int]$MaxLen = 8192,

    [int]$TimeoutSec = 25
)

$ErrorActionPreference = 'Stop'
$repoRoot     = Split-Path -Parent $PSScriptRoot
$fuzzProj     = Join-Path $repoRoot 'Microsoft.Azure.Cosmos.FuzzTests'
$binDir       = Join-Path $fuzzProj 'bin\Release\net10.0'
$harnessExe   = Join-Path $binDir 'Microsoft.Azure.Cosmos.FuzzTests.exe'
$sdkDll       = Join-Path $binDir 'Microsoft.Azure.Cosmos.Client.dll'
$seedsRoot    = Join-Path $fuzzProj 'seeds'
$runsRoot     = Join-Path $repoRoot '.fuzz-runs'

# Map target -> seed folder
$seedMap = @{
    'SqlQueryParserFuzz'     = 'sql-parser'
    'JsonNavigatorFuzz'      = 'json-parser'
    'CosmosElementFuzz'      = 'json-parser'
    'FeedResponseFuzz'       = 'feed-response'
    'ErrorResponseFuzz'      = 'error-response'
    'PartitionKeyFuzz'       = 'partition-key'
    'ResourceIdentifierFuzz' = 'resource-id'
}

$allTargets = $seedMap.Keys | Sort-Object
$targets    = if ($Target -eq 'all') { $allTargets } else { ,$Target }

function Write-Section($text) {
    Write-Host ''
    Write-Host ('=' * 70) -ForegroundColor Cyan
    Write-Host $text -ForegroundColor Cyan
    Write-Host ('=' * 70) -ForegroundColor Cyan
}

# ---------- Build ----------
Write-Section "Building $fuzzProj (Release)"
Push-Location $repoRoot
try {
    dotnet build $fuzzProj -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} finally { Pop-Location }

# ---------- Validate mode ----------
if ($Mode -eq 'validate') {
    Write-Section "Seed validation (non-instrumented)"
    $failed = @()
    foreach ($t in $targets) {
        Write-Host "`n--- $t ---" -ForegroundColor Yellow
        $seedDir = Join-Path $seedsRoot $seedMap[$t]
        & $harnessExe --target $t --seeds $seedDir
        if ($LASTEXITCODE -ne 0) { $failed += $t }
    }
    if ($failed.Count -gt 0) {
        Write-Host "`nFAILED: $($failed -join ', ')" -ForegroundColor Red
        exit 1
    }
    Write-Host "`nAll seed validation passed." -ForegroundColor Green
    exit 0
}

# ---------- Locate libfuzzer-dotnet & sharpfuzz ----------
$libfuzzer = (Get-Command libfuzzer-dotnet -ErrorAction SilentlyContinue)?.Source
if (-not $libfuzzer) { $libfuzzer = "$env:USERPROFILE\.dotnet\tools\libfuzzer-dotnet.exe" }
if (-not (Test-Path $libfuzzer)) {
    throw "libfuzzer-dotnet.exe not found. Download from https://github.com/Metalnem/libfuzzer-dotnet/releases and place at $libfuzzer"
}
$sharpfuzz = (Get-Command sharpfuzz -ErrorAction SilentlyContinue)?.Source
if (-not $sharpfuzz) { throw "sharpfuzz CLI not found. Install with: dotnet tool install --global SharpFuzz.CommandLine" }

# ---------- Instrument SDK DLL (once per run) ----------
Write-Section "Instrumenting $sdkDll with SharpFuzz"
& $sharpfuzz $sdkDll
if ($LASTEXITCODE -ne 0) { throw "sharpfuzz instrumentation failed" }

# ---------- Run each target ----------
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot   = Join-Path $runsRoot $timestamp
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Write-Host "`nRun directory: $runRoot" -ForegroundColor Gray

$summary = @()
foreach ($t in $targets) {
    Write-Section "Fuzzing $t for ${Seconds}s"
    $tDir    = Join-Path $runRoot $t
    $corpus  = Join-Path $tDir 'corpus'
    $crashes = Join-Path $tDir 'crashes'
    $log     = Join-Path $tDir 'fuzz.log'
    New-Item -ItemType Directory -Path $corpus,$crashes -Force | Out-Null

    # Copy seeds to a fresh corpus folder so the source seeds/ stays clean
    $seedSrc = Join-Path $seedsRoot $seedMap[$t]
    if (Test-Path $seedSrc) {
        Copy-Item (Join-Path $seedSrc '*') $corpus -Force -ErrorAction SilentlyContinue
    }

    # Run libfuzzer-dotnet; -artifact_prefix puts crash files into $crashes\
    # NOTE: trailing backslash is REQUIRED so files become $crashes\crash-<hash>
    $artifactPrefix = "$crashes\"
    $lfArgs = @(
        "--target_path=$harnessExe",
        "--target_arg=--libfuzzer $t",
        "-max_total_time=$Seconds",
        "-max_len=$MaxLen",
        "-timeout=$TimeoutSec",
        "-artifact_prefix=$artifactPrefix",
        "-print_final_stats=1",
        $corpus
    )
    Write-Host "> libfuzzer-dotnet $($lfArgs -join ' ')" -ForegroundColor DarkGray
    & $libfuzzer @lfArgs 2>&1 | Tee-Object -FilePath $log
    $exit = $LASTEXITCODE

    $crashCount  = (Get-ChildItem $crashes -Filter 'crash-*'  -ErrorAction SilentlyContinue).Count
    $oomCount    = (Get-ChildItem $crashes -Filter 'oom-*'    -ErrorAction SilentlyContinue).Count
    $toCount     = (Get-ChildItem $crashes -Filter 'timeout-*' -ErrorAction SilentlyContinue).Count
    $slowCount   = (Get-ChildItem $crashes -Filter 'slow-unit-*' -ErrorAction SilentlyContinue).Count
    $corpusCount = (Get-ChildItem $corpus -File -ErrorAction SilentlyContinue).Count

    $summary += [pscustomobject]@{
        Target  = $t
        Crashes = $crashCount
        OOM     = $oomCount
        Timeout = $toCount
        Slow    = $slowCount
        Corpus  = $corpusCount
        Exit    = $exit
        LogPath = (Resolve-Path $log -Relative)
    }
}

# ---------- Summary ----------
Write-Section "Summary  (run dir: $runRoot)"
$summary | Format-Table -AutoSize | Out-String | Write-Host

$totalCrash = ($summary | Measure-Object Crashes -Sum).Sum + ($summary | Measure-Object OOM -Sum).Sum
if ($totalCrash -gt 0) {
    Write-Host "Found $totalCrash crash/OOM artifact(s). Reproduce with:" -ForegroundColor Yellow
    Write-Host "  .\scripts\Repro-Crash.ps1 -Target <Name> -CrashFile <path>" -ForegroundColor Yellow
    exit 1
}
Write-Host "No crashes found. " -ForegroundColor Green
exit 0
