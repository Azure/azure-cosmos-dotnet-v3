<#
.SYNOPSIS
    Cosmos DB "GSI" (Materialized Views) scenario runner. Creates an N-region account
    (single-write or multi-master), a source + GSI container, seeds data, exercises GSI,
    runs a background write workload, and fails a region out/back in, confirming region
    role changes appropriately for the account's write-region mode.

.PARAMETER Regions
    Ordered array of Azure regions. First region is the initial write region (failoverPriority 0)
    for single-master accounts. 2 or 3 regions supported.

.PARAMETER MultiMaster
    If set, account is created with multiple write locations enabled (all regions accept writes).

.NOTES
    See New-CosmosGsiTest.ps1 history for the discovered fixes this incorporates:
    - Account must be created with --disable-local-auth true (tenant policy denies local auth).
    - ARM container PUT is an async LRO; must poll for existence, not just fire-and-forget.
    - Cosmos data-plane AAD calls use a FIXED audience https://cosmos.azure.com (not the
      per-account endpoint), and require a manually-built Authorization header of the form
      "type=aad&ver=1.0&sig=<token>" (Bearer auth is rejected) - done via Invoke-RestMethod
      rather than `az rest` to avoid shell quoting problems with the '&' characters.
    - GSI capability "EnableMaterializedViews" additionally requires the subscription-level
      preview feature Microsoft.DocumentDB/MaterializedViewsForNoSQL to be in state
      "Registered" (az feature show ...). If it's still "Pending" (Azure-gated approval),
      enabling the capability fails with "(BadRequest) Invalid capability
      EnableMaterializedViews" - this script detects that and continues the rest of the
      scenario (seed data / background writes / failover) without the GSI container,
      logging a clear warning instead of failing the whole run.
#>
[CmdletBinding()]
param(
    [string]$Subscription = "074d02eb-4d74-486a-b299-b262264d1536",
    [string]$ResourceGroup = "gsi_test",
    [string[]]$Regions = @("eastus2","southeastasia"),
    [switch]$MultiMaster,
    [ValidateSet("Yes","No")]
    [string]$DeleteAtEnd = "No",
    [string]$GsiCapabilityName = "EnableMaterializedViews",
    [int]$DocCount = 100,
    [int]$BackgroundInsertIntervalSeconds = 3,
    [int]$ScenarioTag = 0
)

$ErrorActionPreference = "Stop"
if ($Regions.Count -lt 2 -or $Regions.Count -gt 3) { throw "Regions must contain 2 or 3 entries." }

function Get-RegionSlug {
    param($LocationObj, $AccountName)
    return ($LocationObj.id -replace "^$([regex]::Escape($AccountName))-", "")
}

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

function Invoke-AzJson($azArgs) {
    $out = & az @azArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "az $($azArgs -join ' ') failed:`n$out" }
    if ([string]::IsNullOrWhiteSpace(($out -join ""))) { return $null }
    # az occasionally emits stderr WARNING lines even on success; strip them before parsing JSON.
    $jsonText = ($out | Where-Object { $_.ToString() -notmatch '^\s*WARNING:' }) -join "`n"
    if ([string]::IsNullOrWhiteSpace($jsonText)) { return $null }
    return $jsonText | ConvertFrom-Json
}

function Get-CosmosAadHeader {
    $token = az account get-access-token --resource "https://cosmos.azure.com" --query accessToken -o tsv
    $raw = "type=aad&ver=1.0&sig=$token"
    return [System.Uri]::EscapeDataString($raw)
}

function Add-CosmosDocument {
    param($Endpoint, $DbName, $ContainerName, [hashtable]$DocObj, $PkValue, $AuthHeader)
    $uri = "$Endpoint/dbs/$DbName/colls/$ContainerName/docs"
    $body = $DocObj | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json" `
        -Headers @{ Authorization = $AuthHeader; 'x-ms-version' = '2018-12-31'; 'x-ms-documentdb-partitionkey' = "[`"$PkValue`"]" } `
        -Body $body | Out-Null
}

function Invoke-CosmosQuery {
    param($Endpoint, $DbName, $ContainerName, $QueryText, $AuthHeader)
    $uri = "$Endpoint/dbs/$DbName/colls/$ContainerName/docs"
    $body = @{ query = $QueryText; parameters = @() } | ConvertTo-Json
    return Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/query+json" `
        -Headers @{ Authorization = $AuthHeader; 'x-ms-version' = '2018-12-31'; 'x-ms-documentdb-isquery' = 'true'; 'x-ms-documentdb-query-enablecrosspartition' = 'true' } `
        -Body $body
}

function Wait-ArmContainer {
    param($AccountName, $DbName, $ContainerName, $ResourceGroup, [int]$TimeoutSeconds = 180)
    $elapsed = 0
    while ($elapsed -lt $TimeoutSeconds) {
        $show = & az cosmosdb sql container show --account-name $AccountName --resource-group $ResourceGroup --database-name $DbName --name $ContainerName -o json 2>&1
        if ($LASTEXITCODE -eq 0) { return ($show | ConvertFrom-Json) }
        Start-Sleep -Seconds 5
        $elapsed += 5
    }
    throw "Timed out waiting for container '$ContainerName' to be provisioned."
}

$mode = if ($MultiMaster) { "multi-master" } else { "single-master" }
Write-Step "SCENARIO: $($Regions.Count)-region, $mode -> regions=[$($Regions -join ', ')]"

# Pre-flight: check subscription-level preview feature registration so we don't waste
# minutes polling for a container that will never materialize with allowMaterializedViews set.
$featureState = (az feature show --namespace Microsoft.DocumentDB --name MaterializedViewsForNoSQL --query "properties.state" -o tsv 2>$null)
$gsiFeatureRegistered = ($featureState -eq "Registered")
if (-not $gsiFeatureRegistered) {
    Write-Warning "Subscription feature Microsoft.DocumentDB/MaterializedViewsForNoSQL state='$featureState' (not 'Registered'). GSI (materialized view) container/capability will be skipped for this scenario; rest of the workflow proceeds normally."
}

# ---------------------------------------------------------------------------
# 1/2. Login / select subscription
# ---------------------------------------------------------------------------
az account set --subscription $Subscription
$account = Invoke-AzJson @("account","show","-o","json")
$alias = ($account.user.name -split "@")[0] -replace '[^a-zA-Z0-9]', ''
$alias = $alias.ToLower()

# ---------------------------------------------------------------------------
# 3. Resource group
# ---------------------------------------------------------------------------
$rgExists = (az group exists -n $ResourceGroup) -eq "true"
if (-not $rgExists) {
    az group create -n $ResourceGroup -l $Regions[0] | Out-Null
}

# ---------------------------------------------------------------------------
# 4. Names
# ---------------------------------------------------------------------------
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$suffix = if ($ScenarioTag -gt 0) { "$stamp$ScenarioTag" } else { $stamp }
$accountName = "$alias-$suffix"
$dbName = "db-$alias-$suffix"
$srcContainerName = "coll-$alias-$suffix"
$mvContainerName = "coll-mvPk-$alias-$suffix"

Write-Host "Account=$accountName Db=$dbName Src=$srcContainerName Mv=$mvContainerName"

# ---------------------------------------------------------------------------
# 5. Create account across N regions
# ---------------------------------------------------------------------------
Write-Step "Creating account '$accountName'"
$locationArgs = @()
for ($i = 0; $i -lt $Regions.Count; $i++) {
    $locationArgs += "regionName=$($Regions[$i])"
    $locationArgs += "failoverPriority=$i"
    $locationArgs += "isZoneRedundant=False"
}
$createArgs = @(
    "cosmosdb","create",
    "--name",$accountName,
    "--resource-group",$ResourceGroup,
    "--default-consistency-level","Session",
    "--disable-local-auth","true"
)
# az cosmosdb create takes repeated --locations blocks; build via generic invocation
$azCreateCmd = @("cosmosdb","create","--name",$accountName,"--resource-group",$ResourceGroup,"--default-consistency-level","Session","--disable-local-auth","true")
foreach ($r in $Regions) {
    $idx = [array]::IndexOf($Regions,$r)
    $azCreateCmd += "--locations"
    $azCreateCmd += "regionName=$r"
    $azCreateCmd += "failoverPriority=$idx"
    $azCreateCmd += "isZoneRedundant=False"
}
if ($MultiMaster) {
    $azCreateCmd += "--enable-multiple-write-locations"
    $azCreateCmd += "true"
} else {
    $azCreateCmd += "--enable-automatic-failover"
    $azCreateCmd += "false"
}
Invoke-AzJson $azCreateCmd | Out-Null

$accountShow = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
$documentEndpoint = $accountShow.documentEndpoint.TrimEnd('/')
Write-Host "Account created. Endpoint=$documentEndpoint WriteLocations=$($accountShow.writeLocations.locationName -join ',')"

# ---------------------------------------------------------------------------
# RBAC grant for AAD data-plane access
# ---------------------------------------------------------------------------
Write-Step "Granting Cosmos DB Built-in Data Contributor"
$objectId = (az ad signed-in-user show --query id -o tsv)
$roleDefId = "/subscriptions/$Subscription/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$accountName/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
az cosmosdb sql role assignment create --account-name $accountName --resource-group $ResourceGroup --role-definition-id $roleDefId --principal-id $objectId --scope "/" | Out-Null
Start-Sleep -Seconds 15  # RBAC propagation

# ---------------------------------------------------------------------------
# 6/7. Database + source container
# ---------------------------------------------------------------------------
Write-Step "Creating database '$dbName' and source container '$srcContainerName' (/srcPk)"
az cosmosdb sql database create --account-name $accountName --resource-group $ResourceGroup --name $dbName | Out-Null

$armApiVersion = "2024-11-15"
$srcContainerUri = "https://management.azure.com/subscriptions/$Subscription/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$accountName/sqlDatabases/$dbName/containers/${srcContainerName}?api-version=$armApiVersion"
$srcResource = @{ id = $srcContainerName; partitionKey = @{ paths = @("/srcPk"); kind = "Hash" } }
if ($gsiFeatureRegistered) { $srcResource["allowMaterializedViews"] = $true }
$srcBody = @{ properties = @{ resource = $srcResource; options = @{} } } | ConvertTo-Json -Depth 10
$srcBodyFile = "$env:TEMP\srcBody-$accountName.json"
$srcBody | Out-File -Encoding utf8 $srcBodyFile
az rest --method put --uri $srcContainerUri --body "@$srcBodyFile" | Out-Null
Wait-ArmContainer -AccountName $accountName -DbName $dbName -ContainerName $srcContainerName -ResourceGroup $ResourceGroup -TimeoutSeconds 90 | Out-Null
Write-Host "Source container ready."

# ---------------------------------------------------------------------------
# 8. Insert seed documents
# ---------------------------------------------------------------------------
Write-Step "Inserting $DocCount seed documents"
$authHeader = Get-CosmosAadHeader
for ($i = 1; $i -le $DocCount; $i++) {
    $pk = "pk-$i"
    Add-CosmosDocument -Endpoint $documentEndpoint -DbName $dbName -ContainerName $srcContainerName -DocObj @{ id = "seed-$i"; srcPk = $pk; mvPk = $pk } -PkValue $pk -AuthHeader $authHeader
}
Write-Host "Inserted $DocCount documents."

# ---------------------------------------------------------------------------
# 9/10. Enable + verify GSI capability (best-effort; subscription may still be Pending)
# ---------------------------------------------------------------------------
Write-Step "Enabling GSI capability '$GsiCapabilityName'"
$gsiAvailable = $false
if ($gsiFeatureRegistered) {
    try {
        az cosmosdb update --name $accountName --resource-group $ResourceGroup --capabilities $GsiCapabilityName 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "capability update failed" }
        $capCheck = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
        if (-not ($capCheck.capabilities | Where-Object { $_.name -eq $GsiCapabilityName })) { throw "capability not present after update" }
        $gsiAvailable = $true
        Write-Host "GSI capability confirmed enabled."
    } catch {
        Write-Warning "GSI capability could not be enabled even though the subscription feature reports Registered. Error: $($_.Exception.Message)"
    }
} else {
    Write-Host "Skipping GSI capability enable - subscription feature not Registered yet."
}

# ---------------------------------------------------------------------------
# 11/12. GSI container + query (only if capability available)
# ---------------------------------------------------------------------------
if ($gsiAvailable) {
    Write-Step "Creating GSI container '$mvContainerName'"
    $srcContainerShow = Invoke-AzJson @("cosmosdb","sql","container","show","--account-name",$accountName,"--resource-group",$ResourceGroup,"--database-name",$dbName,"--name",$srcContainerName,"-o","json")
    $srcResourceId = $srcContainerShow.resource.rid
    $mvContainerUri = "https://management.azure.com/subscriptions/$Subscription/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$accountName/sqlDatabases/$dbName/containers/${mvContainerName}?api-version=$armApiVersion"
    $mvBody = @{ properties = @{ resource = @{ id = $mvContainerName; partitionKey = @{ paths = @("/mvPk"); kind = "Hash" }; materializedViewDefinition = @{ sourceContainerId = $srcContainerName; sourceContainerResourceId = $srcResourceId; definition = "SELECT * FROM c" } }; options = @{ autoscaleSettings = @{ maxThroughput = 4000 } } } } | ConvertTo-Json -Depth 10
    $mvBodyFile = "$env:TEMP\mvBody-$accountName.json"
    $mvBody | Out-File -Encoding utf8 $mvBodyFile
    az rest --method put --uri $mvContainerUri --body "@$mvBodyFile" | Out-Null
    Wait-ArmContainer -AccountName $accountName -DbName $dbName -ContainerName $mvContainerName -ResourceGroup $ResourceGroup | Out-Null
    Write-Host "GSI container ready; waiting for change-feed sync..."
    Start-Sleep -Seconds 30

    Write-Step "Querying GSI container"
    $queryResult = Invoke-CosmosQuery -Endpoint $documentEndpoint -DbName $dbName -ContainerName $mvContainerName -QueryText "SELECT VALUE COUNT(1) FROM c" -AuthHeader $authHeader
    Write-Host "GSI container document count: $($queryResult.Documents[0])"
} else {
    Write-Host "Skipping steps 11/12 (GSI container + query) - GSI not enabled for this subscription yet."
}

# ---------------------------------------------------------------------------
# 13. Background insert job
# ---------------------------------------------------------------------------
Write-Step "Starting background insert job"
$job = Start-Job -Name "CosmosBgInsert-$accountName" -ScriptBlock {
    param($Endpoint, $DbName, $ContainerName, $IntervalSeconds)
    $i = 0
    while ($true) {
        $i++
        $pk = "bg-pk-$i"
        $token = az account get-access-token --resource "https://cosmos.azure.com" --query accessToken -o tsv
        $authHeader = [System.Uri]::EscapeDataString("type=aad&ver=1.0&sig=$token")
        $doc = @{ id = "bg-$([guid]::NewGuid())"; srcPk = $pk; mvPk = $pk } | ConvertTo-Json -Compress
        try {
            Invoke-RestMethod -Uri "$Endpoint/dbs/$DbName/colls/$ContainerName/docs" -Method Post -ContentType "application/json" `
                -Headers @{ Authorization = $authHeader; 'x-ms-version' = '2018-12-31'; 'x-ms-documentdb-partitionkey' = "[`"$pk`"]" } `
                -Body $doc | Out-Null
        } catch { }
        Start-Sleep -Seconds $IntervalSeconds
    }
} -ArgumentList $documentEndpoint, $dbName, $srcContainerName, $BackgroundInsertIntervalSeconds
Write-Host "Background job id $($job.Id) started."

# ---------------------------------------------------------------------------
# 14/15. Offline a region while background writes continue, then confirm + restore
# ---------------------------------------------------------------------------
if (-not $MultiMaster) {
    $writeRegion = $Regions[0]
    $nextRegion = $Regions[1]
    Write-Step "Single-master: taking write region '$writeRegion' offline via failover-priority-change"
    $policies = @()
    $policies += "$nextRegion=0"
    $policies += "$writeRegion=1"
    if ($Regions.Count -eq 3) { $policies += "$($Regions[2])=2" }
    az cosmosdb failover-priority-change --name $accountName --resource-group $ResourceGroup --failover-policies $policies | Out-Null
    Write-Host "Waiting for failover to settle..."
    Start-Sleep -Seconds 90

    $postFailover = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
    $topWrite = $postFailover.writeLocations | Sort-Object failoverPriority | Select-Object -First 1
    $currentWrite = Get-RegionSlug -LocationObj $topWrite -AccountName $accountName
    Write-Host "Current write region: $currentWrite ($($topWrite.locationName))"
    if ($currentWrite -eq $nextRegion) { Write-Host "CONFIRMED: write region moved from $writeRegion to $nextRegion." }
    else { Write-Warning "Expected write region '$nextRegion' but found '$currentWrite'." }

    Write-Step "Restoring original priority order (bringing '$writeRegion' back online as a read region)"
    $restorePolicies = @()
    $restorePolicies += "$writeRegion=0"
    $restorePolicies += "$nextRegion=1"
    if ($Regions.Count -eq 3) { $restorePolicies += "$($Regions[2])=2" }
    Invoke-AzJson (@("cosmosdb","failover-priority-change","--name",$accountName,"--resource-group",$ResourceGroup,"--failover-policies") + $restorePolicies) | Out-Null
    Start-Sleep -Seconds 90
    $final = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
    $finalTopWrite = $final.writeLocations | Sort-Object failoverPriority | Select-Object -First 1
    $finalWrite = Get-RegionSlug -LocationObj $finalTopWrite -AccountName $accountName
    $isRead = $final.readLocations | Where-Object { (Get-RegionSlug -LocationObj $_ -AccountName $accountName) -eq $writeRegion }
    if ($finalWrite -eq $writeRegion) { Write-Host "CONFIRMED: '$writeRegion' is the write region again (back to original)." }
    else { Write-Warning "Expected '$writeRegion' to be write region again but found '$finalWrite'." }
    if ($isRead) { Write-Host "CONFIRMED: '$writeRegion' present among read locations." }
} else {
    # Multi-master: there is no single write region - all regions accept writes.
    # "Offline" a region by removing it from the account's locations list (simulated regional outage),
    # then add it back and confirm it re-joins as a write location (multi-master regions are always write).
    $regionToOffline = $Regions[0]
    $remainingRegions = @($Regions | Where-Object { $_ -ne $regionToOffline })
    Write-Step "Multi-master: removing region '$regionToOffline' from account (simulated offline)"
    $removeCmd = @("cosmosdb","update","--name",$accountName,"--resource-group",$ResourceGroup)
    foreach ($r in $remainingRegions) {
        $idx = [array]::IndexOf($remainingRegions,$r)
        $removeCmd += "--locations"
        $removeCmd += "regionName=$r"
        $removeCmd += "failoverPriority=$idx"
        $removeCmd += "isZoneRedundant=False"
    }
    Invoke-AzJson $removeCmd | Out-Null
    Start-Sleep -Seconds 90
    $mid = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
    $stillThere = $mid.writeLocations | Where-Object { (Get-RegionSlug -LocationObj $_ -AccountName $accountName) -eq $regionToOffline }
    if (-not $stillThere) { Write-Host "CONFIRMED: '$regionToOffline' removed; remaining write regions: $(($mid.writeLocations | ForEach-Object { Get-RegionSlug -LocationObj $_ -AccountName $accountName }) -join ',')" }
    else { Write-Warning "'$regionToOffline' still present in writeLocations." }

    Write-Step "Bringing '$regionToOffline' back online"
    $addCmd = @("cosmosdb","update","--name",$accountName,"--resource-group",$ResourceGroup)
    $allRegionsAgain = @($remainingRegions) + @($regionToOffline)
    foreach ($r in $allRegionsAgain) {
        $idx = [array]::IndexOf($allRegionsAgain,$r)
        $addCmd += "--locations"
        $addCmd += "regionName=$r"
        $addCmd += "failoverPriority=$idx"
        $addCmd += "isZoneRedundant=False"
    }
    Invoke-AzJson $addCmd | Out-Null
    Start-Sleep -Seconds 90
    $final = Invoke-AzJson @("cosmosdb","show","-n",$accountName,"-g",$ResourceGroup,"-o","json")
    $backAsWrite = $final.writeLocations | Where-Object { (Get-RegionSlug -LocationObj $_ -AccountName $accountName) -eq $regionToOffline }
    if ($backAsWrite) { Write-Host "CONFIRMED: '$regionToOffline' re-added and is a WRITE region again (expected for multi-master; there is no read-only role here)." }
    else { Write-Warning "'$regionToOffline' did not reappear as a write region." }
}

Write-Step "Stopping background insert job"
Stop-Job -Job $job -ErrorAction SilentlyContinue
Receive-Job -Job $job -Keep | Select-Object -Last 10
Remove-Job -Job $job -Force -ErrorAction SilentlyContinue

if ($DeleteAtEnd -eq "Yes") {
    Write-Step "Deleting account '$accountName'"
    az cosmosdb delete --name $accountName --resource-group $ResourceGroup --yes
} else {
    Write-Host "Leaving account '$accountName' in place (DeleteAtEnd=No)."
}

Write-Host "`nSCENARIO COMPLETE: Account=$accountName Mode=$mode Regions=[$($Regions -join ',')] GsiEnabled=$gsiAvailable"
