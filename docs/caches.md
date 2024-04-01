# Caches conceptual model

![image](https://user-images.githubusercontent.com/6880899/199167007-bcc054c3-ecb1-4469-ba7d-eae52362e9cd.png)


- CollectionCache: Dictionary<CollectionName/Rid, CollectionProperties>
- CollectionRoutingMap: Single collection PartitionKeyRanges map
- PartitionKeyRangeCache: Dictionary<CollectionName/Rid, CollectionRoutingMap>
- GlobalPartitionEndpointManager: Per partition override state. Every request will flow through
    -   Today GlobalEndpointManager is at region scope only and doesn't look at the partition
    -   Ideal abstraction is to fold it into GlobalEndpointManager --> extra hash computation
        - Posible to refactor direct code and flow HashedValue down stream (more contract work with direct package)
- AddressResolver: It does use IAddressCache (Above diagram missing it)


```mermaid
flowchart LR
    subgraph CDB_account
        subgraph CDB_Account_Endpoint
            CDB_EP[[CosmosDB-Account/Endpoint]]
        end

        subgraph CDB_Account_Region1
            CDBR1_GW[[CosmosDB-Account/Gateway/Region1]]
            CDBR1_BE[[CosmosDB-Account/Backend/Region1]]
        end
        subgraph CDB_Account_RegionN
            CDBRN_GW[[CosmosDB-Account/Gateway/Region1]]
            CDBRN_BE[[CosmosDB-Account/Backend/RegionN]]
        end
    end
    subgraph AddressCaches
        GAC1[<a href='https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Routing/GatewayAddressCache.cs'>GatewayAddressCache/R1</a>]
        GACN[<a href='https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Routing/GatewayAddressCache.cs'>GatewayAddressCache/RN</a>]

        GAC1 --> |NonBlockingAsyncCache| CDBR1_GW
        GAC1 -.-> CDBR1_BE
        GACN -->  |NonBlockingAsyncCache| CDBRN_GW
        GACN -.-> CDBRN_BE
    end
```

## Sequence of interaction

```mermaid
sequenceDiagram
    GlobalAddressResolver->>GlobalEndpointManager: ResolveServiceEndpoint(DocumentServiceRequest)
    GlobalEndpointManager-->>GlobalAddressResolver: URI (ServingRegion)
    GlobalAddressResolver->>GlobalAddressResolver: GetEndpointCache(ServingRegion).AddressResolver

    critical RegularAddressResolution(Implicit contract of dsr.RequestContext.ResolvedPartitionKeyRange population)
        GlobalAddressResolver->>AddressResolver: ResolveAsync(DocumentServiceRequest)
        AddressResolver-->>GlobalAddressResolver: PartitionAddressInformation
    end

    critical AnyPerpartitionOverrides
        GlobalAddressResolver->>GlobalPartitionEndpointManager: TryAddPartitionLevelLocationOverride(DocumentServiceRequest)
        GlobalPartitionEndpointManager-->>GlobalAddressResolver: (bool, DSR.RequestContext.RouteToLocation)

        option YES
            GlobalAddressResolver->>GlobalEndpointManager: ResolveServiceEndpoint(DocumentServiceRequest)
            GlobalEndpointManager-->>GlobalAddressResolver: URI (ServingRegion)
    end
    GlobalAddressResolver->>GlobalAddressResolver: GetEndpointCache(ServingRegion)
    GlobalAddressResolver->>AddressResolver: ResolveAsync(DocumentServiceRequest)
    AddressResolver-->>GlobalAddressResolver: PartitionAddressInformation
```