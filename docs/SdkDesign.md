# Introduction and high level SDK concepts

## Important Cosmos DB Concepts

### Cosmos DB scaling:
https://docs.microsoft.com/azure/cosmos-db/partitioning-overview

### Cosmos DB consistency levels:
https://docs.microsoft.com/azure/cosmos-db/consistency-levels

## Try running and walking through the samples:
https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/ItemManagement/Program.cs

## High level overview of the SDK
```mermaid
flowchart
    subgraph Service
        subgraph VM1
            R1[(Replica 1)]
            R2[(Replica 2)]
            R6[(Replica 6)]
            R8[(Replica 8)]
            R12[(Replica 12)]
        end
        subgraph VM2
            R3[(Replica 3)]
            R20[(Replica 20)]
            R10[(Replica 10)]
            R17[(Replica 17)]
            
        end
        subgraph VM3
            R5[(Replica 5)]
            R14[(Replica 14)]
            R22[(Replica 22)]
            R15[(Replica 15)]
            
        end
        subgraph VM4
            R7[(Replica 7)]
            R11[(Replica 11)]
            R13[(Replica 13)]
            R25[(Replica 25)]
            
        end
        GatewayService[Gateway Service]
    end
    subgraph SDK
        Direct[Direct Store]
        PublicApi[Public API]
        PublicApi <--> ClientContext[Client Context]
        ClientContext <--> Pipeline{Handler Pipeline}
        Pipeline <-- Direct Mode --> Direct
        Direct <--> TransportClient[Transport Client]
        
        TransportClient <-- TCP --> R1
        TransportClient <-- TCP --> R5
        TransportClient <-- TCP --> R1
        TransportClient <-- TCP --> R17
        TransportClient <-- TCP --> R17
        TransportClient <-- TCP --> R25

        GatewayService <-- TCP --> R6
        GatewayService <-- TCP --> R22
        GatewayService <-- TCP --> R3
        GatewayService <-- TCP --> R7
        GatewayService <-- TCP --> R13

        Pipeline <-- Gateway mode --> GatewayStore[Gateway store]

        GatewayStore <--> HttpClient[HttpClient]
        Direct <-- Account information for location, Container properties, partition key ranges, and addresses --> HttpClient[HttpClient]
        HttpClient <-- HTTPS --> GatewayService
    end

```

## Caches the SDK maintains:

<img width="1352" alt="image" src="https://user-images.githubusercontent.com/8868107/165150887-2ccd6b82-7f09-4729-a096-afb84e6af829.png">


## Direct mode overview:
<img width="1372" alt="image" src="https://user-images.githubusercontent.com/8868107/165151366-4497ebae-c0bd-4f41-abea-afec92355c1d.png">


