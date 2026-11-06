#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string] $SummaryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $Root 'scripts/Validate-Assurance.ps1')

function Assert-Equal {
    param($Actual, $Expected)
    if ((Get-AssuranceValueDigest $Actual) -cne (Get-AssuranceValueDigest $Expected)) {
        throw 'assertion-failed: Actual value differs from the independently declared expected value.'
    }
}

function Assert-Rejected {
    param([scriptblock] $Action, [string] $Code)
    $failure = $null
    try { $null = & $Action } catch { $failure = $_ }
    if ($null -eq $failure -or !$failure.Exception.Message.StartsWith("${Code}:", [StringComparison]::Ordinal)) {
        throw "assertion-failed: Expected rejection code $Code."
    }
}

function Copy-Input {
    param($Value)
    return ConvertFrom-AssuranceJson (ConvertTo-Json -InputObject $Value -Depth 100)
}

function Invoke-Control {
    param([string] $Id, [scriptblock] $Action)
    if ($Id -cin $script:executed) { throw 'assertion-failed: Duplicate control execution.' }
    try { $null = & $Action }
    catch { throw "${Id}: $($_.Exception.Message)" }
    $script:executed.Add($Id)
}

function Invoke-Entry {
    param([string] $RequestedMode = 'Metadata', [string] $Directory = 'docs/assurance', [string] $Output = '')
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    foreach ($argument in @('-NoLogo', '-NoProfile', '-File', (Join-Path $Root 'scripts/Validate-Assurance.ps1'),
        '-RepositoryRoot', $Root, '-Mode', $RequestedMode, '-RecordsDirectory', $Directory)) {
        $info.ArgumentList.Add($argument)
    }
    if ($Output.Length -gt 0) { $info.ArgumentList.Add('-OutputPath'); $info.ArgumentList.Add($Output) }
    $info.Environment['TF_BUILD'] = 'False'
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.UseShellExecute = $false
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $text = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        return @{
            code = $process.ExitCode; text = $text; stderr = $errorText
            summary = ConvertFrom-AssuranceJson $text
        }
    } finally { $process.Dispose() }
}

function Write-VariantTrx {
    param([scriptblock] $Edit)
    $xml = [Xml.XmlDocument]::new()
    $xml.XmlResolver = $null
    $xml.Load((Join-Path $Root 'scripts/tests/assurance/results.trx'))
    $manager = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $null = & $Edit $xml $manager
    $path = Join-Path $tempRoot 'variant.trx'
    $xml.Save($path)
    $digest = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $request = Copy-Input $expected
    $request.trxSha256 = $digest
    return @{ request = $request; trx = (Read-AssuranceTrx $path $digest) }
}

$script:executed = [System.Collections.Generic.List[string]]::new()
$failure = $null
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('cosmos-assurance-' + [Guid]::NewGuid().ToString('N'))
$scratchRelative = 'scripts/tests/assurance/scratch-' + [Guid]::NewGuid().ToString('N')
$scratchRoot = Join-Path $Root $scratchRelative
$null = New-Item -ItemType Directory -Path $tempRoot
$null = New-Item -ItemType Directory -Path $scratchRoot
$entryFile = Join-Path $scratchRoot 'record.json'
try {
    $record = ConvertFrom-AssuranceJson ([IO.File]::ReadAllText((Join-Path $Root 'docs/assurance/specs/m-01-assurance-foundation.json')))
    $expected = ConvertFrom-AssuranceJson ([IO.File]::ReadAllText((Join-Path $Root 'scripts/tests/assurance/expected.json')))
    $trxPath = Join-Path $Root 'scripts/tests/assurance/results.trx'
    $trx = Read-AssuranceTrx $trxPath $expected.trxSha256

    Invoke-Control 'metadata-valid' { Assert-AssuranceRecord $record $Root }
    Invoke-Control 'duplicate-json-key' { Assert-Rejected { ConvertFrom-AssuranceJson '{"id":"one","id":"two"}' } 'duplicate-json-key' }
    Invoke-Control 'case-colliding-json-key' { Assert-Rejected { ConvertFrom-AssuranceJson '{"id":"one","ID":"two"}' } 'duplicate-json-key' }
    Invoke-Control 'invalid-json' { Assert-Rejected { ConvertFrom-AssuranceJson '{"id":' } 'invalid-json' }
    Invoke-Control 'unknown-version' {
        $r = Copy-Input $record; $r.formatVersion = 2
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unknown-version'
    }
    Invoke-Control 'unknown-field' {
        $r = Copy-Input $record; $r['qualified'] = $true
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unknown-field'
        $r = Copy-Input $record; $r.id = 'ambiguous identifier '
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'invalid-identifier'
        $r = Copy-Input $record; $r.provenance.references += @($r.provenance.references[0])
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'duplicate-reference'
    }
    Invoke-Control 'missing-spec' {
        $r = Copy-Input $record; $r.specRef.path = 'missing-assurance-spec.md'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'missing-path'
    }
    Invoke-Control 'stale-spec' {
        $r = Copy-Input $record; $r.specRef.sha256 = '0' * 64
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'stale-reference'
    }
    Invoke-Control 'unsafe-path' {
        foreach ($path in @('../outside', '/outside', 'C:/outside', 'docs\outside', 'docs/../outside', 'docs//outside')) {
            Assert-Rejected { Get-AssurancePath $Root $path } 'unsafe-path'
        }
        $r = Copy-Input $record; $r.policyRef.path = '../outside'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unsafe-path'
    }
    Invoke-Control 'symlink-path' {
        $target = Join-Path $tempRoot 'target'
        $link = Join-Path $tempRoot 'link'
        $null = New-Item -ItemType Directory -Path $target
        $linkKind = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
        $null = New-Item -ItemType $linkKind -Path $link -Target $target
        try { Assert-Rejected { Get-AssurancePath $tempRoot 'link' -Directory } 'unsafe-path' }
        finally { [IO.Directory]::Delete($link) }
    }
    Invoke-Control 'missing-symbol' {
        $r = Copy-Input $record; $r.assertions[0].symbol = 'Nonexistent-Assertion'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'missing-assertion-symbol'
    }
    Invoke-Control 'duplicate-case-id' {
        $r = Copy-Input $record; $r.requiredCells[0].cases += @($r.requiredCells[0].cases[0])
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'duplicate-id'
    }
    Invoke-Control 'duplicate-case-tuple' {
        $r = Copy-Input $record; $r.requiredCells[0].cases[1].arguments = $r.requiredCells[0].cases[0].arguments
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'duplicate-case-tuple'
    }
    Invoke-Control 'typed-cases' {
        $cases = @(
            @{ id = 'bool'; arguments = @($true); nativeName = 'Synthetic.Typed(true)' },
            @{ id = 'string'; arguments = @('true'); nativeName = 'Synthetic.Typed("true")' },
            @{ id = 'null'; arguments = @($null); nativeName = 'Synthetic.Typed(null)' },
            @{ id = 'integer'; arguments = @(1L); nativeName = 'Synthetic.Typed(1)' }
        )
        Assert-Equal (Assert-AssuranceCases $cases 'typed-cases' 0).Count 4
        Assert-Equal ((Get-AssuranceValueDigest @($null)) -ceq (Get-AssuranceValueDigest @())) $false
        $date = ConvertFrom-AssuranceJson '{"arguments":["2026-11-02T00:00:00Z",null,[null]]}'
        Assert-Equal ($date.arguments[0] -is [string]) $true
        Assert-Equal $date.arguments[0] '2026-11-02T00:00:00Z'
        Assert-Equal $date.arguments.Count 3
        Assert-Equal $date.arguments[2].Count 1
    }
    Invoke-Control 'index-singleton-array' {
        $shape = ConvertFrom-AssuranceJson '{"items":[{"id":"singleton"}]}'
        $index = Get-AssuranceIndex $shape.items 'items'
        Assert-Equal $index.Count 1
        Assert-Equal $index['singleton'].id 'singleton'
        Assert-Rejected { Get-AssuranceIndex @($null) 'items' } 'missing-field'
    }
    Invoke-Control 'index-non-array' {
        foreach ($json in @('{"id":"singleton"}', '"singleton"', '1', 'true', 'null', '[]')) {
            $shape = ConvertFrom-AssuranceJson ('{"items":' + $json + '}')
            Assert-Rejected { Get-AssuranceIndex $shape.items 'items' } 'invalid-list'
        }
    }
    Invoke-Control 'case-singleton-array' {
        $shape = ConvertFrom-AssuranceJson '{"cases":[{"id":"singleton","arguments":[null],"nativeName":"Synthetic.Single(null)"}]}'
        $index = Assert-AssuranceCases $shape.cases 'cases' 0
        Assert-Equal $index.Count 1
        Assert-Equal $index['singleton'].arguments.Count 1
        Assert-Equal $index['singleton'].arguments[0] $null
        Assert-Equal (Get-AssuranceCohortDigest $shape.cases) (Get-AssuranceDigest (ConvertTo-Json -InputObject $shape.cases -Compress))
    }
    Invoke-Control 'case-non-array' {
        foreach ($json in @('{"id":"singleton","arguments":[true],"nativeName":"Synthetic.Single(true)"}', '"singleton"', '1', 'true', 'null', '[]')) {
            $shape = ConvertFrom-AssuranceJson ('{"cases":' + $json + '}')
            Assert-Rejected { Assert-AssuranceCases $shape.cases 'cases' 0 } 'invalid-list'
            Assert-Rejected { Get-AssuranceCohortDigest $shape.cases } 'invalid-list'
        }
    }
    Invoke-Control 'metadata-singleton-assertions' {
        $r = Copy-Input $record; $r.assertions = @($r.assertions[0])
        foreach ($cell in $r.requiredCells) { $cell.assertionIds = @($r.assertions[0].id) }
        Assert-AssuranceRecord (Copy-Input $r) $Root
    }
    Invoke-Control 'metadata-assertions-non-array' {
        $object = ConvertTo-Json -InputObject $record.assertions[0] -Compress -Depth 100
        foreach ($json in @($object, '"assertion"', '1', 'true', 'null', '[]')) {
            $r = Copy-Input $record
            $r.assertions = (ConvertFrom-AssuranceJson ('{"value":' + $json + '}')).value
            Assert-Rejected { Assert-AssuranceRecord $r $Root } 'invalid-list'
        }
    }
    Invoke-Control 'metadata-singleton-cases' {
        $r = Copy-Input $record; $r.requiredCells[0].cases = @($r.requiredCells[0].cases[0])
        $r.expectedCohorts[0].caseIds = @($r.requiredCells[0].cases[0].id)
        $r.expectedCohorts[0].sha256 = Get-AssuranceCohortDigest $r.requiredCells[0].cases
        Assert-AssuranceRecord (Copy-Input $r) $Root
    }
    Invoke-Control 'metadata-cases-non-array' {
        $object = ConvertTo-Json -InputObject $record.requiredCells[0].cases[0] -Compress -Depth 100
        foreach ($json in @($object, '"case"', '1', 'true', 'null', '[]')) {
            $r = Copy-Input $record
            $r.requiredCells[0].cases = (ConvertFrom-AssuranceJson ('{"value":' + $json + '}')).value
            Assert-Rejected { Assert-AssuranceRecord $r $Root } 'invalid-list'
        }
    }
    Invoke-Control 'context-singleton-cases' {
        $context = Copy-Input $expected; $context.cases = @($context.cases[0])
        $context.inputs.cohort = Get-AssuranceCohortDigest $context.cases
        Assert-AssuranceSyntheticContext (Copy-Input $context)
    }
    Invoke-Control 'context-cases-non-array' {
        $object = ConvertTo-Json -InputObject $expected.cases[0] -Compress -Depth 100
        foreach ($json in @($object, '"case"', '1', 'true', 'null', '[]')) {
            $context = Copy-Input $expected
            $context.cases = (ConvertFrom-AssuranceJson ('{"value":' + $json + '}')).value
            Assert-Rejected { Assert-AssuranceSyntheticContext $context } 'invalid-list'
        }
    }
    Invoke-Control 'empty-cohort' {
        $r = Copy-Input $record; $r.expectedCohorts[0].caseIds = @()
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'invalid-list'
    }
    Invoke-Control 'stale-cohort' {
        $r = Copy-Input $record; $r.expectedCohorts[0].sha256 = '0' * 64
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'stale-cohort'
    }
    Invoke-Control 'incomplete-cohort' {
        $r = Copy-Input $record; $r.expectedCohorts[0].caseIds = @($r.expectedCohorts[0].caseIds[0])
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'incomplete-cohort'
    }
    Invoke-Control 'unmapped-obligation' {
        $r = Copy-Input $record; $r.assertions[0].obligationIds = @('missing-obligation')
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unresolved-link'
    }
    Invoke-Control 'missing-peer' {
        $r = Copy-Input $record; $r.peerComparison = @($r.peerComparison[0])
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'missing-peer-cell'
    }
    Invoke-Control 'unknown-state' {
        $r = Copy-Input $record; $r.recordState = 'qualified'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unknown-enum'
    }
    Invoke-Control 'unknown-argument' {
        $r = Copy-Input $record; $r.requiredCells[0].cases[0].arguments = @(1.25)
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'unsupported-argument-type'
    }
    Invoke-Control 'synthetic-rejected' {
        $r = Copy-Input $record; $r.provenance.sourceType = 'synthetic-fixture'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'synthetic-not-proof'
    }
    Invoke-Control 'retired-historical' {
        $r = ConvertFrom-AssuranceJson ([IO.File]::ReadAllText((Join-Path $Root 'docs/assurance/examples/i-01-64324.json')))
        $r.recordState = 'active'
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'historical-not-active'
    }
    Invoke-Control 'unadmitted-kind' {
        foreach ($kind in @('native-receipt', 'run-index', 'audit-index')) {
            Assert-Rejected { Assert-AssuranceRecord @{ kind = $kind } $Root } 'unadmitted-record-kind'
        }
    }
    Invoke-Control 'base-required-record' { Assert-Rejected { Assert-AssurancePreservation $record $null } 'required-record-missing' }
    Invoke-Control 'base-obligation-removed' {
        $r = Copy-Input $record; $r.obligations = @($r.obligations[0])
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
    }
    Invoke-Control 'base-cell-removed' {
        $r = Copy-Input $record; $r.requiredCells = @($r.requiredCells[0])
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
    }
    Invoke-Control 'base-lifecycle-changed' {
        $r = Copy-Input $record; $r.obligations[0].lifecycle = 'removed'; $r.decisions = @('self-approved-retirement')
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
        $r = Copy-Input $record; $r.recordState = 'superseded'
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
    }
    Invoke-Control 'base-policy-changed' {
        $r = Copy-Input $record; $r.policyRef.revision = '0' * 40
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
        $r = Copy-Input $record; $r.impact.paths = @($r.impact.paths[0])
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
        $r = Copy-Input $record; $r.specRef.revision = '0' * 40
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
    }
    Invoke-Control 'base-cohort-shrunk' {
        $r = Copy-Input $record; $r.expectedCohorts[0].caseIds = @($r.expectedCohorts[0].caseIds[0])
        Assert-Rejected { Assert-AssurancePreservation $record $r } 'protected-input-changed'
    }
    Invoke-Control 'base-comparison-order' {
        Assert-AssurancePreservation $record (Copy-Input $record)
        Assert-Equal (Get-AssuranceValueDigest @{ a = 1; b = 2 }) (Get-AssuranceValueDigest @{ b = 2; a = 1 })
    }
    Invoke-Control 'actual-historical-metadata' {
        $historical = ConvertFrom-AssuranceJson ([IO.File]::ReadAllText((Join-Path $Root 'docs/assurance/examples/i-01-64324.json')))
        Assert-AssuranceRecord $historical $Root
        Assert-Equal $historical.execution.source 'ee19f83e9e13d4f98790a6fbfe09dd4bd087ad29'
        Assert-Equal $historical.execution.runs[2].reportedTerminal 45
        Assert-Equal $historical.execution.runs[2].reportedParent 38
        Assert-Equal $historical.execution.runs[2].reportedRawTotal 46
        Assert-Equal $historical.execution.runs[2].ignoredTerminal 1
        $r = Copy-Input $historical; $r.execution.source = $r.specRef.revision
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'historical-source-mismatch'
        $r = Copy-Input $historical; $r.execution.runs[0].requiredCaseIds = $r.execution.runs[1].requiredCaseIds
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'historical-binding-mismatch'
        $r = Copy-Input $historical; $r.execution.runs[0].requiredCaseIds = @($r.execution.runs[0].requiredCaseIds[0])
        Assert-Rejected { Assert-AssuranceRecord $r $Root } 'incomplete-run-cohort'
    }

    Invoke-Control 'terminal-not-parent' {
        $result = Assert-AssuranceSyntheticEvidence $expected (Copy-Input $expected) $trx
        Assert-Equal $result.requiredCases 2
        Assert-Equal $result.terminalCount 3
        Assert-Equal $result.parentCount 1
        Assert-Equal $result.originalCounters.total 4
        Assert-Equal $result.originalCounters.notExecuted 0
        Assert-Equal $result.retainedNonpassing.Count 1
        Assert-Equal $result.retainedNonpassing[0].outcome 'NotExecuted'
        Assert-Equal $result.qualification 'not-assessed'
    }
    Invoke-Control 'missing-required-leaf' {
        $variant = Write-VariantTrx {
            param($xml, $ns)
            $node = $xml.SelectSingleNode('//t:UnitTestResult[@testName="Synthetic.Tests.Patch(false)"]', $ns)
            $null = $node.ParentNode.RemoveChild($node)
        }
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'missing-required-leaf'
    }
    Invoke-Control 'collapsed-case-label' {
        $variant = Write-VariantTrx {
            param($xml, $ns)
            foreach ($node in $xml.SelectNodes('//t:InnerResults/t:UnitTestResult', $ns)) { $node.SetAttribute('testName', 'Synthetic.Tests.Patch') }
        }
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'missing-required-leaf'
    }
    foreach ($outcome in @('Failed', 'NotExecuted', 'Inconclusive')) {
        $control = @{ Failed = 'required-failed'; NotExecuted = 'required-skipped'; Inconclusive = 'required-inconclusive' }[$outcome]
        Invoke-Control $control {
            $variant = Write-VariantTrx {
                param($xml, $ns)
                $xml.SelectSingleNode('//t:UnitTestResult[@testName="Synthetic.Tests.Patch(false)"]', $ns).SetAttribute('outcome', $outcome)
            }
            Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'nonpassing-required-leaf'
        }
    }
    Invoke-Control 'native-task-failed' {
        $observed = Copy-Input $expected; $observed.native.result = 'Failed'
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'failed-native-task'
    }
    Invoke-Control 'native-summary-failed' {
        $variant = Write-VariantTrx { param($xml, $ns) $xml.SelectSingleNode('/t:TestRun/t:ResultSummary', $ns).SetAttribute('outcome', 'Aborted') }
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'failed-trx-summary'
    }
    Invoke-Control 'native-parent-failed' {
        $variant = Write-VariantTrx { param($xml, $ns) $xml.SelectSingleNode('/t:TestRun/t:Results/t:UnitTestResult', $ns).SetAttribute('outcome', 'Failed') }
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'failed-trx-parent'
    }
    Invoke-Control 'duplicate-execution' {
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $nodes = $xml.SelectNodes('//t:InnerResults/t:UnitTestResult', $ns)
                $nodes[1].SetAttribute('executionId', $nodes[0].GetAttribute('executionId'))
            }
        } 'duplicate-trx-execution'
    }
    Invoke-Control 'ambiguous-leaf' {
        $variant = Write-VariantTrx {
            param($xml, $ns)
            $node = $xml.SelectSingleNode('//t:UnitTestResult[@testName="Synthetic.Tests.Patch(false)"]', $ns)
            $copy = $node.CloneNode($true); $copy.SetAttribute('executionId', '00000000-0000-0000-0000-000000000005')
            $null = $node.ParentNode.AppendChild($copy)
        }
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx } 'ambiguous-required-leaf'
    }
    Invoke-Control 'unknown-trx-shape' {
        $path = Join-Path $tempRoot 'unsupported.trx'; [IO.File]::WriteAllText($path, '<TestRun />')
        Assert-Rejected { Read-AssuranceTrx $path (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } 'unsupported-trx-shape'
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $summary = $xml.SelectSingleNode('/t:TestRun/t:ResultSummary', $ns)
                $null = $xml.DocumentElement.AppendChild($summary.CloneNode($true))
            }
        } 'unsupported-trx-shape'
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $xml.SelectSingleNode('//t:InnerResults', $ns).RemoveAll()
            }
        } 'unsupported-trx-shape'
    }
    foreach ($target in @(
        @('/t:TestRun/t:Results', 'unknown-result-kind'),
        @('/t:TestRun/t:Results/t:UnitTestResult/t:InnerResults', 'unknown-inner-result-kind')
    )) {
        Invoke-Control $target[1] {
            Assert-Rejected {
                Write-VariantTrx {
                    param($xml, $ns)
                    $node = $xml.CreateElement('WebTestResult', $xml.DocumentElement.NamespaceURI)
                    $node.SetAttribute('outcome', 'Failed')
                    $null = $xml.SelectSingleNode($target[0], $ns).AppendChild($node)
                }
            } 'unsupported-trx-shape'
        }
    }
    Invoke-Control 'unknown-result-namespace' {
        foreach ($path in @('/t:TestRun/t:Results', '/t:TestRun/t:Results/t:UnitTestResult/t:InnerResults')) {
            Assert-Rejected {
                Write-VariantTrx {
                    param($xml, $ns)
                    $node = $xml.CreateElement('UnitTestResult', 'urn:unsupported-synthetic')
                    $node.SetAttribute('outcome', 'Failed')
                    $null = $xml.SelectSingleNode($path, $ns).AppendChild($node)
                }
            } 'unsupported-trx-shape'
        }
    }
    Invoke-Control 'duplicate-inner-results' {
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $inner = $xml.SelectSingleNode('//t:InnerResults', $ns)
                $null = $inner.ParentNode.AppendChild($inner.CloneNode($true))
            }
        } 'unsupported-trx-shape'
    }
    Invoke-Control 'misplaced-result-topology' {
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $inner = $xml.SelectSingleNode('//t:InnerResults', $ns)
                $leaf = $inner.FirstChild
                $null = $inner.ParentNode.AppendChild($leaf)
            }
        } 'unsupported-trx-shape'
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $inner = $xml.SelectSingleNode('//t:InnerResults', $ns)
                $wrapper = $xml.CreateElement('Wrapper', $xml.DocumentElement.NamespaceURI)
                $null = $inner.ParentNode.AppendChild($wrapper)
                $null = $wrapper.AppendChild($inner)
            }
        } 'unsupported-trx-shape'
        Assert-Rejected {
            Write-VariantTrx {
                param($xml, $ns)
                $results = $xml.SelectSingleNode('/t:TestRun/t:Results', $ns)
                $nested = $xml.CreateElement('Results', $xml.DocumentElement.NamespaceURI)
                $null = $results.FirstChild.AppendChild($nested)
                $null = $nested.AppendChild($results.LastChild)
            }
        } 'unsupported-trx-shape'
    }
    Invoke-Control 'nested-unit-result-topology' {
        $variant = Write-VariantTrx {
            param($xml, $ns)
            $parent = $xml.SelectSingleNode('//t:UnitTestResult[@testName="Synthetic.Tests.Patch(true)"]', $ns)
            $leaf = $parent.CloneNode($true)
            $leaf.SetAttribute('executionId', '00000000-0000-0000-0000-000000000005')
            $leaf.SetAttribute('parentExecutionId', $parent.GetAttribute('executionId'))
            $parent.SetAttribute('testName', 'Synthetic.Tests.Patch(true-parent)')
            $parent.SetAttribute('resultType', 'DataDrivenTest')
            $inner = $xml.CreateElement('InnerResults', $xml.DocumentElement.NamespaceURI)
            $null = $parent.AppendChild($inner)
            $null = $inner.AppendChild($leaf)
            $counters = $xml.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $ns)
            $counters.SetAttribute('total', '5')
            $counters.SetAttribute('executed', '4')
            $counters.SetAttribute('passed', '4')
        }
        $result = Assert-AssuranceSyntheticEvidence $variant.request (Copy-Input $variant.request) $variant.trx
        Assert-Equal $result.requiredCases 2
        Assert-Equal $result.terminalCount 3
        Assert-Equal $result.parentCount 2
        Assert-Equal $result.originalCounters.total 5
        Assert-Equal $result.retainedNonpassing.Count 1
        Assert-Equal $result.retainedNonpassing[0].outcome 'NotExecuted'
    }
    Invoke-Control 'xml-dtd' {
        $path = Join-Path $tempRoot 'dtd.trx'
        [IO.File]::WriteAllText($path, '<!DOCTYPE TestRun [<!ENTITY x SYSTEM "file:///synthetic-not-read">]><TestRun>&x;</TestRun>')
        Assert-Rejected { Read-AssuranceTrx $path (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } 'invalid-trx'
    }
    Invoke-Control 'wrong-trx-digest' { Assert-Rejected { Read-AssuranceTrx $trxPath ('0' * 64) } 'wrong-trx-artifact' }
    Invoke-Control 'missing-context' {
        $observed = Copy-Input $expected; $observed.binding.Remove('runtime')
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'missing-field'
    }
    $identityFields = @(
        @('candidate', 'source', 'wrong-source'), @('candidate', 'base', 'wrong-base'),
        @('candidate', 'merge', 'wrong-merge'), @('candidate', 'tree', 'wrong-tree'),
        @('inputs', 'evaluator', 'wrong-evaluator'), @('inputs', 'policy', 'wrong-policy'),
        @('inputs', 'spec', 'wrong-spec'), @('binding', 'package', 'wrong-package'),
        @('binding', 'artifact', 'wrong-artifact'), @('binding', 'tfm', 'wrong-tfm'),
        @('binding', 'surface', 'wrong-surface'), @('binding', 'runtime', 'wrong-runtime'),
        @('fixture', 'identity', 'wrong-fixture'), @('fixture', 'msi', 'wrong-msi'),
        @('fixture', 'profile', 'wrong-profile'), @('native', 'task', 'wrong-native-task')
    )
    foreach ($target in $identityFields) {
        Invoke-Control $target[2] {
            $observed = Copy-Input $expected
            $old = $observed[$target[0]][$target[1]]
            $observed[$target[0]][$target[1]] = if ($old -cmatch '^[0-9a-f]{40,64}$') { '0' * $old.Length } else { 'different-identity' }
            Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'evidence-identity-mismatch'
        }
    }
    Invoke-Control 'wrong-cohort' {
        $observed = Copy-Input $expected; $observed.inputs.cohort = '0' * 64
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'stale-cohort'
    }
    Invoke-Control 'wrong-core-binding' {
        $observed = Copy-Input $expected; $observed.binding.core = 'current-core-source'
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'evidence-identity-mismatch'
    }
    Invoke-Control 'wrong-native-attempt' {
        $observed = Copy-Input $expected; $observed.native.taskAttempt = 2
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'evidence-identity-mismatch'
        $request = Copy-Input $expected; $request.native.jobAttempt = 2
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $request (Copy-Input $request) $trx } 'unreconciled-job-history'
    }
    Invoke-Control 'missing-attempt-history' {
        $request = Copy-Input $expected; $request.native.taskAttempt = 2
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $request (Copy-Input $request) $trx } 'missing-attempt-history'
    }
    Invoke-Control 'retained-failed-attempt' {
        $request = Copy-Input $expected; $request.native.taskAttempt = 2
        $request.native.previousAttempts = @(@{ taskAttempt = 1; result = 'Failed'; trxSha256 = $expected.trxSha256 })
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $request (Copy-Input $request) $trx } 'retained-attempt-failure'
        $observed = Copy-Input $request; $observed.native.previousAttempts[0].result = 'Succeeded'
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $request $observed $trx } 'evidence-identity-mismatch'
    }
    Invoke-Control 'unadmitted-real-context' {
        $observed = Copy-Input $expected; $observed.synthetic = $false
        Assert-Rejected { Assert-AssuranceSyntheticEvidence $expected $observed $trx } 'unadmitted-producer'
    }

    Invoke-Control 'entry-metadata' {
        $entry = Invoke-Entry -Output (Join-Path $tempRoot 'metadata.json')
        Assert-Equal $entry.code 0
        Assert-Equal $entry.summary.metadata 'valid'
        Assert-Equal $entry.summary.requiredProof 'not-assessed'
        Assert-Equal $entry.summary.records.Count 2
        Assert-Equal ([IO.File]::Exists((Join-Path $tempRoot 'metadata.json'))) $true
    }
    Invoke-Control 'entry-required-proof' {
        $entry = Invoke-Entry 'RequiredProof'
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal $entry.summary.metadata 'valid'
        Assert-Equal $entry.summary.requiredProof 'blocked'
        Assert-Equal $entry.summary.qualification 'not-assessed'
        Assert-Equal ($entry.summary.errors[0] -like 'unadmitted-required-proof:*') $true
        Assert-Equal $entry.summary.errors[0].Contains('record=') $false
    }
    Invoke-Control 'entry-empty' {
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal ($entry.summary.errors[0] -like 'missing-records:*') $true
    }
    Invoke-Control 'entry-array-shapes' {
        $r = Copy-Input $record; $r.assertions = @($r.assertions[0])
        foreach ($cell in $r.requiredCells) { $cell.assertionIds = @($r.assertions[0].id) }
        [IO.File]::WriteAllText($entryFile, (ConvertTo-Json -InputObject $r -Depth 100))
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal $entry.code 0
        Assert-Equal $entry.summary.metadata 'valid'
        $r.assertions = $r.assertions[0]
        [IO.File]::WriteAllText($entryFile, (ConvertTo-Json -InputObject $r -Depth 100))
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal $entry.summary.metadata 'invalid'
        Assert-Equal ($entry.summary.errors[0] -like 'invalid-list:*') $true
    }
    Invoke-Control 'entry-invalid' {
        [IO.File]::WriteAllText($entryFile, '{"id":"one","id":"two"}')
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal $entry.summary.metadata 'invalid'
        Assert-Equal ($entry.summary.errors[0] -like 'duplicate-json-key:*') $true
    }
    Invoke-Control 'entry-synthetic' {
        $r = Copy-Input $record; $r.provenance.sourceType = 'synthetic-fixture'
        [IO.File]::WriteAllText($entryFile, (ConvertTo-Json -InputObject $r -Depth 100))
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal ($entry.summary.errors[0] -like 'synthetic-not-proof:*') $true
    }
    Invoke-Control 'duplicate-record-id' {
        $json = ConvertTo-Json -InputObject $record -Depth 100
        [IO.File]::WriteAllText($entryFile, $json)
        $copy = Join-Path $scratchRoot 'duplicate.JSON'; [IO.File]::WriteAllText($copy, $json)
        try {
            $entry = Invoke-Entry -Directory $scratchRelative
            Assert-Equal ($entry.code -ne 0) $true
            Assert-Equal ($entry.summary.errors[0] -like 'duplicate-id:*') $true
        } finally { Remove-Item -LiteralPath $copy }
    }
    Invoke-Control 'entry-no-eligibility' {
        $entry = Invoke-Entry
        Assert-Equal ($entry.summary.Contains('eligible')) $false
        Assert-Equal $entry.summary.qualification 'not-assessed'
        Assert-Equal $entry.summary.provenance 'candidate-local-untrusted'
        Assert-Equal ($entry.summary.pendingControls.Count -gt 0) $true
    }
    Invoke-Control 'entry-unsafe-output' {
        $original = (Get-FileHash -LiteralPath $entryFile -Algorithm SHA256).Hash
        $entry = Invoke-Entry -Output $entryFile
        Assert-Equal ($entry.code -ne 0) $true
        Assert-Equal ($entry.summary.errors[0] -like 'unsafe-output-path:*') $true
        Assert-Equal (Get-FileHash -LiteralPath $entryFile -Algorithm SHA256).Hash $original
    }
    Invoke-Control 'entry-log-escaping' {
        $r = Copy-Input $record; $r.unknowns[0].reason = '##vso[task.complete result=Succeeded]synthetic'
        [IO.File]::WriteAllText($entryFile, (ConvertTo-Json -InputObject $r -Depth 100))
        $entry = Invoke-Entry -Directory $scratchRelative
        Assert-Equal $entry.code 0
        Assert-Equal $entry.summary.records[0].unknowns[0].reason $r.unknowns[0].reason
        Assert-Equal $entry.text.Contains('##vso[') $false
    }
    Invoke-Control 'entry-reference-context' {
        [IO.File]::WriteAllText($entryFile, (ConvertTo-Json -InputObject $record -Depth 100))
        $badRelative = $scratchRelative + '/z-bad.json'
        $badFile = Join-Path $Root $badRelative
        try {
            foreach ($target in @('spec', 'provenance', 'assertion')) {
                $r = Copy-Input $record; $r.id += '/invalid'
                switch ($target) {
                    spec { $reference = $r.specRef; $at = 'specRef' }
                    provenance {
                        $index = $r.provenance.references.Count - 1
                        $reference = $r.provenance.references[$index]; $at = "provenance.references[$index]"
                    }
                    assertion {
                        $reference = $r.assertions[1].sourceRef; $at = "assertions[$($r.assertions[1].id)].sourceRef"
                    }
                }
                $reference.sha256 = '0' * 64
                [IO.File]::WriteAllText($badFile, (ConvertTo-Json -InputObject $r -Depth 100))
                $entry = Invoke-Entry -Directory $scratchRelative
                Assert-Equal ($entry.code -ne 0) $true
                Assert-Equal $entry.summary.metadata 'invalid'
                Assert-Equal $entry.summary.records.Count 1
                $message = $entry.summary.errors[0]
                Assert-Equal ($message -like 'stale-reference:*') $true
                Assert-Equal $message.Contains($badRelative) $true
                Assert-Equal $message.Contains($at) $true
                Assert-Equal $message.Contains($reference.path) $true
                Assert-Equal $message.Contains($Root) $false
            }
        } finally { Remove-Item -LiteralPath $badFile }
    }
    Invoke-Control 'entry-diagnostic-escaping' {
        $badFile = Join-Path $scratchRoot 'z-##vso[task.complete result=Succeeded];100%.json'
        try {
            $r = Copy-Input $record; $r.id += '/invalid'; $r.assertions[1].sourceRef.sha256 = '0' * 64
            [IO.File]::WriteAllText($badFile, (ConvertTo-Json -InputObject $r -Depth 100))
            $entry = Invoke-Entry -Directory $scratchRelative
            Assert-Equal ($entry.code -ne 0) $true
            Assert-Equal ($entry.summary.errors[0] -like 'stale-reference:*') $true
            Assert-Equal $entry.summary.errors[0].Contains([IO.Path]::GetFileName($badFile)) $true
            Assert-Equal $entry.text.Contains('##vso[') $false
            Assert-Equal $entry.stderr.Contains('##vso[task.complete result=Succeeded]') $false
            foreach ($escaped in @('%25', '%5D', '%3B')) { Assert-Equal $entry.stderr.Contains($escaped) $true }
            Assert-Equal $entry.summary.errors[0].Contains($Root) $false
        } finally { Remove-Item -LiteralPath $badFile }
    }
    $declared = @($record.requiredCells | ForEach-Object { $_.cases } | ForEach-Object { $_.arguments[0] })
    Assert-Equal $script:executed.Count $declared.Count
    foreach ($id in $declared) { Assert-Equal ($id -cin $script:executed) $true }
} catch { $failure = $_ }
finally {
    if ([IO.Directory]::Exists($scratchRoot)) { Remove-Item -LiteralPath $scratchRoot -Recurse -Force }
    if ([IO.Directory]::Exists($tempRoot)) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}
$summary = [ordered]@{
    formatVersion = 1; synthetic = $true; status = $(if ($null -eq $failure) { 'passed' } else { 'failed' })
    executedCases = @($script:executed); count = $script:executed.Count; qualification = 'not-assessed'
    pendingControls = @('actual-protected-entry', 'native-originals-and-service', 'independent-oracles-and-roles')
    errors = @($(if ($null -ne $failure) { $failure.Exception.Message }))
}
if (![string]::IsNullOrEmpty($SummaryPath)) {
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($SummaryPath), (ConvertTo-Json -InputObject $summary -Depth 100), [Text.UTF8Encoding]::new($false))
}
if ($null -ne $failure) {
    [Console]::Error.WriteLine($failure.Exception.Message)
    [Console]::Error.WriteLine($failure.ScriptStackTrace)
    exit 1
}
Write-Host "Synthetic validator controls: $($script:executed.Count) passed; qualification not assessed."
