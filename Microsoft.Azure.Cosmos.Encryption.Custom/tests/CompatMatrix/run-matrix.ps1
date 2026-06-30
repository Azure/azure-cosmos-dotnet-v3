# ------------------------------------------------------------
# Copyright (c) Microsoft Corporation.  All rights reserved.
# ------------------------------------------------------------
# Compat-matrix launcher: starts/uses the Docker Linux emulator, pins two
# subprocesses (OLD 1.0.0-preview07 from nuget.org, NEW 2.0.0-preview01 from
# local-feed), cross-writes/reads one shared Cosmos DB, prints a PASS/FAIL grid.
# Skips gracefully (exit 3) when the emulator is unreachable; exit 1 on data break.
[CmdletBinding()]
param(
  [string]$Endpoint = $env:COSMOS_ENDPOINT,
  [string]$Key      = $env:COSMOS_KEY,
  [string]$Database = "compat-matrix-$([Guid]::NewGuid().ToString('N').Substring(0,8))",
  [ValidateSet('Newtonsoft','Stream','both')]
  [string]$Processor = 'both',
  [switch]$IncludeCurrent,
  [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Endpoint) { $Endpoint = 'http://127.0.0.1:8081/' }
if (-not $Key) { $Key = 'C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==' }
$old = "$root\Old\bin\Release\net8.0\CompatMatrix.Old.dll"
$new = "$root\New\bin\Release\net8.0\CompatMatrix.New.dll"
$current = "$root\Current\bin\Release\net8.0\CompatMatrix.Current.dll"
$nodes = @(
  [pscustomobject]@{ Name='old'; Dll=$old; Project="$root\Old\CompatMatrix.Old.csproj"; Expected='1.0.0-preview07' },
  [pscustomobject]@{ Name='new'; Dll=$new; Project="$root\New\CompatMatrix.New.csproj"; Expected='2.0.0-preview01' }
)
if ($IncludeCurrent) {
  # Current is opt-in regression infrastructure: it builds Encryption.Custom from this branch's source
  # through ProjectReference, rather than consuming the shared local-feed package.
  $nodes += [pscustomobject]@{ Name='current'; Dll=$current; Project="$root\Current\CompatMatrix.Current.csproj"; Expected='1.0.0-preview09' }
}
if (-not $NoBuild) {
  foreach ($n in $nodes) { dotnet build $n.Project -c Release -v q | Out-Null }
}

function VersionInfo($dll){ & dotnet $dll "--role=version" 2>&1 }
function Assert-Version($line,$node,$expected) {
  if ($line -notmatch "^VERSION\|$node\|$expected\|([^|]+)\|([^|]+)$") {
    Write-Host "VERSION BREAK: $node subprocess did not report expected package $expected." -ForegroundColor Red
    Write-Host "  $line"; exit 1
  }
  $informationalBase = $Matches[1].Split('+')[0]
  if ($informationalBase -ne $expected) {
    Write-Host "VERSION BREAK: $node loaded informational version '$($Matches[1])', expected '$expected'." -ForegroundColor Red
    exit 1
  }
  return $informationalBase
}
$versions = @{}
foreach ($n in $nodes) {
  $versions[$n.Name] = Assert-Version (VersionInfo $n.Dll) $n.Name $n.Expected
}
$distinctVersionCount = @($versions.Values | Sort-Object -Unique).Count
if ($distinctVersionCount -ne $nodes.Count) {
  Write-Host "VERSION BREAK: matrix nodes loaded duplicate Microsoft.Azure.Cosmos.Encryption.Custom versions." -ForegroundColor Red
  $versions.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0}={1}" -f $_.Name, $_.Value) }
  exit 1
}
Write-Host ("Versions: " + (($versions.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0}={1}" -f $_.Name.ToUpperInvariant(), $_.Value }) -join ' '))

function Reachable($ep) {
  foreach ($u in @($ep, ($ep -replace '^https','http'), ($ep -replace '^http:','https:'))) {
    $code = (& curl.exe -sk --max-time 6 $u -o NUL -w '%{http_code}' 2>$null)
    if ($code -match '^[23]') { return $u }
  }
  return $null
}

$live = Reachable $Endpoint
if (-not $live) {
  Write-Host "SKIP: Cosmos emulator not reachable at $Endpoint. Start it, e.g.:" -ForegroundColor Yellow
  Write-Host "  docker run -d --name cosmos-emu -p 8081:8081 -p 1234:1234 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview"
  Write-Host "Subprocesses build fine offline; only cross-read/write cells need the emulator."
  exit 3
}
$Endpoint = $live
Write-Host "Emulator: $Endpoint  DB: $Database"

$ea = @("--endpoint=$Endpoint","--key=$Key","--db=$Database","--processor=$Processor")
$cells = @()
$wrote = @()
function Run($dll,$role,$peer){ & dotnet $dll "--role=$role" "--peer=$peer" @ea 2>&1 }

foreach ($n in $nodes) {
  Run $n.Dll write $n.Name | Tee-Object -Variable writeOutput | Out-Host
  $wrote += $writeOutput
}

# Write-side gate: AEAD+Stream must THROW on a version that supports Stream (NEW=preview01).
# A WROTE|UNSUPPORTED-DID-NOT-THROW means the no-op slipped through -> hard fail (was silently dropped).
$didNotThrow = @($wrote | Where-Object { $_ -match '^WROTE\|UNSUPPORTED-DID-NOT-THROW\|' })
if ($didNotThrow.Count) {
  Write-Host "WRITE BREAK: unsupported cell did not throw on a Stream-capable version:" -ForegroundColor Red
  $didNotThrow | Out-Host; exit 1
}

foreach ($reader in $nodes) {
  foreach ($writer in $nodes) {
    $cells += (Run $reader.Dll read $writer.Name)
  }
}

$grid = $cells | Where-Object { $_ -match '^CELL\|' } | ForEach-Object {
  $p = $_.Split('|'); [pscustomobject]@{ Write=$p[1]; Read=$p[2]; Algo=$p[3]; Proc=$p[4]; Path=$p[5]; Status=$p[6]; Msg=$p[7] } }
Write-Host "`n===== COMPAT MATRIX GRID =====" -ForegroundColor Cyan
$grid | Sort-Object Write,Read,Algo,Proc,Path | Format-Table -AutoSize | Out-Host
$fail = @($grid | Where-Object Status -ne 'PASS')
Write-Host ("PASS={0} FAIL={1}" -f (@($grid|? Status -eq 'PASS').Count), $fail.Count)
if ($fail.Count) { Write-Host "DATA BREAK:" -ForegroundColor Red; $fail|Format-Table|Out-Host; exit 1 }

# Exact-tuple enforcement: the grid must contain EXACTLY the cells the A/B toggle produces, so a silently
# dropped (or duplicated) cell fails the run. 'both' adds the Stream-decrypt + equivalence cells; a single
# processor decrypts every MDE doc once (no equivalence). Counts are derived in CompatMatrixContractTests.
$expectedCells = if ($IncludeCurrent) {
  if ($Processor -eq 'both') { 112 } else { 72 }
} else {
  if ($Processor -eq 'both') { 42 } else { 30 }
}
if ($grid.Count -ne $expectedCells) {
  Write-Host ("CELL COUNT BREAK: expected {0} cells for -Processor {1}, got {2}." -f $expectedCells, $Processor, $grid.Count) -ForegroundColor Red
  exit 1
}

# Anti-fake-green control: a plaintext (unencrypted) doc must be REJECTED by the raw assertion.
$tamper = foreach ($n in $nodes) { Run $n.Dll tamper $n.Name }
$tamper = @($tamper) | Where-Object { $_ -match '^TAMPER\|' }
$tamper | Out-Host
if (@($tamper | Where-Object { $_ -notmatch '^TAMPER\|PASS\|' }).Count -or $tamper.Count -lt $nodes.Count) {
  Write-Host "GUARD BREAK: plaintext doc accepted as encrypted." -ForegroundColor Red; exit 1
}
Write-Host "All cross-version cells PASS (no data break); plaintext rejected." -ForegroundColor Green
exit 0
