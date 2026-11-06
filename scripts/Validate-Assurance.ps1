#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Metadata', 'RequiredProof')]
    [string] $Mode = 'Metadata',
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $RecordsDirectory = 'docs/assurance',
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-AssuranceValidation {
    param([string] $Code, [string] $Detail)
    throw [System.IO.InvalidDataException]::new("${Code}: $Detail")
}

function Assert-AssuranceShape {
    param($Value, [string[]] $Required, [string[]] $Optional = @(), [string] $At = '$')
    if ($Value -isnot [System.Collections.IDictionary]) {
        Stop-AssuranceValidation 'invalid-object' $At
    }
    foreach ($key in $Required) {
        if ($key -cnotin $Value.Keys) { Stop-AssuranceValidation 'missing-field' "$At.$key" }
    }
    foreach ($key in $Value.Keys) {
        if ($key -cnotin ($Required + $Optional)) { Stop-AssuranceValidation 'unknown-field' "$At.$key" }
    }
}

function Assert-AssuranceText {
    param($Value, [string] $At)
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value)) {
        Stop-AssuranceValidation 'invalid-text' $At
    }
}

function Assert-AssuranceId {
    param($Value, [string] $At)
    Assert-AssuranceText $Value $At
    if ($Value -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$') {
        Stop-AssuranceValidation 'invalid-identifier' $At
    }
}

function Assert-AssuranceList {
    param($Value, [string] $At, [switch] $AllowEmpty)
    if ($Value -isnot [array] -or (!$AllowEmpty -and $Value.Count -eq 0)) {
        Stop-AssuranceValidation 'invalid-list' $At
    }
}

function Assert-AssuranceTextList {
    param($Value, [string] $At, [switch] $AllowEmpty)
    Assert-AssuranceList $Value $At -AllowEmpty:$AllowEmpty
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $Value) {
        Assert-AssuranceText $item $At
        if (!$seen.Add($item)) { Stop-AssuranceValidation 'duplicate-value' $At }
    }
}

function Assert-AssuranceEnum {
    param($Value, [string[]] $Allowed, [string] $At)
    Assert-AssuranceText $Value $At
    if ($Value -cnotin $Allowed) { Stop-AssuranceValidation 'unknown-enum' $At }
}

function Assert-AssuranceHash {
    param($Value, [string] $At, [int] $Length = 64)
    if ($Value -isnot [string] -or $Value -cnotmatch "^[0-9a-f]{$Length}$") {
        Stop-AssuranceValidation 'invalid-digest' $At
    }
}

function Assert-AssuranceInteger {
    param($Value, [string] $At, [switch] $Positive)
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt 0 -or ($Positive -and $Value -eq 0)) {
        Stop-AssuranceValidation 'invalid-integer' $At
    }
}

function Assert-AssuranceJsonKeys {
    param([System.Text.Json.JsonElement] $Element)
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $Element.EnumerateObject()) {
            if (!$seen.Add($property.Name)) { Stop-AssuranceValidation 'duplicate-json-key' 'Object keys must be unambiguous.' }
            Assert-AssuranceJsonKeys $property.Value
        }
    } elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($item in $Element.EnumerateArray()) { Assert-AssuranceJsonKeys $item }
    }
}

function ConvertFrom-AssuranceJsonElement {
    param([System.Text.Json.JsonElement] $Element)
    switch ($Element.ValueKind) {
        Object {
            $value = [ordered]@{}
            foreach ($property in $Element.EnumerateObject()) {
                $value[$property.Name] = ConvertFrom-AssuranceJsonElement $property.Value
            }
            return $value
        }
        Array {
            $value = [System.Collections.Generic.List[object]]::new()
            foreach ($item in $Element.EnumerateArray()) { $value.Add((ConvertFrom-AssuranceJsonElement $item)) }
            return ,$value.ToArray()
        }
        String { return $Element.GetString() }
        Number {
            $integer = 0L
            if ($Element.TryGetInt64([ref] $integer)) { return $integer }
            return $Element.GetDouble()
        }
        True { return $true }
        False { return $false }
        Null { return $null }
        default { Stop-AssuranceValidation 'invalid-json-value' 'Unsupported JSON value.' }
    }
}

function ConvertFrom-AssuranceJson {
    param([string] $Text)
    try { $document = [System.Text.Json.JsonDocument]::Parse($Text) }
    catch { Stop-AssuranceValidation 'invalid-json' 'JSON parsing failed.' }
    try {
        Assert-AssuranceJsonKeys $document.RootElement
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            Stop-AssuranceValidation 'invalid-object' 'A record must be an object.'
        }
        return ConvertFrom-AssuranceJsonElement $document.RootElement
    } finally { $document.Dispose() }
}

function Get-AssurancePath {
    param([string] $Root, $RelativePath, [switch] $Directory)
    Assert-AssuranceText $RelativePath 'path'
    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains('\') -or
        $RelativePath.Contains(':') -or @($RelativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        Stop-AssuranceValidation 'unsafe-path' 'Use a nontraversing repository-relative slash path.'
    }
    $current = [IO.Path]::GetFullPath($Root)
    if (![IO.Directory]::Exists($current)) { Stop-AssuranceValidation 'missing-root' 'Repository root must exist.' }
    if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        Stop-AssuranceValidation 'unsafe-path' 'Repository root cannot be a symlink/reparse point.'
    }
    foreach ($part in $RelativePath.Split('/')) {
        $current = [IO.Path]::Combine($current, $part)
        try { $attributes = [IO.File]::GetAttributes($current) }
        catch [IO.FileNotFoundException] { Stop-AssuranceValidation 'missing-path' $RelativePath }
        catch [IO.DirectoryNotFoundException] { Stop-AssuranceValidation 'missing-path' $RelativePath }
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Stop-AssuranceValidation 'unsafe-path' 'Symlink/reparse traversal is not supported.'
        }
    }
    if ($Directory -ne [IO.Directory]::Exists($current)) { Stop-AssuranceValidation 'wrong-path-kind' $RelativePath }
    return $current
}

function Get-AssuranceDigest {
    param([string] $Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function ConvertTo-AssuranceCanonicalValue {
    param($Value)
    if ($Value -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        [string[]] $keys = @($Value.Keys)
        [Array]::Sort($keys, [StringComparer]::Ordinal)
        foreach ($key in $keys) { $ordered[$key] = ConvertTo-AssuranceCanonicalValue $Value[$key] }
        return $ordered
    }
    if ($Value -is [array]) {
        $items = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $Value) { $items.Add((ConvertTo-AssuranceCanonicalValue $item)) }
        return ,$items.ToArray()
    }
    return $Value
}

function Get-AssuranceValueDigest {
    param($Value)
    $canonical = ConvertTo-AssuranceCanonicalValue $Value
    return Get-AssuranceDigest (ConvertTo-Json -InputObject $canonical -Compress -Depth 100)
}

function Get-AssuranceCohortDigest {
    param($Cases)
    Assert-AssuranceList $Cases 'cases'
    $byId = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($case in $Cases) { $byId.Add($case.id, $case) }
    [string[]] $ids = @($byId.Keys)
    [Array]::Sort($ids, [StringComparer]::Ordinal)
    $ordered = @(
        foreach ($id in $ids) {
            $case = $byId[$id]
            [ordered]@{ id = $case.id; arguments = $case.arguments; nativeName = $case.nativeName }
        }
    )
    return Get-AssuranceDigest (ConvertTo-Json -InputObject $ordered -Compress -Depth 100)
}

function Assert-AssuranceReference {
    param($Value, [string] $Root, [string] $At, [int] $UnknownCount)
    Assert-AssuranceShape $Value @('repository', 'revision', 'path', 'sha256') -At $At
    Assert-AssuranceText $Value.repository "$At.repository"
    Assert-AssuranceHash $Value.sha256 "$At.sha256"
    Assert-AssuranceText $Value.path "$At.path"
    if ($Value.repository -ceq 'self') {
        if ($null -eq $Value.revision) {
            if ($UnknownCount -eq 0) { Stop-AssuranceValidation 'undisclosed-unknown' "$At.revision" }
        } else { Assert-AssuranceHash $Value.revision "$At.revision" 40 }
        $path = Get-AssurancePath $Root $Value.path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Value.sha256) {
            Stop-AssuranceValidation 'stale-reference' "$At ($($Value.path))"
        }
    } else {
        if ($Value.repository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
            Stop-AssuranceValidation 'invalid-repository' $At
        }
        Assert-AssuranceHash $Value.revision "$At.revision" 40
        if ($Value.path -match '(^/|/$|\\|:|(^|/)\.\.?(/|$)|//)') { Stop-AssuranceValidation 'unsafe-path' $At }
    }
}

function Get-AssuranceIndex {
    param($Items, [string] $At)
    Assert-AssuranceList $Items $At
    $index = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($item in $Items) {
        if ($item -isnot [System.Collections.IDictionary] -or 'id' -cnotin $item.Keys) {
            Stop-AssuranceValidation 'missing-field' "$At.id"
        }
        Assert-AssuranceId $item.id "$At.id"
        if (!$index.TryAdd($item.id, $item)) { Stop-AssuranceValidation 'duplicate-id' $At }
    }
    return $index
}

function Assert-AssuranceLinks {
    param($Ids, $Index, [string] $At)
    Assert-AssuranceTextList $Ids $At
    foreach ($id in $Ids) {
        if (!$Index.ContainsKey($id)) { Stop-AssuranceValidation 'unresolved-link' $At }
    }
}

function Assert-AssuranceCases {
    param($Cases, [string] $At, [int] $UnknownCount)
    $index = Get-AssuranceIndex $Cases $At
    $tuples = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($case in $Cases) {
        Assert-AssuranceShape $case @('id', 'arguments', 'nativeName') -At $At
        Assert-AssuranceList $case.arguments "$At.arguments" -AllowEmpty
        foreach ($argument in $case.arguments) {
            if ($null -ne $argument -and $argument -isnot [string] -and $argument -isnot [bool] -and
                $argument -isnot [int] -and $argument -isnot [long]) {
                Stop-AssuranceValidation 'unsupported-argument-type' $At
            }
        }
        $tuple = ConvertTo-Json -InputObject $case.arguments -Compress -Depth 100
        if (!$tuples.Add($tuple)) { Stop-AssuranceValidation 'duplicate-case-tuple' $At }
        if ($null -eq $case.nativeName) {
            if ($UnknownCount -eq 0) { Stop-AssuranceValidation 'undisclosed-unknown' "$At.nativeName" }
        } else {
            Assert-AssuranceText $case.nativeName "$At.nativeName"
            if (!$names.Add($case.nativeName)) { Stop-AssuranceValidation 'ambiguous-case-name' $At }
        }
    }
    return $index
}

function Assert-AssuranceRecord {
    param($Record, [string] $Root, [switch] $AllowSynthetic)
    if ($Record -is [System.Collections.IDictionary] -and 'kind' -cin $Record.Keys -and
        $Record.kind -cin @('run-index', 'audit-index', 'native-receipt')) {
        Stop-AssuranceValidation 'unadmitted-record-kind' 'Producer/index admission is outside M-01.1-M-01.4.'
    }
    $required = @('formatVersion', 'kind', 'id', 'recordState', 'policyRef', 'provenance', 'unknowns',
        'specRef', 'preImplementationReceipt', 'impact', 'obligations', 'assertions', 'requiredCells',
        'expectedCohorts', 'peerComparison', 'decisions')
    if ($Record -is [System.Collections.IDictionary] -and 'kind' -cin $Record.Keys -and $Record.kind -ceq 'historical-example') {
        $required += 'execution'
    }
    Assert-AssuranceShape $Record $required @('supersedes')
    Assert-AssuranceInteger $Record.formatVersion 'formatVersion' -Positive
    if ($Record.formatVersion -ne 1) { Stop-AssuranceValidation 'unknown-version' 'formatVersion' }
    Assert-AssuranceEnum $Record.kind @('change', 'historical-example') 'kind'
    Assert-AssuranceId $Record.id 'id'
    Assert-AssuranceEnum $Record.recordState @('proposed', 'active', 'superseded') 'recordState'
    if ($Record.kind -ceq 'historical-example' -and $Record.recordState -ceq 'active') {
        Stop-AssuranceValidation 'historical-not-active' 'A label cannot promote historical proof.'
    }
    Assert-AssuranceShape $Record.policyRef @('repository', 'revision', 'path', 'version') -At 'policyRef'
    Assert-AssuranceText $Record.policyRef.repository 'policyRef.repository'
    Assert-AssuranceHash $Record.policyRef.revision 'policyRef.revision' 40
    Assert-AssuranceText $Record.policyRef.path 'policyRef.path'
    Assert-AssuranceText $Record.policyRef.version 'policyRef.version'
    if ($Record.policyRef.repository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        Stop-AssuranceValidation 'invalid-repository' 'policyRef'
    }
    if ($Record.policyRef.path -match '(^/|/$|\\|:|(^|/)\.\.?(/|$)|//)') {
        Stop-AssuranceValidation 'unsafe-path' 'policyRef.path'
    }
    Assert-AssuranceList $Record.unknowns 'unknowns' -AllowEmpty
    if ($Record.unknowns.Count -gt 0) {
        $null = Get-AssuranceIndex $Record.unknowns 'unknowns'
        foreach ($unknown in $Record.unknowns) {
            Assert-AssuranceShape $unknown @('id', 'reason', 'proofImpact') -At 'unknowns'
            Assert-AssuranceText $unknown.reason 'unknowns.reason'
            Assert-AssuranceText $unknown.proofImpact 'unknowns.proofImpact'
        }
    }
    Assert-AssuranceShape $Record.provenance @('sourceType', 'verification', 'references', 'limits') -At 'provenance'
    Assert-AssuranceEnum $Record.provenance.sourceType @('candidate-authored', 'published-documentation', 'synthetic-fixture') 'provenance.sourceType'
    if (!$AllowSynthetic -and $Record.provenance.sourceType -ceq 'synthetic-fixture') {
        Stop-AssuranceValidation 'synthetic-not-proof' 'Synthetic records are not public evidence records.'
    }
    Assert-AssuranceEnum $Record.provenance.verification @('declared', 'inspected', 'native-reported') 'provenance.verification'
    Assert-AssuranceList $Record.provenance.references 'provenance.references'
    Assert-AssuranceTextList $Record.provenance.limits 'provenance.limits'
    $references = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($reference in $Record.provenance.references) {
        if (!$references.Add((Get-AssuranceValueDigest $reference))) {
            Stop-AssuranceValidation 'duplicate-reference' 'provenance.references'
        }
    }
    Assert-AssuranceReference $Record.specRef $Root 'specRef' $Record.unknowns.Count
    for ($index = 0; $index -lt $Record.provenance.references.Count; $index++) {
        Assert-AssuranceReference $Record.provenance.references[$index] $Root "provenance.references[$index]" $Record.unknowns.Count
    }
    if ('supersedes' -cin $Record.Keys) {
        Assert-AssuranceShape $Record.supersedes @('id', 'sha256', 'reason') -At 'supersedes'
        Assert-AssuranceText $Record.supersedes.id 'supersedes.id'
        Assert-AssuranceHash $Record.supersedes.sha256 'supersedes.sha256'
        Assert-AssuranceText $Record.supersedes.reason 'supersedes.reason'
        if ($Record.supersedes.id -ceq $Record.id) { Stop-AssuranceValidation 'self-supersession' 'Preserve the prior record.' }
    }
    $receipt = $Record.preImplementationReceipt
    Assert-AssuranceShape $receipt @('kind', 'sha256', 'reference', 'attribution', 'verification') @('approvalLabel', 'approvalQuestion', 'approvalTimestamp') 'preImplementationReceipt'
    Assert-AssuranceHash $receipt.sha256 'preImplementationReceipt.sha256'
    foreach ($field in @('kind', 'reference', 'attribution', 'verification')) { Assert-AssuranceText $receipt[$field] "preImplementationReceipt.$field" }
    foreach ($field in @('approvalLabel', 'approvalQuestion', 'approvalTimestamp')) {
        if ($field -cin $receipt.Keys) {
            if ($null -eq $receipt[$field]) {
                if ($Record.unknowns.Count -eq 0) { Stop-AssuranceValidation 'undisclosed-unknown' "preImplementationReceipt.$field" }
            } else { Assert-AssuranceText $receipt[$field] "preImplementationReceipt.$field" }
        }
    }
    Assert-AssuranceShape $Record.impact @('paths', 'obligationIds', 'gapIds', 'preservation', 'intentionalChanges') -At 'impact'
    foreach ($field in @('paths', 'gapIds', 'preservation')) { Assert-AssuranceTextList $Record.impact[$field] "impact.$field" }
    Assert-AssuranceTextList $Record.impact.intentionalChanges 'impact.intentionalChanges' -AllowEmpty
    foreach ($gap in $Record.impact.gapIds) {
        if ($gap -cnotmatch '^G-(0[1-9]|1[0-2])$') { Stop-AssuranceValidation 'unknown-gap' 'impact.gapIds' }
    }
    if ($Record.kind -ceq 'change') {
        foreach ($path in $Record.impact.paths) { $null = Get-AssurancePath $Root $path }
    }
    $obligations = Get-AssuranceIndex $Record.obligations 'obligations'
    $scenarios = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($obligation in $Record.obligations) {
        Assert-AssuranceShape $obligation @('id', 'version', 'lifecycle', 'preconditions', 'allowed', 'forbidden', 'interactions', 'scenarios', 'authority') -At 'obligations'
        Assert-AssuranceText $obligation.version 'obligations.version'
        Assert-AssuranceEnum $obligation.lifecycle @('supported', 'preview', 'deprecated', 'removed', 'unsupported', 'unresolved') 'obligations.lifecycle'
        foreach ($field in @('preconditions', 'allowed', 'forbidden', 'interactions')) { Assert-AssuranceTextList $obligation[$field] "obligations.$field" }
        Assert-AssuranceShape $obligation.authority @('status', 'reference') -At 'obligations.authority'
        Assert-AssuranceEnum $obligation.authority.status @('unresolved', 'independently-approved') 'obligations.authority.status'
        if ($obligation.authority.status -ceq 'independently-approved') {
            Assert-AssuranceText $obligation.authority.reference 'obligations.authority.reference'
        } elseif ($null -ne $obligation.authority.reference) {
            Stop-AssuranceValidation 'inconsistent-authority' 'Unresolved authority must not imply approval.'
        }
        Assert-AssuranceList $obligation.scenarios 'obligations.scenarios'
        foreach ($scenario in $obligation.scenarios) {
            Assert-AssuranceShape $scenario @('id', 'kind', 'expectation') -At 'scenarios'
            Assert-AssuranceId $scenario.id 'scenarios.id'
            Assert-AssuranceEnum $scenario.kind @('positive', 'negative', 'boundary') 'scenarios.kind'
            Assert-AssuranceText $scenario.expectation 'scenarios.expectation'
            if (!$scenarios.TryAdd($scenario.id, $obligation.id)) { Stop-AssuranceValidation 'duplicate-id' 'scenarios' }
        }
    }
    Assert-AssuranceLinks $Record.impact.obligationIds $obligations 'impact.obligationIds'
    $assertions = Get-AssuranceIndex $Record.assertions 'assertions'
    foreach ($assertion in $Record.assertions) {
        $at = "assertions[$($assertion.id)]"
        Assert-AssuranceShape $assertion @('id', 'obligationIds', 'scenarioIds', 'project', 'sourceRef', 'symbol', 'observes', 'limits') -At $at
        Assert-AssuranceLinks $assertion.obligationIds $obligations "$at.obligationIds"
        Assert-AssuranceLinks $assertion.scenarioIds $scenarios "$at.scenarioIds"
        foreach ($scenarioId in $assertion.scenarioIds) {
            if ($scenarios[$scenarioId] -cnotin $assertion.obligationIds) { Stop-AssuranceValidation 'scenario-obligation-mismatch' $at }
        }
        foreach ($field in @('project', 'symbol', 'observes')) { Assert-AssuranceText $assertion[$field] "$at.$field" }
        Assert-AssuranceTextList $assertion.limits "$at.limits"
        Assert-AssuranceReference $assertion.sourceRef $Root "$at.sourceRef" $Record.unknowns.Count
        if ($assertion.sourceRef.repository -ceq 'self') {
            $null = Get-AssurancePath $Root $assertion.project
            if ($assertion.sourceRef.path.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase)) {
                $tokens = $null
                $parseErrors = $null
                $ast = [Management.Automation.Language.Parser]::ParseFile((Get-AssurancePath $Root $assertion.sourceRef.path), [ref] $tokens, [ref] $parseErrors)
                if ($parseErrors.Count -gt 0) { Stop-AssuranceValidation 'invalid-assertion-source' $at }
                $matches = @($ast.FindAll({
                    param($node)
                    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $assertion.symbol
                }, $true))
                if ($matches.Count -ne 1) { Stop-AssuranceValidation 'missing-assertion-symbol' $assertion.id }
            }
        }
    }
    $cells = Get-AssuranceIndex $Record.requiredCells 'requiredCells'
    $allCases = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($cell in $Record.requiredCells) {
        Assert-AssuranceShape $cell @('id', 'obligationIds', 'scenarioIds', 'assertionIds', 'layer', 'variant', 'tfm', 'binding', 'cases') -At 'requiredCells'
        Assert-AssuranceLinks $cell.obligationIds $obligations 'requiredCells.obligationIds'
        Assert-AssuranceLinks $cell.scenarioIds $scenarios 'requiredCells.scenarioIds'
        Assert-AssuranceLinks $cell.assertionIds $assertions 'requiredCells.assertionIds'
        foreach ($obligationId in $cell.obligationIds) {
            if (@($cell.assertionIds | Where-Object { $obligationId -cin $assertions[$_].obligationIds }).Count -eq 0) {
                Stop-AssuranceValidation 'unmapped-cell-obligation' $cell.id
            }
        }
        foreach ($scenarioId in $cell.scenarioIds) {
            if ($scenarios[$scenarioId] -cnotin $cell.obligationIds -or
                @($cell.assertionIds | Where-Object { $scenarioId -cin $assertions[$_].scenarioIds }).Count -eq 0) {
                Stop-AssuranceValidation 'scenario-obligation-mismatch' $cell.id
            }
        }
        Assert-AssuranceEnum $cell.layer @('unit', 'emulator', 'authorized-isolated-live') 'requiredCells.layer'
        foreach ($field in @('variant', 'tfm', 'binding')) { Assert-AssuranceText $cell[$field] "requiredCells.$field" }
        $null = Assert-AssuranceCases $cell.cases 'requiredCells.cases' $Record.unknowns.Count
        foreach ($case in $cell.cases) {
            if (!$allCases.TryAdd($case.id, $cell)) { Stop-AssuranceValidation 'duplicate-id' 'requiredCells.cases' }
        }
    }
    Assert-AssuranceList $Record.expectedCohorts 'expectedCohorts'
    $cohorts = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($cohort in $Record.expectedCohorts) {
        Assert-AssuranceShape $cohort @('cellId', 'caseIds', 'sha256', 'origin') -At 'expectedCohorts'
        Assert-AssuranceText $cohort.cellId 'expectedCohorts.cellId'
        if (!$cells.ContainsKey($cohort.cellId)) { Stop-AssuranceValidation 'unresolved-link' 'expectedCohorts.cellId' }
        if (!$cohorts.Add($cohort.cellId)) { Stop-AssuranceValidation 'duplicate-id' 'expectedCohorts.cellId' }
        Assert-AssuranceTextList $cohort.caseIds 'expectedCohorts.caseIds'
        Assert-AssuranceText $cohort.origin 'expectedCohorts.origin'
        Assert-AssuranceHash $cohort.sha256 'expectedCohorts.sha256'
        $caseIds = @($cells[$cohort.cellId].cases | ForEach-Object { $_.id })
        if ($cohort.caseIds.Count -ne $caseIds.Count -or @($cohort.caseIds | Where-Object { $_ -cnotin $caseIds }).Count -gt 0) {
            Stop-AssuranceValidation 'incomplete-cohort' 'Expected cases must exactly match the declared cell.'
        }
        if ((Get-AssuranceCohortDigest $cells[$cohort.cellId].cases) -cne $cohort.sha256) {
            Stop-AssuranceValidation 'stale-cohort' $cohort.cellId
        }
    }
    if ($cohorts.Count -ne $cells.Count) { Stop-AssuranceValidation 'missing-cohort' 'Every cell needs an explicit expected cohort.' }
    foreach ($obligationId in $obligations.Keys) {
        if (@($Record.assertions | Where-Object { $obligationId -cin $_.obligationIds }).Count -eq 0 -or
            @($Record.requiredCells | Where-Object { $obligationId -cin $_.obligationIds }).Count -eq 0) {
            Stop-AssuranceValidation 'unmapped-obligation' $obligationId
        }
    }
    Assert-AssuranceList $Record.peerComparison 'peerComparison'
    $peers = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($peer in $Record.peerComparison) {
        Assert-AssuranceShape $peer @('sdk', 'disposition', 'reason', 'proofImpact') -At 'peerComparison'
        Assert-AssuranceEnum $peer.sdk @('.NET', 'Java', 'Python', 'Go', 'Rust', 'JS/TS') 'peerComparison.sdk'
        Assert-AssuranceEnum $peer.disposition @('unverified', 'inspected', 'not-assessed') 'peerComparison.disposition'
        Assert-AssuranceText $peer.reason 'peerComparison.reason'
        Assert-AssuranceText $peer.proofImpact 'peerComparison.proofImpact'
        if (!$peers.Add($peer.sdk)) { Stop-AssuranceValidation 'duplicate-id' 'peerComparison.sdk' }
    }
    if ($peers.Count -ne 6) { Stop-AssuranceValidation 'missing-peer-cell' 'Keep all six comparison cells visible.' }
    Assert-AssuranceTextList $Record.decisions 'decisions' -AllowEmpty
    if ($Record.kind -ceq 'historical-example') {
        $execution = $Record.execution
        Assert-AssuranceShape $execution @('source', 'base', 'mergeCandidate', 'binding', 'discovery', 'assessment', 'runs') -At 'execution'
        foreach ($field in @('source', 'base', 'mergeCandidate')) { Assert-AssuranceHash $execution[$field] "execution.$field" 40 }
        foreach ($field in @('binding', 'discovery', 'assessment')) { Assert-AssuranceText $execution[$field] "execution.$field" }
        foreach ($assertion in $Record.assertions) {
            if ($assertion.sourceRef.revision -cnotin @($execution.source, $execution.mergeCandidate)) {
                Stop-AssuranceValidation 'historical-source-mismatch' 'A later documentation head is not the executed assertion source.'
            }
        }
        $null = Get-AssuranceIndex $execution.runs 'execution.runs'
        $coveredCells = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($run in $execution.runs) {
            Assert-AssuranceShape $run @('id', 'layer', 'variant', 'taskAttempt', 'requiredCaseIds', 'trxSha256',
                'reportedTerminal', 'reportedParent', 'reportedRawTotal', 'passedTerminal', 'ignoredTerminal', 'locator') -At 'execution.runs'
            Assert-AssuranceEnum $run.layer @('unit', 'emulator', 'authorized-isolated-live') 'execution.runs.layer'
            Assert-AssuranceText $run.variant 'execution.runs.variant'
            Assert-AssuranceInteger $run.taskAttempt 'execution.runs.taskAttempt' -Positive
            Assert-AssuranceTextList $run.requiredCaseIds 'execution.runs.requiredCaseIds'
            foreach ($id in $run.requiredCaseIds) {
                if (!$allCases.ContainsKey($id)) { Stop-AssuranceValidation 'unresolved-link' 'execution.runs.requiredCaseIds' }
                $cell = $allCases[$id]
                if ($cell.layer -cne $run.layer -or $cell.variant -cne $run.variant) {
                    Stop-AssuranceValidation 'historical-binding-mismatch' 'Do not borrow another layer/variant.'
                }
                foreach ($requiredCase in $cell.cases) {
                    if ($requiredCase.id -cnotin $run.requiredCaseIds) {
                        Stop-AssuranceValidation 'incomplete-run-cohort' 'Retain the complete declared cell.'
                    }
                }
                $null = $coveredCells.Add($cell.id)
            }
            Assert-AssuranceHash $run.trxSha256 'execution.runs.trxSha256'
            Assert-AssuranceText $run.locator 'execution.runs.locator'
            foreach ($field in @('reportedTerminal', 'reportedParent', 'reportedRawTotal', 'passedTerminal', 'ignoredTerminal')) {
                if ($null -eq $run[$field]) {
                    if ($Record.unknowns.Count -eq 0) { Stop-AssuranceValidation 'undisclosed-unknown' "execution.runs.$field" }
                } else { Assert-AssuranceInteger $run[$field] "execution.runs.$field" }
            }
        }
        if ($coveredCells.Count -ne $cells.Count) { Stop-AssuranceValidation 'incomplete-run-cohort' 'A historical cell has no attributed run.' }
    }
}

function Assert-AssurancePreservation {
    param($Base, $Candidate)
    if ($null -eq $Candidate) { Stop-AssuranceValidation 'required-record-missing' 'A previous record cannot disappear.' }
    if ($Base.id -cne $Candidate.id) { Stop-AssuranceValidation 'record-identity-changed' 'Reconcile renames independently.' }
    foreach ($field in @('formatVersion', 'kind', 'recordState', 'policyRef', 'specRef',
        'preImplementationReceipt', 'impact', 'peerComparison', 'decisions', 'provenance', 'unknowns')) {
        if ((Get-AssuranceValueDigest $Base[$field]) -cne (Get-AssuranceValueDigest $Candidate[$field])) {
            Stop-AssuranceValidation 'protected-input-changed' "Reconcile previous $field independently."
        }
    }
    foreach ($field in @('obligations', 'assertions', 'requiredCells')) {
        $candidateIndex = Get-AssuranceIndex $Candidate[$field] $field
        foreach ($item in $Base[$field]) {
            if (!$candidateIndex.ContainsKey($item.id) -or
                (Get-AssuranceValueDigest $item) -cne (Get-AssuranceValueDigest $candidateIndex[$item.id])) {
                Stop-AssuranceValidation 'protected-input-changed' "Retain/reconcile previous $field."
            }
        }
    }
    foreach ($cohort in $Base.expectedCohorts) {
        $matches = @($Candidate.expectedCohorts | Where-Object { $_.cellId -ceq $cohort.cellId })
        if ($matches.Count -ne 1 -or (Get-AssuranceValueDigest $matches[0]) -cne (Get-AssuranceValueDigest $cohort)) {
            Stop-AssuranceValidation 'protected-input-changed' 'Expected cohorts cannot weaken their own denominator.'
        }
    }
}

function Read-AssuranceTrx {
    param([string] $Path, [string] $ExpectedSha256)
    Assert-AssuranceHash $ExpectedSha256 'trxSha256'
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ExpectedSha256) {
        Stop-AssuranceValidation 'wrong-trx-artifact' 'The named original must match its expected digest.'
    }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $xml = [Xml.XmlDocument]::new()
        $xml.XmlResolver = $null
        try { $xml.Load($reader) }
        catch [Xml.XmlException] { Stop-AssuranceValidation 'invalid-trx' 'XML/DTD parsing failed.' }
    } finally { $reader.Dispose() }
    $namespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    if ($null -eq $xml.DocumentElement -or $xml.DocumentElement.LocalName -cne 'TestRun' -or
        $xml.DocumentElement.NamespaceURI -cne $namespace) { Stop-AssuranceValidation 'unsupported-trx-shape' 'Expected VSTest 2010 TestRun.' }
    $manager = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $manager.AddNamespace('t', $namespace)
    foreach ($section in @('/t:TestRun/t:Results', '/t:TestRun/t:TestDefinitions',
        '/t:TestRun/t:ResultSummary', '/t:TestRun/t:ResultSummary/t:Counters')) {
        if ($xml.SelectNodes($section, $manager).Count -ne 1) {
            Stop-AssuranceValidation 'unsupported-trx-shape' 'Critical native sections must be unique and present.'
        }
    }
    $resultRoot = $xml.SelectSingleNode('/t:TestRun/t:Results', $manager)
    foreach ($container in $resultRoot.SelectNodes('. | .//*[local-name()="InnerResults"]')) {
        if ($container.NamespaceURI -cne $namespace -or
            (![object]::ReferenceEquals($container, $resultRoot) -and
                ($container.ParentNode.LocalName -cne 'UnitTestResult' -or $container.ParentNode.NamespaceURI -cne $namespace))) {
            Stop-AssuranceValidation 'unsupported-trx-shape' 'Unexpected InnerResults placement or namespace.'
        }
        if ($container.SelectNodes('text()[normalize-space(.) != ""]').Count -gt 0) {
            Stop-AssuranceValidation 'unsupported-trx-shape' 'Result containers cannot contain text.'
        }
        foreach ($child in $container.SelectNodes('*')) {
            if ($child.LocalName -cne 'UnitTestResult' -or $child.NamespaceURI -cne $namespace) {
                Stop-AssuranceValidation 'unsupported-trx-shape' 'Only VSTest 2010 UnitTestResult elements are supported in result containers.'
            }
        }
    }
    $definitions = @{}
    foreach ($definition in $xml.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager)) {
        $id = $definition.GetAttribute('id')
        $parsed = [Guid]::Empty
        if (![Guid]::TryParseExact($id, 'D', [ref] $parsed) -or $definitions.ContainsKey($id)) {
            Stop-AssuranceValidation 'ambiguous-trx-definition' 'Definition IDs must be unique GUIDs.'
        }
        $method = $definition.SelectSingleNode('t:TestMethod', $manager)
        if ($null -eq $method) { Stop-AssuranceValidation 'unsupported-trx-shape' 'Missing method definition.' }
        Assert-AssuranceText $method.GetAttribute('className') 'TRX.className'
        Assert-AssuranceText $method.GetAttribute('name') 'TRX.method'
        $definitions[$id] = "$($method.GetAttribute('className')).$($method.GetAttribute('name'))"
    }
    $nodes = $xml.SelectNodes('/t:TestRun/t:Results//t:UnitTestResult', $manager)
    if ($nodes.Count -eq 0) { Stop-AssuranceValidation 'empty-trx' 'No result nodes.' }
    $executionIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $results = @(
        foreach ($node in $nodes) {
            if ($node.ParentNode.NamespaceURI -cne $namespace -or
                ($node.ParentNode.LocalName -ceq 'Results' -and ![object]::ReferenceEquals($node.ParentNode, $resultRoot)) -or
                $node.ParentNode.LocalName -cnotin @('Results', 'InnerResults')) {
                Stop-AssuranceValidation 'unsupported-trx-shape' 'Unexpected result placement.'
            }
            if ($node.SelectNodes('t:InnerResults', $manager).Count -gt 1) {
                Stop-AssuranceValidation 'unsupported-trx-shape' 'InnerResults must be unique within a unit result.'
            }
            $id = $node.GetAttribute('executionId')
            $parsed = [Guid]::Empty
            if (![Guid]::TryParseExact($id, 'D', [ref] $parsed) -or !$executionIds.Add($id)) {
                Stop-AssuranceValidation 'duplicate-trx-execution' 'Execution IDs must be unique GUIDs.'
            }
            $testId = $node.GetAttribute('testId')
            if (!$definitions.ContainsKey($testId)) { Stop-AssuranceValidation 'unresolved-trx-definition' 'Missing native definition.' }
            $name = $node.GetAttribute('testName')
            Assert-AssuranceText $name 'TRX.testName'
            $outcome = $node.GetAttribute('outcome')
            Assert-AssuranceEnum $outcome @('Passed', 'Failed', 'Error', 'Timeout', 'Aborted', 'Inconclusive',
                'NotExecuted', 'NotRunnable', 'Pending', 'Disconnected', 'Warning', 'Completed', 'InProgress') 'TRX.outcome'
            $children = $node.SelectNodes('t:InnerResults/t:UnitTestResult', $manager)
            if (($null -ne $node.SelectSingleNode('t:InnerResults', $manager) -or
                $node.GetAttribute('resultType') -ceq 'DataDrivenTest') -and $children.Count -eq 0) {
                Stop-AssuranceValidation 'unsupported-trx-shape' 'An empty data-driven parent is not a terminal leaf.'
            }
            $parentId = $null
            if ($node.ParentNode.LocalName -ceq 'InnerResults') {
                if ($node.ParentNode.ParentNode.LocalName -cne 'UnitTestResult') {
                    Stop-AssuranceValidation 'unsupported-trx-shape' 'Missing containing parent result.'
                }
                $parentId = $node.ParentNode.ParentNode.GetAttribute('executionId')
            }
            if ($node.HasAttribute('parentExecutionId') -and $node.GetAttribute('parentExecutionId') -cne $parentId) {
                Stop-AssuranceValidation 'wrong-trx-parent' 'Parent link conflicts with native topology.'
            }
            [pscustomobject]@{
                executionId = $id; testId = $testId; nativeName = $name; method = $definitions[$testId]
                outcome = $outcome; parentExecutionId = $parentId; terminal = ($children.Count -eq 0)
            }
        }
    )
    $summary = $xml.SelectSingleNode('/t:TestRun/t:ResultSummary', $manager)
    $counterNode = $xml.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $manager)
    if ($null -eq $summary -or $null -eq $counterNode) { Stop-AssuranceValidation 'unsupported-trx-shape' 'Missing original summary/counters.' }
    $counters = [ordered]@{}
    foreach ($attribute in $counterNode.Attributes) {
        $value = 0L
        if (![long]::TryParse($attribute.Value, [ref] $value) -or $value -lt 0) {
            Stop-AssuranceValidation 'invalid-trx-counter' 'Original counters must be nonnegative integers.'
        }
        $counters[$attribute.Name] = $value
    }
    foreach ($field in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
        if (!$counters.Contains($field)) { Stop-AssuranceValidation 'unsupported-trx-shape' 'Missing original counter.' }
    }
    return [pscustomobject]@{ results = $results; counters = $counters; outcome = $summary.GetAttribute('outcome'); sha256 = $ExpectedSha256 }
}

function Assert-AssuranceSyntheticContext {
    param($Context)
    Assert-AssuranceShape $Context @('synthetic', 'candidate', 'inputs', 'binding', 'fixture', 'native', 'cases', 'trxSha256') -At 'context'
    if ($Context.synthetic -isnot [bool] -or !$Context.synthetic) {
        Stop-AssuranceValidation 'unadmitted-producer' 'This helper accepts synthetic test inputs only.'
    }
    $shapes = [ordered]@{
        candidate = @('repository', 'source', 'base', 'merge', 'tree')
        inputs = @('evaluator', 'policy', 'spec', 'cohort')
        binding = @('core', 'package', 'artifact', 'tfm', 'configuration', 'surface', 'runtime')
        fixture = @('identity', 'msi', 'profile')
        native = @('provider', 'build', 'job', 'task', 'jobAttempt', 'taskAttempt', 'result', 'previousAttempts')
    }
    foreach ($section in $shapes.Keys) {
        Assert-AssuranceShape $Context[$section] $shapes[$section] -At "context.$section"
        foreach ($key in $shapes[$section]) {
            if ($key -cnotin @('jobAttempt', 'taskAttempt', 'previousAttempts')) {
                Assert-AssuranceText $Context[$section][$key] "context.$section.$key"
            }
        }
    }
    foreach ($field in @('source', 'base', 'merge', 'tree')) { Assert-AssuranceHash $Context.candidate[$field] "candidate.$field" 40 }
    foreach ($field in @('evaluator', 'policy')) { Assert-AssuranceHash $Context.inputs[$field] "inputs.$field" 40 }
    foreach ($field in @('spec', 'cohort')) { Assert-AssuranceHash $Context.inputs[$field] "inputs.$field" }
    Assert-AssuranceHash $Context.binding.artifact 'binding.artifact'
    Assert-AssuranceHash $Context.fixture.msi 'fixture.msi'
    Assert-AssuranceHash $Context.trxSha256 'trxSha256'
    Assert-AssuranceEnum $Context.binding.core @('current-core-source', 'published-reference', 'produced-nupkg', 'installed-consumer') 'binding.core'
    Assert-AssuranceInteger $Context.native.jobAttempt 'native.jobAttempt' -Positive
    Assert-AssuranceInteger $Context.native.taskAttempt 'native.taskAttempt' -Positive
    Assert-AssuranceList $Context.native.previousAttempts 'native.previousAttempts' -AllowEmpty
    $null = Assert-AssuranceCases $Context.cases 'context.cases' 0
    if ((Get-AssuranceCohortDigest $Context.cases) -cne $Context.inputs.cohort) {
        Stop-AssuranceValidation 'stale-cohort' 'context.inputs.cohort'
    }
}

function Assert-AssuranceSyntheticEvidence {
    param($Expected, $Observed, $Trx)
    Assert-AssuranceSyntheticContext $Expected
    Assert-AssuranceSyntheticContext $Observed
    foreach ($section in @('candidate', 'inputs', 'binding', 'fixture', 'cases')) {
        if ((Get-AssuranceValueDigest $Expected[$section]) -cne (Get-AssuranceValueDigest $Observed[$section])) {
            Stop-AssuranceValidation 'evidence-identity-mismatch' $section
        }
    }
    if ($Expected.trxSha256 -cne $Observed.trxSha256 -or $Trx.sha256 -cne $Expected.trxSha256) {
        Stop-AssuranceValidation 'wrong-trx-artifact' 'Do not borrow another report.'
    }
    foreach ($field in @('provider', 'build', 'job', 'task', 'jobAttempt', 'taskAttempt')) {
        if ($Expected.native[$field] -cne $Observed.native[$field]) { Stop-AssuranceValidation 'evidence-identity-mismatch' "native.$field" }
    }
    if ((Get-AssuranceValueDigest $Expected.native.previousAttempts) -cne (Get-AssuranceValueDigest $Observed.native.previousAttempts)) {
        Stop-AssuranceValidation 'evidence-identity-mismatch' 'native.previousAttempts'
    }
    if ($Observed.native.jobAttempt -ne 1) {
        Stop-AssuranceValidation 'unreconciled-job-history' 'Job retry admission/collection is pending.'
    }
    $prior = $Observed.native.previousAttempts
    if ($prior.Count -ne ($Observed.native.taskAttempt - 1)) {
        Stop-AssuranceValidation 'missing-attempt-history' 'Preserve every task attempt.'
    }
    $attempts = [System.Collections.Generic.HashSet[long]]::new()
    foreach ($attempt in $prior) {
        Assert-AssuranceShape $attempt @('taskAttempt', 'result', 'trxSha256') -At 'previousAttempts'
        Assert-AssuranceInteger $attempt.taskAttempt 'previousAttempts.taskAttempt' -Positive
        Assert-AssuranceHash $attempt.trxSha256 'previousAttempts.trxSha256'
        Assert-AssuranceText $attempt.result 'previousAttempts.result'
        if ($attempt.taskAttempt -ge $Observed.native.taskAttempt -or !$attempts.Add($attempt.taskAttempt)) {
            Stop-AssuranceValidation 'missing-attempt-history' 'Prior attempts must be unique and precede this attempt.'
        }
        if ($attempt.result -cne 'Succeeded') {
            Stop-AssuranceValidation 'retained-attempt-failure' 'Original failure is retained; retry admissibility is not selected.'
        }
    }
    if ($Observed.native.result -cne 'Succeeded') { Stop-AssuranceValidation 'failed-native-task' 'Partial passing leaves do not override the native task.' }
    if ($Trx.outcome -cnotin @('Completed', 'Passed')) { Stop-AssuranceValidation 'failed-trx-summary' 'The native parent did not succeed.' }
    $parents = @($Trx.results | Where-Object { !$_.terminal })
    if (@($parents | Where-Object { $_.outcome -cne 'Passed' }).Count -gt 0) {
        Stop-AssuranceValidation 'failed-trx-parent' 'Parent failure is retained.'
    }
    $leaves = @($Trx.results | Where-Object { $_.terminal })
    foreach ($case in $Expected.cases) {
        $matches = @($leaves | Where-Object { $_.nativeName -ceq $case.nativeName })
        if ($matches.Count -eq 0) { Stop-AssuranceValidation 'missing-required-leaf' $case.id }
        if ($matches.Count -ne 1) { Stop-AssuranceValidation 'ambiguous-required-leaf' $case.id }
        $method = $case.nativeName.Split('(')[0].TrimEnd()
        if ($matches[0].method -cne $method) { Stop-AssuranceValidation 'unresolved-trx-definition' $case.id }
        if ($matches[0].outcome -cne 'Passed') { Stop-AssuranceValidation 'nonpassing-required-leaf' $case.id }
    }
    return [pscustomobject]@{
        synthetic = $true; requiredCases = $Expected.cases.Count; terminalCount = $leaves.Count
        parentCount = $parents.Count; originalCounters = $Trx.counters
        retainedNonpassing = @($leaves | Where-Object { $_.outcome -cne 'Passed' })
        qualification = 'not-assessed'
    }
}

function Invoke-AssuranceValidation {
    param([string] $Root, [string] $Records, [string] $RequestedMode, [string] $SummaryPath)
    $summary = [ordered]@{
        formatVersion = 1; mode = $RequestedMode; metadata = 'invalid'; requiredProof = 'not-assessed'
        qualification = 'not-assessed'; provenance = 'candidate-local-untrusted'; records = @(); errors = @()
        pendingControls = @('protected-entry-and-evaluator', 'server-derived-diff-and-policy',
            'native-original-artifact-and-attempts', 'runtime-and-service-fixture',
            'independent-oracles-and-role-judgments', 'operating-profile-admission')
        runtime = [ordered]@{ powershell = $PSVersionTable.PSVersion.ToString(); dotnet = [Environment]::Version.ToString(); os = [Runtime.InteropServices.RuntimeInformation]::OSDescription }
    }
    $exitCode = 1
    $recordContext = $null
    try {
        if (![string]::IsNullOrEmpty($SummaryPath)) {
            $output = [IO.Path]::GetFullPath($SummaryPath)
            $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            if ($output.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                Stop-AssuranceValidation 'unsafe-output-path' 'Write summaries outside the checkout.'
            }
        }
        $directory = Get-AssurancePath $Root $Records -Directory
        $entries = @(Get-ChildItem -LiteralPath $directory -Recurse -Force)
        if (@($entries | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) {
            Stop-AssuranceValidation 'unsafe-path' 'Record directories cannot contain symlinks/reparse points.'
        }
        $files = @($entries | Where-Object { !$_.PSIsContainer -and $_.Extension -ieq '.json' } | Sort-Object FullName)
        if ($files.Count -eq 0) { Stop-AssuranceValidation 'missing-records' 'An empty directory is not validated evidence.' }
        $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($file in $files) {
            $relativePath = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($Root), $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, [char] '/')
            $recordContext = 'record=' + (ConvertTo-Json -InputObject $relativePath -Compress)
            $record = ConvertFrom-AssuranceJson ([IO.File]::ReadAllText($file.FullName, [Text.UTF8Encoding]::new($false, $true)))
            Assert-AssuranceRecord $record $Root
            if (!$ids.Add($record.id)) { Stop-AssuranceValidation 'duplicate-id' 'Record IDs must be unique across the directory.' }
            $summary.records += [ordered]@{
                id = $record.id; kind = $record.kind; state = $record.recordState
                sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                unknowns = $record.unknowns
            }
        }
        $recordContext = $null
        $summary.metadata = 'valid'
        if ($RequestedMode -ceq 'RequiredProof') {
            $summary.requiredProof = 'blocked'
            Stop-AssuranceValidation 'unadmitted-required-proof' 'No protected producer/evaluator is admitted; metadata cannot substitute.'
        }
        $exitCode = 0
    } catch {
        $message = $_.Exception.Message
        if ($null -ne $recordContext) { $message += " [$recordContext]" }
        $summary.errors += $message
        if ($RequestedMode -ceq 'RequiredProof') { $summary.requiredProof = 'blocked' }
    }
    $json = ConvertTo-Json -InputObject $summary -Depth 100
    if (![string]::IsNullOrEmpty($SummaryPath) -and @($summary.errors | Where-Object { $_ -like 'unsafe-output-path:*' }).Count -eq 0) {
        [IO.File]::WriteAllText([IO.Path]::GetFullPath($SummaryPath), $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    }
    Write-Output $json.Replace('##', '#\u0023')
    if ($exitCode -ne 0) {
        foreach ($failure in $summary.errors) {
            $safe = $failure.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A').Replace(']', '%5D').Replace(';', '%3B')
            if ($env:TF_BUILD -ceq 'True') { Write-Host "##vso[task.logissue type=error]$safe" }
            else { [Console]::Error.WriteLine($safe) }
        }
    }
    return $exitCode
}

if ($MyInvocation.InvocationName -ne '.') {
    $result = @(Invoke-AssuranceValidation $RepositoryRoot $RecordsDirectory $Mode $OutputPath)
    $code = $result[-1]
    $result | Select-Object -SkipLast 1 | Write-Output
    exit $code
}
