#!/bin/env pwsh
#Requires -Version 7

# Defines shared constants used by both Analyze-AOT-Compact.ps1 and Render-AOT-Analysis-Result.ps1

# Calculate the workspace root directory (azure-cosmos-dotnet-v3) from the scripts directory
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$aotSampleRoot = Join-Path $workspaceRoot "AOT-Sample"

$script:AOTConfig = @{
    # Base paths
    RootPath = $aotSampleRoot
    ProjectFile = Join-Path $aotSampleRoot "AOT-Sample.csproj"
    
    # AOT report directories and files 
    ReportDirectory = Join-Path $aotSampleRoot ".work/aotCompactReport"
    RawReportPath = Join-Path $aotSampleRoot ".work/aotCompactReport/aot-compact-report.txt"
    JsonReportPath = Join-Path $aotSampleRoot ".work/aotCompactReport/aot-compact-report.json"
    HtmlReportPath = Join-Path $aotSampleRoot ".work/aotCompactReport/aot-compact-report.html"
}

function Get-AOTConfig {
    return $script:AOTConfig
}