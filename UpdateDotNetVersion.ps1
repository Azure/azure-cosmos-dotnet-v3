$projFiles = Get-ChildItem . *.csproj -rec

$oldVersion = "<TargetFramework>netcoreapp3.1</TargetFramework>";
$newVersion = "net6.0</TargetFramework>"
foreach ($file in $projFiles)
{
    $content = (Get-Content $file.PSPath)
    if($content -match $oldVersion){
    
        $content -replace $oldVersion, $newVersion | Set-Content $file.PSPath
    }
}