if(!(Test-Path -Path .\Microsoft.Azure.Cosmos)){
    Write-Error "Please run script from root of the enlistment"
}

#Run the Cosmos DB SDK GA contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$noContractsUpdated = $true

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.0\Contracts\DotNetSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
    $noContractsUpdated = $false
}

$updatedContractFolder = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.0\BaselineTest\TestOutput\*"
if(!(Test-Path -Path $updatedContractFolder)){
    Write-warning ("Please run tests to generate an updated contract at " + $updatedContractFolder)
}else{
    Copy-Item -Path $updatedContractFolder -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\BaselineTest\TestBaseline" -Recurse
    Write-Output ("Updated contract " + $updatedContractFolder)
    $noContractsUpdated = $false
}

#Run the Cosmos DB SDK Preview contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release -p:IsPreview=true

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.0\Contracts\DotNetPreviewSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetPreviewSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
    $noContractsUpdated = $false
}

#Run the Encryption SDK contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Microsoft.Azure.Cosmos.Encryption.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\bin\Release\netcoreapp2.0\Contracts\DotNetSDKEncryptionAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-warning ("Please run ContractEnforcement.ContractChanges tests to generate an updated contract at " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Contracts\DotNetSDKEncryptionAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
    $noContractsUpdated = $false
}

if($noContractsUpdated){
    Write-Error "No contracts have been updated. Please run the tests with category 'UpdateContract' for the projects requiring an update"
}