## Cosmos1000

<table>
<tr>
  <td>TypeName</td>
  <td>Cosmos503_0000TransportException</td>
</tr>
<tr>
  <td>CheckId</td>
  <td>Cosmos503_0000</td>
</tr>
<tr>
  <td>Category</td>
  <td>Connectivity</td>
</tr>
</table>

## Issue

The SDK was not able to connect to the Azure Cosmos DB service.

## Troubleshooting steps

These are the known causes for this issue.

<table>
<tr>
  <th>Possible cause</th>
  <th>Solution</th>
</tr>
<tr>
  <td>High CPU utilization. This is the most common cause. It is recommended to look at CPU utilization at 10 second intervals. If the interval is larger then CPU spikes can be missed by getting averaged in with lower values.</td>
  <td>The application should be scaled up/out.</td>
</tr>
<tr>
  <td>Socket / Port availability might be low. When running in Azure, clients using the .NET SDK can hit Azure SNAT (PAT) port exhaustion.</td>
  <td>Follow the Cosmos1001 guide.</td>
</tr>
<tr>
  <td>Creating multiple DocumentClient instances might lead to connection contention and timeout issues.</td>
  <td>Follow the [performance tips](performance-tips.md), and use a single DocumentClient instance across an entire process.</td>
</tr>
<tr>
  <td>Retries occur from throttled requests. The SDK retries internally without surfacing this to the caller. </td>
  <td>Check the [portal metrics](https://docs.microsoft.com/azure/cosmos-db/monitor-cosmos-db) for 429 throttled requests</td>
</tr>
<tr>
  <td>Hot partition key. Azure Cosmos DB distributes the overall provisioned throughput evenly across physical partitions. Check portal metrics to see if the workload is encountering a hot [partition key](https://docs.microsoft.com/azure/cosmos-db/partition-data). This will cause the aggregate consumed throughput (RU/s) to be appear to be under the provisioned RUs, but a single partition consumed throughput (RU/s) will exceed the provisioned throughput.</td>
  <td>The partition key should be changed to avoid the heavily used value.</td>
</tr>
<tr>
  <td>A high degree of concurrency can lead to contention on the channel</td>
  <td>Try to scale the application up/out.</td>
</tr>
<tr>
  <td>Large requests or responses can lead to head-of-line blocking on the channel and exacerbate contention, even with a relatively low degree of concurrency.</td>
  <td>Try to scale the application up/out.</td>
</tr>
</table>

SDK logs can be captured through [Trace Listener](https://github.com/Azure/azure-cosmosdb-dotnet/blob/master/docs/documentdb-sdk_capture_etl.md) to get more details. This can cause a significant performance impact.
