[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $PackagePath,
    [string] $BaselineAssemblyPath,
    [string] $ApiCompatPath
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Microsoft.Azure.Cosmos.Direct.csproj'
$manifest = Get-Content (Join-Path $PSScriptRoot 'source-manifest.json') -Raw | ConvertFrom-Json
$expected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in $manifest.files) {
    $path = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $file.path))
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "Imported source differs from the pinned manifest: $($file.path)"
    }
    if ($file.kind -in @('Compile', 'CompileSharedFile', 'MetadataTemplate', 'EmbeddedResource')) {
        if (!$expected.Add($path)) {
            throw "Duplicate manifest input: $($file.path)"
        }
    }
}

$evaluation = & dotnet msbuild $project -nologo "-p:Configuration=$Configuration" `
    '-getItem:Compile,EmbeddedResource' '-getProperty:TargetPath,QueryPlanInteropNativePath'
if ($LASTEXITCODE -ne 0) {
    throw "Direct project evaluation failed ($LASTEXITCODE)."
}
$evaluated = ($evaluation -join "`n") | ConvertFrom-Json
$actual = @($evaluated.Items.Compile.FullPath) + @($evaluated.Items.EmbeddedResource.FullPath)
if (!$expected.SetEquals([string[]]$actual) -or $actual.Count -ne $expected.Count) {
    $difference = Compare-Object @($expected) $actual | Out-String
    throw "Evaluated Direct source/resource inputs differ from the manifest:`n$difference"
}
Write-Host "Verified $($manifest.files.Count) byte-preserved inputs and $($actual.Count) evaluated source/resource inputs."

if ($BaselineAssemblyPath -or $ApiCompatPath) {
    if (!$BaselineAssemblyPath -or !$ApiCompatPath) {
        throw "Specify both BaselineAssemblyPath and ApiCompatPath for binary compatibility validation."
    }
    $baseline = [System.Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $BaselineAssemblyPath).Path)
    $candidate = [System.Reflection.Assembly]::LoadFile($evaluated.Properties.TargetPath)
    if ($baseline.FullName -ne $candidate.FullName -or
        [System.BitConverter]::ToString($baseline.GetName().GetPublicKey()) -ne
        [System.BitConverter]::ToString($candidate.GetName().GetPublicKey())) {
        throw "Direct assembly identity or signing public key differs from the baseline."
    }
    $friends = foreach ($assembly in @($baseline, $candidate)) {
        ,@($assembly.GetCustomAttributesData() |
            Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.InternalsVisibleToAttribute' } |
            ForEach-Object { $_.ConstructorArguments[0].Value } | Sort-Object)
    }
    if (Compare-Object $friends[0] $friends[1]) {
        throw "Direct friend-assembly grants differ from the baseline."
    }
    if (Compare-Object $baseline.GetManifestResourceNames() $candidate.GetManifestResourceNames()) {
        throw "Direct resource names differ from the baseline."
    }
    & $ApiCompatPath -l $BaselineAssemblyPath -r $evaluated.Properties.TargetPath --respect-internals --strict-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Strict Direct API compatibility failed ($LASTEXITCODE)."
    }
    Write-Host "Verified assembly identity, public key, $($friends[1].Count) friend grants, resources and internal API compatibility."
}

if (!$PackagePath) {
    return
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $payload = @{
        'lib/netstandard2.0/Microsoft.Azure.Cosmos.Direct.dll' = $evaluated.Properties.TargetPath
        'runtimes/win-x64/native/Microsoft.Azure.Cosmos.ServiceInterop.dll' = $evaluated.Properties.QueryPlanInteropNativePath
        'ThirdPartyNotices/Microsoft.Azure.Cosmos.Direct.txt' = Join-Path $PSScriptRoot 'ThirdPartyNotice.txt'
    }
    foreach ($name in $payload.Keys) {
        $entries = @($archive.Entries | Where-Object FullName -EQ $name)
        if ($entries.Count -ne 1) {
            throw "Expected exactly one package entry: $name"
        }
        $stream = $entries[0].Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
            if ($hash -ne (Get-FileHash -LiteralPath $payload[$name] -Algorithm SHA256).Hash) {
                throw "Package payload does not match its resolved source: $name"
            }
        }
        finally {
            $sha.Dispose()
            $stream.Dispose()
        }
    }
    $nativeEntries = @($archive.Entries | Where-Object { $_.FullName -like 'runtimes/*/native/*' })
    if ($nativeEntries.Count -ne 1) {
        throw "Source-mode package must contain only the QueryPlanInterop native DLL."
    }
    $nuspecs = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($nuspecs.Count -ne 1) {
        throw "Expected one package nuspec."
    }
    $reader = [System.IO.StreamReader]::new($nuspecs[0].Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
        $forbidden = $nuspec.SelectNodes("//*[local-name()='dependency' and (@id='Microsoft.Azure.Cosmos.Direct' or @id='Microsoft.Azure.Cosmos.QueryPlanInterop.Windows')]")
        if ($forbidden.Count -ne 0) {
            throw "Bundled Direct/native implementation must not become a consumer NuGet dependency."
        }
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $archive.Dispose()
}
Write-Host "Verified packaged Direct/native provenance and dependency boundaries: $PackagePath"
