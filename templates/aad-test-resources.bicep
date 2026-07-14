// File: templates/aad-test-resources.bicep
//
// Provisions the live-account AAD (Microsoft Entra ID) test resources for the
// TestCategory=MultiRegionAad integration tests, modeled on the azure-sdk-for-python
// sdk/cosmos/test-resources.bicep pattern.
//
// The deploying identity (passed as testApplicationOid) is granted the Cosmos DB built-in
// Data Contributor data-plane role at account scope. Because the account and the identity are
// created / assigned within the SAME tenant as the deployment, the data-plane token the tests
// acquire (via DefaultAzureCredential) is accepted -- which is what avoids the cross-tenant
// rejection seen when trying to grant a build-agent MI from another tenant onto a corp account.
//
// Unlike the Python tests (which create their databases at runtime with a key), the .NET AAD
// tests use a data-plane-ONLY identity and therefore cannot create the database/container
// (that is a control-plane operation). So this template pre-creates them at deploy time.

@description('Base name used to derive the Cosmos DB account name. Must be globally unique-able when lowercased.')
param baseName string = 'dnetaad${uniqueString(resourceGroup().id)}'

@description('Location for the Cosmos DB account. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Object (principal) id to grant the Cosmos DB data-plane Data Contributor role. This is the identity the tests authenticate as (e.g. the build-agent managed identity or the deploying service principal). MUST be in the same tenant as this deployment.')
param testApplicationOid string

@description('Enable a second region (multi-region) on the account.')
param enableMultipleRegions bool = false

@description('Default account-level consistency.')
param defaultConsistencyLevel string = 'Session'

var accountName = toLower(baseName)
var databaseName = 'AadLiveTestDb'
var containerName = 'AadLiveTestContainer'

// Built-in "Cosmos DB Built-in Data Contributor" data-plane role definition id.
var dataContributorRoleDefinitionId = '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'

var singleRegion = [
  {
    locationName: location
    failoverPriority: 0
    isZoneRedundant: false
  }
]
var multiRegion = [
  {
    locationName: location
    failoverPriority: 0
    isZoneRedundant: false
  }
  {
    locationName: 'West Central US'
    failoverPriority: 1
    isZoneRedundant: false
  }
]

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
    isVirtualNetworkFilterEnabled: false
    consistencyPolicy: {
      defaultConsistencyLevel: defaultConsistencyLevel
    }
    locations: (enableMultipleRegions ? multiRegion : singleRegion)
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        paths: [
          '/pk'
        ]
        kind: 'Hash'
      }
    }
  }
}

// Grant the test identity the data-plane Data Contributor role at account scope.
resource dataPlaneRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-11-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, testApplicationOid, dataContributorRoleDefinitionId)
  properties: {
    roleDefinitionId: dataContributorRoleDefinitionId
    principalId: testApplicationOid
    scope: cosmosAccount.id
  }
}

@description('The AAD account endpoint the tests read from COSMOSDB_MULTI_REGION_AAD.')
output ACCOUNT_HOST string = cosmosAccount.properties.documentEndpoint
@description('The provisioned account name (for later cleanup).')
output ACCOUNT_NAME string = cosmosAccount.name
