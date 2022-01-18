# Script to update all the csproj .NET target framework version
$projFiles = Get-ChildItem . *.csproj -rec

foreach ($file in $projFiles)
{
    $content = (Get-Content $file.PSPath)
    if($content -match "<TargetFramework>netcoreapp3.1</TargetFramework>"){
    
        $content -replace "<TargetFramework>netcoreapp3.1</TargetFramework>", "<TargetFramework>net6.0</TargetFramework>" | Set-Content $file.PSPath
    }
}