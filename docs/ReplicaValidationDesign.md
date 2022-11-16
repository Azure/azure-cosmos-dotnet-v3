# Design Approach to Validate Backend Replicas During Service Upgrade in Direct Mode

## Table of Contents

* [Backgraound.]()
* [Proposed Solution.]()
* [Design Approach.]()
    * Outline.
    * Updated Sequence Diagram for `CosmosClient` initialization.
    * Sequence Diagram when `StoreReader` invokes the `GatewayAddressCache` to resolve addresses and leverages `AddressEnumerator` to enumerate the transport addresses.
    * State Diagram to Understand the `TransportAddressUri` Health State Transformations.
    * `Azure.Cosmos.Direct` package class diagrams.
    * `Microsoft.Azure.Cosmos` package class diagrams.
* [Pull Request with Sample Code Changes.]()
* [References.]()

## Backgraound

During an upgrade scenario in the backend replica nodes, there has been an observation of increased request latency. One of the primary reason for the latency is that, during an upgrade, a replica which is still undergoing upgrade may still be returned back to SDK, when an address refresh occurres. As of today, the incoming request will have 25% chance to hit the replica that not ready yet, therefore causing the `ConnectionTimeoutException`, which contributes to the increased latency. 

To understand the problem statement better, please take a look at the below sequence diagram which reflects the connection timeouts caused by the replica upgrade.

```mermaid
sequenceDiagram
    autonumber
    participant A as StoreReader <br> [Direct Code]
    participant B as GlobalAddressResolver <br> [v3 Code]
    participant C as GatewayAddressCache <br> [v3 Code]
    participant D as GatewayService <br> [External Service]    
    A->>+B: Request (forceRefresh - false)
    B->>+C: TryGetAddresses (forceRefresh - false)
    C->>-B: Fetch Cached Addresses
    B->>-A: Return Addresses
    A->>A: Request fails with <br> 410 GoneException
    A->>+B: Request (forceRefresh - true)
    B->>+C: TryGetAddresses (forceRefresh - true)
    C->>+D: GetServerAddresses
    D->>-C: Returns the new refreshed addresses
    Note over D: Note that the returned addresses from <br> GatewayService may still undergoing <br> the upgrade, thus and they are not in a ready state.
    C->>-B: Returns the refreshed addresses    
    B->>-A: Returns the refreshed addresses
    A-xA: Request fails with <br> 410 GoneException
    Note over A: Note that the request fails to connect to the replica <br> which causes a "ConnectionTimeoutException".
```

## Proposed Solution

The .NET SDK will track the replica endpoint health based on client side metrics, and de-prioritize the replica which were marked as - `Unhealthy`. SDK will validate the health of the replica by attempting to open RNTBD connections to the backend. When SDK refresh addresses back from gateway for a partition, **SDK will only validate the replica/s which were in `Unhealthy` status, by opening RNTBD connection requests**. This process will be completed with best effort, which means:

- If the validation can not finish within `1 min` of opening connections, the de-prioritize will stop for certain status.

- The selection of the replica will not be blocked by the validation process. To better understand this - if a request needs to be sent to `N` replicas, and if there is only `N-1` replica in good status, it will still go ahead selecting the `Nth` replica which needs validation.

- It is opt in only for now, by setting the environment variable `AZURE_COSMOS_REPLICA_VALIDATION_ENABLED` to `true`.

## Design Approach

### Outline

The basic idea for this design approach has been divited into *three* parts, which has been mentioned below in detail:

- Maintain `4` new health statuses into the `TransportAddressUri`, which are :

  - **`Connected`** (Indicates that there is already a connection made successfully to the backend replica)
  - **`Unknown`** (Indicates that the connection exists however the status is unknown at the moment)
  - **`Unhealthy Pending`** (Indicates that the connection was unhealthy previously, but an attempt will be made to validate the replica to check the current status)
  - **`Unhealthy`** (Indicates that the connection is unhealthy at the moment)

- Validate the `Unhealthy` replicas returned from the Address Cache, by attempting to open the RNTBD connection. Note that the validation task and the connection opening part has been done as a background task, so that the address resolving doesn't wait on the RNTBD context negotiation to finish.

- Leverage the `AddressEnumerator` to reorder the replicas by their health statuses. For instance, if the replica validation is enabled, the `AddressEnumerator` will reorder the replicas by sorting them in the order of **Connected/ Unknown** > **Unhealthy Pending** > **Unhealthy**. However, if the replica validation is disabled, the replicas will be sorted in the order of **Connected/ Unknown/ Unhealthy Pending** > **Unhealthy**.

Prior discussing the replica validation, it is very important to understand the changes in the flow while opening the RNTBD connections to the backend replica nodes, during the `CosmosClient` initialization. The changes in the flow are mentioned below as an updated sequence diagram.

### Updated Sequence Diagram for `CosmosClient` initialization.

```mermaid
sequenceDiagram
    participant A as CosmosClient <br> [v3 Code]
    participant B as ClientContextCore <br> [v3 Code]
    participant C as DocumentClient <br> [v3 Code]
    participant D as ServerStoreModel <br> [Direct Code]
    participant K as StoreClientFactory <br> [Direct Code]
    participant E as StoreClient <br> [Direct Code]
    participant F as ReplicatedResourceClient <br> [Direct Code]
    participant G as GlobalAddressResolver <br> [v3 Code]
    participant H as GatewayAddressCache <br> [v3 Code]
    participant J as RntbdOpenConnectionHandler <br> [Direct Code]    
    participant I as TransportClient <br> [Direct Code]
    A->>B: 1. InitializeContainerWithRntbdAsync()
    B->>C: 2. OpenConnectionsAsync()
    C->>C: 2.1. CreateStoreModel()
    C->>K: 3. CreateStoreClient(addressResolver)
    K->>E: 4. new StoreClient(addressResolver)
    E->>G: 5. SetOpenConnectionsHandler(new RntbdOpenConnectionHandler(transportClient))
    Note over E: a) Creates a new instance of RntbdOpenConnectionHandler <br> and sets it to the IAddressResolverExtension. <br> Note that the GlobalAddressResolver implements the <br> IAddressResolverExtension today. <br> b) Sets the IAddressResolverExtension to the Replicated <br> ResourceClient.
    G->>H: 6. SetOpenConnectionsHandler(openConnectionHandler)
    C->>D: 7. OpenConnectionsToAllReplicasAsync()
    D->>E: 8. OpenConnectionsToAllReplicasAsync()
    E->>F: 9. OpenConnectionsToAllReplicasAsync()
    F->>G: 10. OpenConnectionsToAllReplicasAsync()
    G->>G: 10.1 collection = collectionCache.<br>ResolveByNameAsync()
    G->>G: 10.2 partitionKeyRanges = routingMapProvider.<br>TryGetOverlappingRangesAsync(FullRange)
    G->>H: 11. OpenAsync<br>(partitionKeyRangeIdentities)
    Note over G: Resolves the collection by the <br> container link url and fetches <br> the partition key full ranges.
    H->>H: 11.1 GetServerAddressesViaGatewayAsync()
    H->>J: 12. OpenRntbdChannelsAsync()
    Note over H: Gets the transport address uris from address info <br> and invokes the RntbdOpenConnectionHandler <br> OpenRntbdChannelsAsync() method with the transport uris <br> to establish the Rntbd connection.    
    J->>I: 13. OpenConnectionAsync() <br> using the resolved transport address uris.
```

Now that we are well aware of the changes in the RNTBD open connection flow, let's leverage the changes in the replica validation flow. The below sequence diagram describes the request and response flow from `StoreReader` (present in the `Cosmos.Direct` namespace) to the `GatewayAddressCache` (present in the `Microsoft.Azure.Cosmos` namespace), during the read request to backend replica/s.

### Sequence Diagram when `StoreReader` invokes the `GatewayAddressCache` to resolve addresses and leverages `AddressEnumerator` to enumerate the transport addresses.

```mermaid
sequenceDiagram
    participant A as StoreReader <br> [Direct Code]
    participant B as AddressSelector <br> [v3 Code]
    participant C as GlobalAddressResolver <br> [v3 Code]
    participant D as AddressResolver <br> [v3 Code]
    participant E as GatewayAddressCache <br> [v3 Code]
    participant F as RntbdOpenConnectionHandler <br> [Direct Code]
    participant G as AsyncCacheNonBlocking <br> [v3 Code]
    participant H as GatewayService <br> [external service]
    participant I as AddressEnumerator <br> [Direct Code]
    participant J as TransportClient <br> [Direct Code]    
    participant K as Channel <br> [Direct Code]
    participant L as TransportAddressUri <br> [Direct Code]
    A->>A: ReadMultipleReplicaAsync()
    A->>B: 1. ResolveAllTransportAddressUriAsync(forceRefresh - false)
    B->>C: 2. ResolveAsync(forceRefresh - false)
    C->>D: 3. ResolveAsync(forceRefresh - false)
    D->>D: 4. ResolveAddressesAndIdentityAsync()
    D->>E: 5. TryGetAddressesAsync(forceRefresh - false)
    E->>G: 6. GetAsync ("singleValueInitFunc delegate", "forceRefresh - false")
    Note over L: Initial health status of a <br> TransportAddressUri is "Unknown".
    Note over E: Passes the SingleValueInitFunc delegate <br> to async nonblocking cache.
    G->>E: 7. Returns the cached addresses
    E->>D: 8. Returns the resolved addresses
    D->>C: 9. Returns the resolved addresses
    C->>B: 10. Returns the resolved addresses
    B->>A: 11. Returns the resolved addresses
    A->>A: 12. Request failes with "GoneException"
    Note over A: Sets Force Refresh <br> header to true.
    A->>A: 13. ReadMultipleReplicaAsync()
    A->>B: 14. ResolveAllTransportAddressUriAsync (forceRefresh - true)
    B->>C: 15. ResolveAsync(forceRefresh - true)
    C->>D: 16. ResolveAsync(forceRefresh - true)
    D->>D: 17. ResolveAddressesAndIdentityAsync(forceRefresh - true)
    D->>E: 18. TryGetAddressesAsync(forceRefresh - true)
    E->>G: 19. GetAsync ("singleValueInitFunc delegate", "forceRefresh - true")
    Note over E: Passes the SingleValueInitFunc delegate <br> to async nonblocking cache.
    G->>E: 20. Invokes the singleValueInitFunc delegate
    E->>E: 21. SetTransportAddressUrisToUnhealthy(currentCachedValue, failedEndpoints)
    Note over E: Sets the failed TransportAddressUris <br> to an "Unhealthy" status.    
    E->>L: 22. SetUnhealthy()
    E->>E: 23. GetAddressesForRangeIdAsync(forceRefresh - true, cachedAddresses)
    E->>H: 24. Invokes the GatewayService using GetServerAddressesViaGatewayAsync() <br> to receive new addresses.
    H->>E: 25. Receives the resolved new addresses.
    E->>E: 26. MergeAddresses<NewAddress, CachedAddress>
    Note over E: The purpose of the merge is to restore the health statuses of all the new addresses to <br> that of their recpective cached addresses, if returned same addresses.
    E->>L: 27. SetRefreshedIfUnhealthy()
    Note over E: Sets any TransportAddressUri with <br> an "Unhealthy" status to "UnhealthyPending".
    E->>E: 28. ValidateReplicaAddresses(mergedTransportAddress)
    Note over E: Validates the backend replicas, <br> if the replica validation env variable is enabled.
    E-->>F: 29. OpenRntbdChannelsAsync(mergedTransportAddress)
    Note over E: If replica validation is enabled, then validate unhealthy pending <br> replicas by opening RNTBD connections. Note that this <br> operations executes as a background task.
    F-->>J: 30. OpenConnectionAsync() <br> using the resolved transport address uris
    J-->>K: 31. OpenConnectionAsync() <br> using the resolved physical address uris
    K-->>L: 32. SetConnected()
    Note over K: Initializes and Establishes a RNTBD <br> context negotiation to the backend replica nodes.
    E->>G: 33. Returns the merged addresses to cache and store into the Async Nonblocking Cache
    G->>E: 34. Returns the resolved addresses
    E->>E: 35. ShouldRefreshHealthStatus()
    Note over E: Refresh the cache if there was an address <br> has been marked as unhealthy long enough (more than a minute) <br> and need to revalidate its status.
    E-->>G: 36. Refresh ("GetAddressesForRangeIdAsync() as the singleValueInitFunc delegate", "forceRefresh - true")
    Note over E: Note that the refresh operation <br> happens as a background task.   
    E->>D: 37. Returns the resolved addresses
    D->>C: 38. Returns the resolved addresses
    C->>B: 39. Returns the resolved addresses
    B->>A: 40. Returns the resolved transport addresses
    A->>I: 41. GetTransportAddresses("transportAddressUris", "replicaAddressValidationEnabled")
    I->>I: 42. ReorderReplicasByHealthStatus()
    Note over I: Re-orders the transport addresses <br> by their health statuses <br> Connected/Unknown >> UnhealthyPending >> Unhealthy.
    I->>A: 43. Returns the transport addresses re-ordered by their health statuses
```
### State Diagram to Understand the `TransportAddressUri` Health State Transformations.

To better understand the design, it is critical to understand the `TransportAddressUri` health state transformations. The below state diagram depicts the `TransportAddressUri` state transitions in detail.

```mermaid
    stateDiagram-v2
    [*] --> Unknown
    note right of Unknown
        Initial state of <br> a TransportAddressUri
    end note
    Unknown --> Connected: Channel Aquire <br> Successful
    Unknown --> Unhealthy: Channel Aquire <br> Failed
    Unhealthy --> Connected: Channel Aquire <br> Successful after 1 Min
    Unhealthy --> UnhealthyPending: Refresh Addresses <br> from Gateway when <br> Replica Validation <br> is enabled
    UnhealthyPending --> Unhealthy: RntbdOpenConnectionHandler - <br> Channel Aquire <br> Failed
    UnhealthyPending --> Connected: RntbdOpenConnectionHandler - <br> Channel Aquire <br> Successful
    Connected --> Unhealthy: Request failed with 410 <br> GoneException and <br> force refresh
    Connected --> [*]
```

To accomplish the above changes in the replica validation flow, below are the class diagrams and the proposed code changes in both `Azure.Cosmos.Direct` and `Microsoft.Azure.Cosmos` packages.

### `Azure.Cosmos.Direct` package class diagrams.

Introduce a new `IOpenConnectionsHandler` interface with `OpenRntbdChannelsAsync()` method. Create a new class `RntbdOpenConnectionHandler` that will eventually implement the `IOpenConnectionsHandler` interface and override the `OpenRntbdChannelsAsync()` method to establish Rntbd connections to the transport address uris. Note that, this class will also add the concurrency control logic, so that any burst of connection creation could be avoided. The below class diagram depicts the same behavior.

```mermaid
classDiagram
    IOpenConnectionsHandler --|> RntbdOpenConnectionHandler : implements
    <<Interface>> IOpenConnectionsHandler
    IOpenConnectionsHandler: +OpenRntbdChannelsAsync(IReadOnlyList~TransportAddressUri~ addresses)
    class RntbdOpenConnectionHandler{
        -TransportClient transportClient
        -SemaphoreSlim semaphore
        -int SemaphoreAcquireTimeoutInMillis
        +OpenRntbdChannelsAsync()
    }
```

Extend the `IAddressResolverExtension` interface with `SetOpenConnectionsHandler()` method. The benefits and the utilizations are provided below: 

- The `GlobalAddressResolver` can then implement the `SetOpenConnectionsHandler()` method, which will be invoked by the `StoreClient` constructor to set the `RntbdOpenConnectionHandler`. 

- The `OpenConnectionsAsync()` method present inside `GlobalAddressResolver`,  will be invoked from the `ReplicatedResourceClient` eventually. The `AddressResolver` class can simply implement the method/s and return an empty `Task`. The `GlobalAddressResolver`.`OpenConnectionsAsync()` is responsible for 

    - Resolving the collection by the database name and container link, 
    - Fetching the partition key full ranges and
    - Invoking the `GatewayAddressCache` for the preferred region, with the `RntbdOpenConnectionHandler` instance, passed by the `StoreClient`. 
    
The below class diagram depicts the same behavior.

*[Note: The `IAddressResolverExtension` was introduced to hold the new methods, which could break the existing build, if put directly into `IAddressResolver` interface. The `IAddressResolverExtension` will be removed eventually and the existing methods will be moved into `IAddressResolver` interface.]*

```mermaid
classDiagram
    IAddressResolver --|> IAddressResolverExtension : extends
    IAddressResolverExtension --|> GlobalAddressResolver : implements
    <<Interface>> IAddressResolver
    <<Interface>> IAddressResolverExtension
    IAddressResolverExtension: +OpenConnectionsAsync(string databaseName, string containerLinkUri)
    IAddressResolverExtension: +SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionHandler)    
    class GlobalAddressResolver{
      +OpenConnectionsAsync()
      +SetOpenConnectionsHandler()
    }
```

Update the method definition of `GetTransportAddresses()` present in `IAddressEnumerator` to add a new `boolean` argument `replicaAddressValidationEnabled`. This will help to choose the correct set of replica/s when replica validation is enabled or disabled. Additionally, a new private method `ReorderReplicasByHealthStatus()` will be added in `AddressEnumerator` to re-order the transport uri/s by their health status priority. The below class diagram depicts the same changes.

```mermaid
classDiagram
    IAddressEnumerator --|> AddressEnumerator : implements
    <<Interface>> IAddressEnumerator
    IAddressEnumerator: +GetTransportAddresses(IReadOnlyList~TransportAddressUri~ transportAddressUris, Lazy~HashSet~TransportAddressUri~~ failedEndpoints, bool replicaAddressValidationEnabled) IEnumerable~TransportAddressUri~
    class AddressEnumerator{
      +GetTransportAddresses()
      -ReorderReplicasByHealthStatus(IReadOnlyList~TransportAddressUri~ transportAddressUris, Lazy~HashSet~TransportAddressUri~~ failedEndpoints, bool replicaAddressValidationEnabled) IEnumerable~TransportAddressUri~
    }
```

This class will be updated eventually, with critical set of changes required for the replica validation workstream. It will introduce an enum `HealthStatus` with `4` new health statuses - `Connected`, `Unknown`, `UnhealthyPending` and `Unhealthy` to re-order the replicas with their status priorities. The setters will help to capture the correct health state of the `TransportAddressUri` at any given point of time. The below class diagram depicts the same behavior.

```mermaid
classDiagram   
    class TransportAddressUri {
        -DateTime? lastUnknownTimestamp
        -DateTime? lastUnhealthyPendingTimestamp
        -DateTime? lastUnhealthyTimestamp
        -HealthStatus healthStatus
        -ReaderWriterLockSlim healthStatusLock
        +SetConnected()
        +SetRefreshedIfUnhealthy()
        +SetHealthStatus(HealthStatus status, bool forceSet)
        +GetHealthStatus() HealthStatus
        +GetEffectiveHealthStatus() HealthStatus
        +ShouldRefreshHealthStatus() bool
    }

    class HealthStatus {
        <<enumeration>>
        Connected : 100
        Unknown : 200
        UnhealthyPending : 300
        Unhealthy : 400
    }
```

### `Microsoft.Azure.Cosmos` package class diagrams.

Extend the `IAddressCache` interface with `SetOpenConnectionsHandler()` method. This method should take an instance of the `RntbdOpenConnectionHandler` (which implements the `IOpenConnectionsHandler`) and sets it to a private field for later usage, to establish the Rntbd connectivity to the backend replica nodes.

```mermaid
classDiagram
    IAddressCache --|> GatewayAddressCache : implements
    <<Interface>> IAddressCache
    IAddressCache: +SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionsHandler)
    class GatewayAddressCache {
        -IOpenConnectionsHandler openConnectionsHandler
        -string replicaValidationVariableName
        +SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionsHandler)
        -MergeAddresses(PartitionAddressInformation newAddresses, PartitionAddressInformation cachedAddresses) PartitionAddressInformation
        -ValidateReplicaAddresses(IReadOnlyList~TransportAddressUri~ addresses)
    }
```

Add a new method `Refresh()` into the `AsyncCacheNonBlocking` to force refresh the address cache on demand. Note that `Refresh()` is used to force refresh any `Unhealthy` replica nodes, which has been in `Unhealthy` state for more than `1` minute. That way, any `Unhealthy` replica nodes will be back into the validation pool, which will eventually be useful to avoid any skewed replica selection. 

```mermaid
classDiagram   
    class AsyncCacheNonBlocking~TKey, TValue~ {
        +Task Refresh(TKey key, Func~TValue, Task~TValue~~ singleValueInitFunc)
    }
```

## Pull Request with Sample Code Changes

Here is the link to a sample PR which provides an overview of the incoming changes.

## References

- [Mermaid Documentation.](https://mermaid-js.github.io/mermaid/#/)
- [Upgrade Resiliency Tasks List.](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/3409)
- [Design Document to Utilize RNTBD Context Negotiation During `CosmosClient` Initialization.](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/3442)