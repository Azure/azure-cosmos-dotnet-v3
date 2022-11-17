$baseDir    = "e:\src\m1"
$sourceDir = 
    @(
        "\Product\SDK\.net\Microsoft.Azure.Cosmos.Direct\src\",
        "\Product\Microsoft.Azure.Documents\Common\SharedFiles\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Routing\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd2\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd\",
        "\Product\SDK\.net\Microsoft.Azure.Documents.Client\LegacyXPlatform\",
        "\Product\Cosmos\Core\Core.Trace\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Collections\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Query\",
        "\Product\Microsoft.Azure.Documents\SharedFiles\Management\"
    )
$syncCopyFile="msdata_sync.ps1"
$exclueList =
    @(
        "BaseTransportClient.cs",
        "CpuReaderBase.cs",
        "LinuxCpuReader.cs",
        "MaterializedViews.cs",
        "MemoryLoad.cs",
        "MemoryLoadHistory.cs",
        "UnsupportedCpuReader.cs",
        "WindowsCpuReader.cs",
        $syncCopyFile
    )

foreach ($excluded in $exclueList)
{
    if ($excluded -ne $syncCopyFile)
    {
        Remove-Item $excluded -ErrorAction SilentlyContinue 
    }
}

$currentLocation = (Get-Location).Path

# Transport Client overloaded/replicated and special case it 
$sourceTransportFile = $baseDir + "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd2\TransportClient.cs"
$targetDir = $currentLocation + "\rntbd2"
Copy-Item $sourceTransportFile  -Destination $targetDir -Force

$sourceTransportFile = $baseDir + "\Product\Microsoft.Azure.Documents\SharedFiles\TransportClient.cs"
Copy-Item $sourceTransportFile  -Destination $currentLocation -Force

$sourceTransportFile = $baseDir + "\Product\Microsoft.Azure.Documents\SharedFiles\RMResources.Designer.cs"
$targetDir = $currentLocation + "\.."
Copy-Item $sourceTransportFile  -Destination $targetDir -Force

$sourceTransportFile = $baseDir + "\Product\Microsoft.Azure.Documents\SharedFiles\RMResources.resx"
Copy-Item $sourceTransportFile  -Destination $targetDir -Force


foreach ($file in Get-ChildItem . -Name )
{
    $fileFound = $false;

    if ($file -eq "TransportClient.cs")
    {
        continue;
    }

    foreach($source in $sourceDir)
    {
        $targetFile = $baseDir + $source + $file 
        $fileFound = Test-Path $targetFile;
        if ($fileFound)
        {
            break;
        }
    }

    if (! ($fileFound -or $exclueList -contains $file))
    {
        Write-Error "$file $fileFound"
        break;
    }

    if ($fileFound)
    {
        Copy-Item $targetFile -Destination $currentLocation -Force
    }
}
