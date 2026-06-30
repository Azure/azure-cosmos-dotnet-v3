<#
.SYNOPSIS
    Loads KEY=VALUE pairs from a .env file into the current PowerShell session.

.DESCRIPTION
    Reads a .env file (default: ..\.env relative to this script) and sets each
    pair as a process-scope environment variable ($env:KEY = "value") visible to
    child processes (e.g. `dotnet run`) started from the same session.

    Supports:
      * Blank lines and `#` comments
      * Optional surrounding double or single quotes on values
      * `export KEY=VALUE` prefix (bash style) — ignored

    Does NOT support variable interpolation (e.g. ${OTHER_VAR}) — keep it simple.

.PARAMETER Path
    Path to the .env file. Defaults to ..\.env relative to this script's folder.

.EXAMPLE
    PS> . .\scripts\Load-Env.ps1
    Loaded 6 variable(s) from Q:\...\Microsoft.Azure.Cosmos.NativeDriverBenchmark\.env

.EXAMPLE
    PS> . .\scripts\Load-Env.ps1 -Path C:\secrets\cosmos.env
#>
[CmdletBinding()]
param(
    [string]$Path
)

if (-not $Path) {
    $Path = Join-Path (Split-Path -Parent $PSScriptRoot) '.env'
}

if (-not (Test-Path $Path)) {
    Write-Error "Env file not found: $Path`nCopy .env.example to .env and fill in real values."
    return
}

$count = 0
Get-Content -LiteralPath $Path | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith('#')) { return }

    if ($line -match '^\s*export\s+(.*)$') { $line = $Matches[1] }

    $eq = $line.IndexOf('=')
    if ($eq -lt 1) {
        Write-Warning "Skipping malformed line: $line"
        return
    }

    $key = $line.Substring(0, $eq).Trim()
    $val = $line.Substring($eq + 1).Trim()

    if (($val.StartsWith('"') -and $val.EndsWith('"')) -or
        ($val.StartsWith("'") -and $val.EndsWith("'"))) {
        $val = $val.Substring(1, $val.Length - 2)
    }

    Set-Item -Path "Env:$key" -Value $val
    $count++
}

Write-Host "Loaded $count variable(s) from $Path" -ForegroundColor Green
