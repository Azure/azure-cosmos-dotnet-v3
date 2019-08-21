Add-Type -AssemblyName System.Web

Function Generate-MasterKeyAuthorizationSignature{

    [CmdletBinding()]

    param (

        [string] $Verb,
        [string] $ResourceId,
        [string] $ResourceType,
        [string] $Date,
        [string] $MasterKey,
    	[String] $KeyType,
        [String] $TokenVersion
    )

    $keyBytes = [System.Convert]::FromBase64String($MasterKey)

    $sigCleartext = @($Verb.ToLower() + "`n" + $ResourceType.ToLower() + "`n" + $ResourceId + "`n" + $Date.ToString().ToLower() + "`n" + "" + "`n")
	Write-Host "sigCleartext = " $sigCleartext

    $bytesSigClear = [Text.Encoding]::UTF8.GetBytes($sigCleartext)

    $hmacsha = new-object -TypeName System.Security.Cryptography.HMACSHA256 -ArgumentList (, $keyBytes)

    $hash = $hmacsha.ComputeHash($bytesSigClear) 

    $signature = [System.Convert]::ToBase64String($hash)

    $key = [System.Web.HttpUtility]::UrlEncode('type='+$KeyType+'&ver='+$TokenVersion+'&sig=' + $signature)

    return $key
}

$endpoint = "https://localhost:8081/"
$MasterKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="

$KeyType = "master"
$TokenVersion = "1.0"
$date = Get-Date
$utcDate = $date.ToUniversalTime()
$xDate = $utcDate.ToString('r', [System.Globalization.CultureInfo]::InvariantCulture)
$databaseId = "TestDB"
$containerId = "TestContainer"
$itemId = "TestItem"
$itemResourceType = "docs"
$itemResourceId = "dbs/"+$databaseId+"/colls/"+$containerId+"/docs/"+$ItemId
$itemResourceLink = "dbs/"+$databaseId+"/colls/"+$containerId+"/docs/"+$ItemId
$verbMethod = "PUT"

$requestUri = "$endpoint$itemResourceLink"

$authKey = Generate-MasterKeyAuthorizationSignature -Verb $verbMethod -ResourceId $itemResourceId -ResourceType $itemResourceType -Date $xDate -MasterKey $MasterKey -KeyType $KeyType -TokenVersion $TokenVersion

$header = @{

        "authorization"         = "$authKey";

        "x-ms-version"          = "2018-12-31";

        "Cache-Control"         = "no-cache";

        "x-ms-date"             = "$xDate";

        "Accept"                = "application/json";

        "User-Agent"            = "PowerShell-RestApi-Samples";

		"x-ms-documentdb-partitionkey" = '["testPk"]'
    }

$ItemDefinition = @"
{
    "id": "$ItemId",
	"testProperty": "testReplace",
	"pk": "testPk"
}
"@

try {
    $result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -Body $ItemDefinition
    Write-Host "Replace Item response = "$result
    return "ReplaceItemSuccess";
}
catch {
    # Dig into the exception to get the Response details.
    # Note that value__ is not a typo.
    Write-Host "StatusCode:" $_.Exception.Response.StatusCode.value__ 
    Write-Host "Exception Message:" $_.Exception.Message
	echo $_.Exception|format-list -force
}