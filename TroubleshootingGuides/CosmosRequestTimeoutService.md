## CosmosRequestTimeoutService

|   |   |   |
|---|---|---|
|TypeName|CosmosRequestTimeoutService|
|Status|408_0000|
|Category|Service|

## Issue

The SDK was able to connect to the Azure Cosmos DB service, but the request timed out.

## Troubleshooting steps
These are the known causes for this issue.

### Transient
It most likely a transient issue. Check if the failure rate is violating the Cosmos DB SLA. If it is violating the SLA please contact Azure support. The application should be able to handle transient 

#### Failure rate is within Cosmos DB SLA
 The application should be able to handle transient failures.

#### Failure rate is violating the Cosmos DB SLA
Please contact Azure support. 


SDK logs can be captured through [Trace Listener](https://github.com/Azure/azure-cosmosdb-dotnet/blob/master/docs/documentdb-sdk_capture_etl.md) to get more details. This can cause a significant performance impact.
