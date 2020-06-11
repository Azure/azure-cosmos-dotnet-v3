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

### 1. High CPU utilization (most common case)
    Cause: For optimal latency it is recommended that CPU usage should be roughly 40%. It is recommended to look at CPU utilization at 10 second intervals. If the interval is larger then CPU spikes can be missed by getting averaged in with lower values. This is more common with cross partition queries where it might do multiple connections for a single request.

    Fix: The application should be scaled up/out.

### 2. Socket / Port availability might be low
    Cause: When running in Azure, clients using the .NET SDK can hit Azure SNAT (PAT) port exhaustion.

    Fix: Follow the CosmosSNATPortExhuastion guide.

### 3. Creating multiple Client instances
    Cause: This might lead to connection contention and timeout issues.

    Fix:Follow the [performance tips](https://docs.microsoft.com/azure/cosmos-db/performance-tips), and use a single CosmosClient instance across an entire process.|

### 4. Hot partition key
    Cause: Azure Cosmos DB distributes the overall provisioned throughput evenly across physical partitions. One partition is having all of it's resources consumed while other partitions go unused. Check portal metrics to see if the workload is encountering a hot [partition key](https://docs.microsoft.com/azure/cosmos-db/partition-data). This will cause the aggregate consumed throughput (RU/s) to be appear to be under the provisioned RUs, but a single partition consumed throughput (RU/s) will exceed the provisioned throughput

    Fix: The partition key should be changed to avoid the heavily used value.

### 5. High degree of concurrency
    Cause: The application is doing a high level of conccurrency which can lead to contention on the channel

    Fix: Try to scale the application up/out.

### 6. Large requests and/or responses
    Cause: Large requests or responses can lead to head-of-line blocking on the channel and exacerbate contention, even with a relatively low degree of concurrency.

    Fix: Try to scale the application up/out.