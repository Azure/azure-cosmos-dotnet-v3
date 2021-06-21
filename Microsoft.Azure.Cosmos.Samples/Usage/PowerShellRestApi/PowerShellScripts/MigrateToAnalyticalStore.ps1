# Note: Analytical migration is in preview, so this script will only work if your Cosmos DB account is opted into the preview.
# This script requires PowerShell 6.0.0 or higher.
param(
    [Parameter(Mandatory = $True, Position = 0, ValueFromPipeline = $false)]
    [System.String]
    $Endpoint,

    [Parameter(Mandatory = $True, Position = 1, ValueFromPipeline = $false)]
    [System.String]
    $MasterKey,
    
    [Parameter(Mandatory = $True, Position = 2, ValueFromPipeline = $false)]
    [System.String]
    $DatabaseName,
    
    [Parameter(Mandatory = $True, Position = 3, ValueFromPipeline = $false)]
    [System.String]
    $ContainerName,
    
    [Parameter(Mandatory = $True, Position = 4, ValueFromPipeline = $false)]
    [int]
    $NewAnalyticalTTL,

    [Parameter(Mandatory = $False, Position = 5, ValueFromPipeline = $false)]
    [int]
    $PollingIntervalInSeconds = 300 
)

Add-Type -AssemblyName System.Web

Function New-MasterKeyAuthorizationSignature {

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
    $bytesSigClear = [Text.Encoding]::UTF8.GetBytes($sigCleartext)
    $hmacsha = new-object -TypeName System.Security.Cryptography.HMACSHA256 -ArgumentList (, $keyBytes)
    $hash = $hmacsha.ComputeHash($bytesSigClear) 
    $signature = [System.Convert]::ToBase64String($hash)
    $key = [System.Web.HttpUtility]::UrlEncode('type=' + $KeyType + '&ver=' + $TokenVersion + '&sig=' + $signature)
    return $key
}

# Step 1: Collection read to get the existing indexing policy.
$KeyType = "master"
$TokenVersion = "1.0"
$date = Get-Date
$utcDate = $date.ToUniversalTime()
$xDate = $utcDate.ToString('r', [System.Globalization.CultureInfo]::InvariantCulture)
$containerResourceType = "colls"
$containerResourceId = "dbs/" + $DatabaseName + "/colls/" + $ContainerName
$containerResourceLink = "dbs/" + $DatabaseName + "/colls/" + $ContainerName
$requestUri = "$Endpoint$containerResourceLink"
$verbMethod = "GET"
$userAgent = "PowerShell-MigrateToAnalyticalStore"
$authKey = New-MasterKeyAuthorizationSignature -Verb $verbMethod -ResourceId $containerResourceId -ResourceType $containerResourceType -Date $xDate -MasterKey $MasterKey -KeyType $KeyType -TokenVersion $TokenVersion -ErrorAction Stop
$header = @{
    "authorization"                                      = "$authKey";
    "x-ms-version"                                       = "2018-12-31";
    "Cache-Control"                                      = "no-cache";
    "x-ms-date"                                          = "$xDate";
    "Accept"                                             = "application/json";
    "User-Agent"                                         = "$userAgent"
    "x-ms-cosmos-populate-analytical-migration-progress" = "true"
}

$result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json"

$NewBody = new-object psobject
$NewBody | Add-Member -MemberType NoteProperty -Name "id" -Value $result.id
$NewBody | Add-Member -MemberType NoteProperty -Name "partitionKey" -Value $result.partitionKey
$NewBody | Add-Member -MemberType NoteProperty -Name "analyticalStorageTtl" -Value $NewAnalyticalTTL
$NewBody = $NewBody | ConvertTo-Json

# Step 2: Collection replace to trigger analytical migration.
$verbMethod = "PUT"
$authKey = New-MasterKeyAuthorizationSignature -Verb $verbMethod -ResourceId $containerResourceId -ResourceType $containerResourceType -Date $xDate -MasterKey $MasterKey -KeyType $KeyType -TokenVersion $TokenVersion
$header = @{
    "authorization" = "$authKey";
    "x-ms-version"  = "2018-12-31";
    "Cache-Control" = "no-cache";
    "x-ms-date"     = "$xDate";
    "Accept"        = "application/json";
    "User-Agent"    = "$userAgent"
}

$result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -Body $NewBody
Write-Host "Analytical migration has been triggered and is pending."

# Step 3: Wait for analytical migration progress to reach 100%.
$Progress = "0"
while ($Progress -ne "100") {
    $date = Get-Date
    $utcDate = $date.ToUniversalTime()
    $xDate = $utcDate.ToString('r', [System.Globalization.CultureInfo]::InvariantCulture)
    $verbMethod = "GET"
    $authKey = New-MasterKeyAuthorizationSignature -Verb $verbMethod -ResourceId $containerResourceId -ResourceType $containerResourceType -Date $xDate -MasterKey $MasterKey -KeyType $KeyType -TokenVersion $TokenVersion
    $header = @{
        "authorization"                                      = "$authKey";
        "x-ms-version"                                       = "2018-12-31";
        "Cache-Control"                                      = "no-cache";
        "x-ms-date"                                          = "$xDate";
        "Accept"                                             = "application/json";
        "User-Agent"                                         = "$userAgent"
        "x-ms-cosmos-populate-analytical-migration-progress" = "true"
    }

    $result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -ResponseHeadersVariable Headers
    Start-Sleep -s $PollingIntervalInSeconds
    $Progress = $Headers["x-ms-cosmos-analytical-migration-progress"]
    Write-Host "$(Get-Date) - Progress = $Progress%"
}