> # NOT SUPPORTED FOR PRODUCTION USAGE
> # ONLY INTERNAL, DEVELOPMENT OR EXPERIMENTAL USAGE ONLY


## Context
Distributed databases that rely on replication for high availability, low latency, or both, must make a fundamental tradeoff between the read consistency, availability, latency, and throughput. 

The linearizability of the strong consistency model is the gold standard of data programmability. But it adds a steep price from higher write latencies due to data having to replicate and commit across large distances. Strong consistency may also suffer from reduced availability (during failures) because data can't replicate and commit in every region. Eventual consistency offers higher availability and better performance, but it's more difficult to program applications because data may not be consistent across all regions (eventual reads are not guaranteed to be monotonic).


Please refer to [public documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/consistency-levels) for more details.


Many applications can benefit from having a read-write model like below

| Operation | Write-Region | Replicated-Regions |
|---|---|---|
|Write | Write high availabillity in write region <br> vs BoundedStaleness: write un-availability when bounds are violated | Eventual replication: Asynchronous non-blocking, possibly unbounded staleness <br> vs BoundedStaleness: staleness limited by bounds  |
|Read |**Single write region**: Read my writes <br> **Multiple write regions**: read-my-writes from the region of writes, otherwise eventual <br> Monotonic reads <br>No sessionToken management| **Single write region**: Eventual read <br> **Multiple write regions**: read-my-writes from the region of writes, otherwise eventual <br> Monotonic reads <br>No sessionToken management |

> ### NOTE: cross-region reads will violate the monotonic reads guarantee


## How-TO
It involves three stages

#### Create CosmosDB account with Eventual/ConsistentPrefix/Session consistency

#### Enabling/opt-in ability to upgrade consistency level 
SDK version: MA [3.35.1](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/changelog.md#-3351---2023-06-27) or [minimum recommended version](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/changelog.md#-recommended-version)

```C#
CosmosClientOptions clientOptions = new CosmosClientOptions();
var upgradeConsistencyProperty = clientOptions.GetType().GetProperty("EnableUpgradeConsistencyToLocalQuorum", BindingFlags.NonPublic | BindingFlags.Instance);
upgradeConsistencyProperty.SetValue(clientOptions, true);

CosmosClient cosmosClient = new CosmosClient(..., clientOptions);
```

#### Per request upgrade consistency to Strong 
> ###### Please note that Strong here is only used as HINT for SDK to do quorum reads 
> ###### It will not impact CosmosDB account or write consistency levels

```C#
ItemRequestOptions requestOption = new ItemRequestOptions();
requestOption.ConsistencyLevel = ConsistencyLevel.Strong;

T item = await container.ReadItemAsync<T>(docId, new PartitionKey(docPartitionKey), requestOption);
```

```C#
QueryRequestOptions requestOption = new QueryRequestOptions();
requestOption.ConsistencyLevel = ConsistencyLevel.Strong;

await container.GetItemQueryIterator<T>(queryText, continuationToken, requestOption);
```

> #### Please use Strong only for per request options as pattern
> #### Single master account possibly Strong == Bounded (**TBD**)