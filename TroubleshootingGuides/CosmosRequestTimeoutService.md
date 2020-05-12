## CosmosRequestTimeoutService

|   |   |   |
|---|---|---|
|TypeName|CosmosRequestTimeoutService|
|Status|408_0000|
|Category|Service|

## Issue

The SDK was able to connect to the Azure Cosmos DB service, but the request timed out.

## Troubleshooting steps

### 1. Check the portal metrics
    Use the [Azure monitoring](https://docs.microsoft.com/azure/cosmos-db/monitor-cosmos-db) to check if the 408 request timeout was from the service.

### 2. Failure rate is within Cosmos DB SLA
    The application should be able to handle transient failures and retry when necessary.

### 3. Failure rate is violating the Cosmos DB SLA
    Please contact Azure support.