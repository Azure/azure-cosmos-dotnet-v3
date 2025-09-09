if(!(Test-Path -Path .\Microsoft.Azure.Cosmos)){
    Write-Error "Please run script from root of the enlistment"
}

#Run the Cosmos DB SDK GA contract tests
dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\net6.0\Contracts\DotNetSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\net6.0\Contracts\DotNetSDKTelemetryAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetSDKTelemetryAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

$updatedContractFolder = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\net6.0\BaselineTest\TestOutput\*"
if(!(Test-Path -Path $updatedContractFolder)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFolder -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\BaselineTest\TestBaseline" -Recurse
    Write-Output ("Updated contract " + $updatedContractFolder)
}

#Run the Cosmos DB SDK Emulator contract tests
dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.EmulatorTests\Microsoft.Azure.Cosmos.EmulatorTests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFolder = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.EmulatorTests\bin\Release\net6.0\BaselineTest\TestOutput\*"
if(!(Test-Path -Path $updatedContractFolder)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFolder -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.EmulatorTests\BaselineTest\TestBaseline" -Recurse
    Write-Output ("Updated contract " + $updatedContractFolder)
}

#Run the Cosmos DB SDK Preview contract tests
dotnet test '.\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Microsoft.Azure.Cosmos.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release -p:IsPreview=true

$updatedContractFile = ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\bin\Release\net6.0\Contracts\DotNetPreviewSDKAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the preview build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Contracts\DotNetPreviewSDKAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

#Run the Encryption SDK contract tests
dotnet test '.\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Microsoft.Azure.Cosmos.Encryption.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\bin\Release\net6.0\Contracts\DotNetSDKEncryptionAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Contracts\DotNetSDKEncryptionAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

try {
    dotnet test '.\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Microsoft.Azure.Cosmos.Encryption.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release -f net8.0
    $updatedContractFileNet8 = ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\bin\Release\net8.0\Contracts\DotNetSDKEncryptionAPIChanges.json"
    if (Test-Path -Path $updatedContractFileNet8) {
        Copy-Item -Path $updatedContractFileNet8 -Destination ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\Contracts\DotNetSDKEncryptionAPI.net8.json"
        Write-Output ("Updated .NET 8 contract " + $updatedContractFileNet8)
        
        # Also copy to Debug output directories so tests work in both configurations
        $debugNet8Path = ".\Microsoft.Azure.Cosmos.Encryption\tests\Microsoft.Azure.Cosmos.Encryption.Tests\bin\Debug\net8.0\Contracts\DotNetSDKEncryptionAPI.net8.json"
        if (Test-Path -Path (Split-Path $debugNet8Path)) {
            Copy-Item -Path $updatedContractFileNet8 -Destination $debugNet8Path -Force
        }
    }
} catch {
    Write-Warning "Unable to run net8.0 Encryption tests to produce .NET 8 baseline. Skipping."
}

#Run the Encryption.Custom SDK contract tests
dotnet test '.\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release

$updatedContractFile = ".\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\bin\Release\net6.0\Contracts\DotNetSDKEncryptionCustomAPIChanges.json"
if(!(Test-Path -Path $updatedContractFile)){
    Write-Error ("The contract file did not get updated with the build. Please fix the test to output the contract file: " + $updatedContractFile)
}else{
    Copy-Item -Path $updatedContractFile -Destination ".\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\Contracts\DotNetSDKEncryptionCustomAPI.json"
    Write-Output ("Updated contract " + $updatedContractFile)
}

# Try to generate and copy .NET 8 specific baselines if net8 target exists
try {
    dotnet test '.\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests.csproj' --filter "TestCategory=UpdateContract" --configuration Release -f net8.0
    $updatedContractFileNet8 = ".\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\bin\Release\net8.0\Contracts\DotNetSDKEncryptionCustomAPIChanges.json"
    if (Test-Path -Path $updatedContractFileNet8) {
        Copy-Item -Path $updatedContractFileNet8 -Destination ".\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\Contracts\DotNetSDKEncryptionCustomAPI.net8.json"
        Write-Output ("Updated .NET 8 contract " + $updatedContractFileNet8)
        
        # Also copy to Debug output directories so tests work in both configurations
        $debugNet8Path = ".\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Tests\bin\Debug\net8.0\Contracts\DotNetSDKEncryptionCustomAPI.net8.json"
        if (Test-Path -Path (Split-Path $debugNet8Path)) {
            Copy-Item -Path $updatedContractFileNet8 -Destination $debugNet8Path -Force
        }
    }
} catch {
    Write-Warning "Unable to run net8.0 Encryption.Custom tests to produce .NET 8 baseline. Skipping."
}
