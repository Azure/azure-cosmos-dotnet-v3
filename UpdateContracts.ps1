if(!(Test-Path -Path .\Microsoft.Azure.Cosmos)){
    Write-Error "Please run script from root of the enlistment"
}

#Run the Cosmos DB SDK GA contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.1\Contracts\DotNetSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

$updatedContractFolder = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.1\BaselineTest\TestOutput\*"
if(!(Test-Path -Path $updatedContractFolder)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFolder -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\BaselineTest\TestBaseline" -Recurse
    Write-Output ("Updated contract " + $updatedContractFolder)
}

#Run the Cosmos DB SDK Preview contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release -p:IsPreview=true

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\netcoreapp2.1\Contracts\DotNetPreviewSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the preview build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetPreviewSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

#Run the Encryption SDK contract tests
$projResult = dotnet test '.\Microsoft.Azure.Cosmos.Encryption\Custom\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Microsoft.Azure.Cosmos.Encryption.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos.Encryption\Custom\tests\Microsoft.Azure.Cosmos.Encryption.Tests\bin\Release\netcoreapp2.1\Contracts\DotNetSDKEncryptionAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos.Encryption\Custom\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Contracts\DotNetSDKEncryptionAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}
