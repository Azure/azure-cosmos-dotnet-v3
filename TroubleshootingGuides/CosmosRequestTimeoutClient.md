## CosmosRequestTimeoutClient

|   |   |   |
|---|---|---|
|TypeName|CosmosRequestTimeoutClient|
|Status|408_0000|
|Category|Connectivity|


## Issue

The SDK was not able to connect to the Azure Cosmos DB service.

## Troubleshooting steps
These are the known causes for this issue.

### High CPU utilization
This is the most common cause. It is recommended to look at CPU utilization at 10 second intervals. If the interval is larger then CPU spikes can be missed by getting averaged in with lower values.

#### Solution:
The application should be scaled up/out.

### Socket / Port availability might be low
When running in Azure, clients using the .NET SDK can hit Azure SNAT (PAT) port exhaustion.

#### Solution:
Follow the CosmosSNATPortExhuastion guide.

### Creating multiple Client instances
This might lead to connection contention and timeout issues.

#### Solution:
Follow the [performance tips](https://docs.microsoft.com/azure/cosmos-db/performance-tips), and use a single CosmosClient instance across an entire process.|

### Hot partition key
Azure Cosmos DB distributes the overall provisioned throughput evenly across physical partitions. One partition is having all of it's resources consumed while other partitions go unused. Check portal metrics to see if the workload is encountering a hot [partition key](https://docs.microsoft.com/azure/cosmos-db/partition-data). This will cause the aggregate consumed throughput (RU/s) to be appear to be under the provisioned RUs, but a single partition consumed throughput (RU/s) will exceed the provisioned throughput

#### Solution:
The partition key should be changed to avoid the heavily used value.

### High degree of concurrency
The application is doing a high level of conccurrency which can lead to contention on the channel

#### Solution:
Try to scale the application up/out.

### Large requests and/or responses
Large requests or responses can lead to head-of-line blocking on the channel and exacerbate contention, even with a relatively low degree of concurrency.

#### Solution:
Try to scale the application up/out.

SDK logs can be captured through [Trace Listener](https://github.com/Azure/azure-cosmosdb-dotnet/blob/master/docs/documentdb-sdk_capture_etl.md) to get more details. This can cause a significant performance impact.
