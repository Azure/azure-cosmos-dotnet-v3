#Requires -Version 7
#Requires -Module Az.CosmosDB
# Note: Analytical migration is in preview, so this script will only work if your Cosmos DB account is opted into the preview.
# This script requires PowerShell 7.0.0 or higher.
param(
    [Parameter(Mandatory = $True, Position = 0, ValueFromPipeline = $false)]
    [System.String]
    $SubscriptionId,

    [Parameter(Mandatory = $True, Position = 1, ValueFromPipeline = $false)]
    [System.String]
    $ResourceGroupName,

    [Parameter(Mandatory = $True, Position = 2, ValueFromPipeline = $false)]
    [System.String]
    $AccountName,
    
    [Parameter(Mandatory = $True, Position = 3, ValueFromPipeline = $false)]
    [System.String]
    $DatabaseName,
    
    [Parameter(Mandatory = $True, Position = 4, ValueFromPipeline = $false)]
    [System.String]
    $ContainerName,
    
    [Parameter(Mandatory = $False, Position = 5, ValueFromPipeline = $false)]
    [int]
    $NewAnalyticalTTL = -1,

    # The progress on the backend is updated every 60 seconds.
    # PollingIntervalInSeconds is how often we want to fetch this value from the backend (any less than 60 is redundant).
    [Parameter(Mandatory = $False, Position = 6, ValueFromPipeline = $false)]
    [int]
    $PollingIntervalInSeconds = 60 
)

Add-Type -AssemblyName System.Web

# First, check if EnableAnalyticalStorage is true. If not, enable it.
Write-Host "Checking if EnableAnalyticalStorage is true..."
$result = Connect-AzAccount
$result = Select-AzSubscription -Subscription $SubscriptionId
$AccountInfo = Get-AzCosmosDBAccount -ResourceGroupName $ResourceGroupName -Name $AccountName
if (!$AccountInfo.EnableAnalyticalStorage) {
    Write-Host "Setting EnableAnalyticalStorage capability to true..."
    $result = Update-AzCosmosDBAccount -ResourceGroupName $ResourceGroupName -Name $AccountName -EnableAnalyticalStorage 1
}
Write-Host "EnableAnalyticalStorage capability is true."

# Get the account key and endpoint.
$result = Get-AzCosmosDBAccountKey -ResourceGroupName $ResourceGroupName -Name $AccountName 
$MasterKey = $result["PrimaryMasterKey"]

$result = Get-AzCosmosDBAccount -ResourceGroupName $ResourceGroupName -Name $AccountName
$Endpoint = $result.DocumentEndpoint
$containerResourceLink = "dbs/" + $DatabaseName + "/colls/" + $ContainerName
$requestUri = "$Endpoint$containerResourceLink"

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

Function Get-Header {

    [CmdletBinding()]

    param (
        [string] $Verb,
        [string] $MasterKey,
        [string] $DatabaseName,
        [string] $ContainerName
    )
    $date = Get-Date
    $utcDate = $date.ToUniversalTime()
    $xDate = $utcDate.ToString('r', [System.Globalization.CultureInfo]::InvariantCulture)
    $KeyType = "master"
    $TokenVersion = "1.0"
    $containerResourceType = "colls"
    $containerResourceId = "dbs/" + $DatabaseName + "/colls/" + $ContainerName
    $userAgent = "PowerShell-MigrateToAnalyticalStore"
    $authKey = New-MasterKeyAuthorizationSignature -Verb $Verb -ResourceId $containerResourceId -ResourceType $containerResourceType -Date $xDate -MasterKey $MasterKey -KeyType $KeyType -TokenVersion $TokenVersion -ErrorAction Stop
    $header = @{
        "authorization"                                      = "$authKey";
        "x-ms-version"                                       = "2018-12-31";
        "Cache-Control"                                      = "no-cache";
        "x-ms-date"                                          = "$xDate";
        "Accept"                                             = "application/json";
        "User-Agent"                                         = "$userAgent";
    }
    return $header
}

# Step 1: Collection read to get the existing indexing policy.
$verbMethod = "GET"
$header = Get-Header -Verb $verbMethod -MasterKey $MasterKey -DatabaseName $DatabaseName -ContainerName $ContainerName
$header.add("x-ms-cosmos-populate-analytical-migration-progress", "true")
$responseHeaders = $null
$result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -ResponseHeadersVariable responseHeaders 

if(!$result.analyticalStorageTtl) {
    $result | Add-Member -MemberType NoteProperty -Name "analyticalStorageTtl" -Value $NewAnalyticalTTL -Force
    $body = $result | ConvertTo-Json -Depth 20
    
    # Step 2: Collection replace to trigger analytical migration.
    $verbMethod = "PUT"
    $header = Get-Header -Verb $verbMethod -MasterKey $MasterKey -DatabaseName $DatabaseName -ContainerName $ContainerName
    $result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -Body $body
    Write-Host "Analytical migration has been triggered and is pending."
} 
else {
    $Progress = $responseHeaders["x-ms-cosmos-analytical-migration-progress"]
    if($Progress -eq "100") {
        Write-Host "Migration is already complete."
        return
    }
}

# Step 3: Wait for analytical migration progress to reach 100%.
Write-Host "Polling for progress..."
while ($true) {
    $verbMethod = "GET"
    $header = Get-Header -Verb $verbMethod -MasterKey $MasterKey -DatabaseName $DatabaseName -ContainerName $ContainerName
    $header.add("x-ms-cosmos-populate-analytical-migration-progress", "true")
    $result = Invoke-RestMethod -Uri $requestUri -Headers $header -Method $verbMethod -ContentType "application/json" -ResponseHeadersVariable responseHeaders
    $Progress = $responseHeaders["x-ms-cosmos-analytical-migration-progress"]
    Write-Host "$(Get-Date) - Progress = $Progress%"
    if ($Progress -eq "100") {
        Write-Host "Migration complete."
        return
    }
    Start-Sleep -s $PollingIntervalInSeconds
}