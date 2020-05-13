if(!(Test-Path -Path .\Microsoft.Azure.Cosmos)){
    Write-Error "Please run script from root of the enlistment"
}

$relativeBuildFile = "bin\Debug\netcoreapp2.0\Current"
$isOneContractUpdated = $false

$currentRootTestPath = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\"
$currentBaselineFileName = "DotNetSDKAPI.json"
$currentJsonFilePath = $currentRootTestPath + $relativeBuildFile + $currentBaselineFileName
if(!(Test-Path -Path $currentJsonFilePath)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $currentJsonFilePath)
}else{
    Copy-Item -Path $currentJsonFilePath -Destination ($currentRootTestPath + $currentBaselineFileName)
    $isOneContractUpdated = $true
}

$currentRootTestPath = ".\Microsoft.Azure.Cosmos.Preview\tests\UnitTests\"
$currentBaselineFileName = "PreviewDotNetSDKAPI.json"
$currentJsonFilePath = $currentRootTestPath + $relativeBuildFile + $currentBaselineFileName
if(!(Test-Path -Path $currentJsonFilePath)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $currentJsonFilePath)
}else{
    Copy-Item -Path $currentJsonFilePath -Destination ($currentRootTestPath + $currentBaselineFileName)
    $isOneContractUpdated = $true
}


$currentRootTestPath = ".\Microsoft.Azure.Cosmos.Internal\tests\UnitTests\"
$currentBaselineFileName = "InternalDotNetSDKAPI.json"
$currentJsonFilePath = $currentRootTestPath + $relativeBuildFile + $currentBaselineFileName
if(!(Test-Path -Path $currentJsonFilePath)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $currentJsonFilePath)
}else{
    Copy-Item -Path $currentJsonFilePath -Destination ($currentRootTestPath + $currentBaselineFileName)
    $isOneContractUpdated = $true
}

$currentRootTestPath = ".\Microsoft.Azure.Cosmos.Encryption\tests\EmulatorTests\"
$currentBaselineFileName = "EncryptionSDKAPI.json"
$currentJsonFilePath = $currentRootTestPath + $relativeBuildFile + $currentBaselineFileName
if(!(Test-Path -Path $currentJsonFilePath)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $currentJsonFilePath)
}else{
    Copy-Item -Path $currentJsonFilePath -Destination ($currentRootTestPath + $currentBaselineFileName)
    $isOneContractUpdated = $true
}

if(!$isOneContractUpdated){
    Write-Error "No contracts have been updated. Please run the ContractEnforcement.ContractChanges for the projects requiring an update"
}