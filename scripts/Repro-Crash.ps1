<#
.SYNOPSIS
    Reproduces a single crash file against a fuzz target and prints the stack trace.

.DESCRIPTION
    Rebuilds the FuzzTests harness clean (non-instrumented) so you get real stack
    traces (instrumented DLLs have inflated frames), then runs the single input
    through Targets.<Name>.Fuzz(...).

.PARAMETER Target
    The fuzz target class name (e.g. JsonNavigatorFuzz).

.PARAMETER CrashFile
    Path to a crash-*, oom-*, or timeout-* file produced by Run-Fuzz.ps1.

.EXAMPLE
    .\scripts\Repro-Crash.ps1 -Target JsonNavigatorFuzz `
        -CrashFile .\.fuzz-runs\20251210-153012\JsonNavigatorFuzz\crashes\crash-abc123
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('SqlQueryParserFuzz','JsonNavigatorFuzz','CosmosElementFuzz','FeedResponseFuzz',
                 'ErrorResponseFuzz','PartitionKeyFuzz','ResourceIdentifierFuzz')]
    [string]$Target,

    [Parameter(Mandatory)]
    [string]$CrashFile,

    [switch]$NoRebuild
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$fuzzProj   = Join-Path $repoRoot 'Microsoft.Azure.Cosmos.FuzzTests'
$binDir     = Join-Path $fuzzProj 'bin\Release\net10.0'
$harnessExe = Join-Path $binDir 'Microsoft.Azure.Cosmos.FuzzTests.exe'

if (-not (Test-Path $CrashFile)) { throw "Crash file not found: $CrashFile" }
$CrashFile = (Resolve-Path $CrashFile).Path

if (-not $NoRebuild) {
    Write-Host "Rebuilding clean (non-instrumented) Release..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet build $fuzzProj -c Release --no-incremental --nologo -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    } finally { Pop-Location }
}

$size = (Get-Item $CrashFile).Length
Write-Host "`nReproducing: $CrashFile ($size bytes) against $Target" -ForegroundColor Cyan
Write-Host ('-' * 70)

# The harness's --target mode reads files from a directory and invokes Fuzz() directly
# on the clean (non-instrumented) DLL -- giving us a real .NET stack trace.
$tmpDir = Join-Path ([IO.Path]::GetTempPath()) ("repro-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
try {
    Copy-Item $CrashFile (Join-Path $tmpDir 'input.bin') -Force
    & $harnessExe --target $Target --seeds $tmpDir
    $exit = $LASTEXITCODE
} finally {
    Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ('-' * 70)
if ($exit -eq 0) {
    Write-Host "No exception thrown -- crash may require the instrumented build." -ForegroundColor Yellow
} else {
    Write-Host "Reproduced (exit $exit). Stack trace above identifies the bug site." -ForegroundColor Green
}
exit $exit
