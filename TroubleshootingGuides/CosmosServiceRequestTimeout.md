## CosmosServiceRequestTimeout

| Http Status Code | Name | Category |
|---|---|---|
|408|CosmosServiceRequestTimeout|Service|

## Issue

Cosmos DB returned a request with 408 request timeout

## Troubleshooting steps

### 1. Check the SLA
The customer should check the [Azure Cosmos DB monitoring](https://docs.microsoft.com/en-us/azure/cosmos-db/monitor-cosmos-db) to check if the number 408s violate the Cosmos DB SLA.

#### Solution 1: It did not viloate the Cosmos DB
The application should handle this scenario and retry on these transient failures.

#### Solution 2: It did viloate the Cosmos DB:
Please contact Azure Support: http://aka.ms/azure-support