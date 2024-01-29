# Sequence Diagram for .NET v3 SDK and Gateway Interactions

## Table of Contents

* [Scope.](#scope)
* [Sequence Diagram to Read Database and Collection Information.](#sequence-diagram-to-read-database-and-collection-information)
* [Sequence Diagram to Read PkRange and Address Information.](#sequence-diagram-to-read-pkRange-and-address-information)

## Scope

The scope of this sequence diagram is to capture the compute/ routing gateway interactions with the `CosmosClient` configured in `Direct` mode.

## Sequence Diagram to Read Database and Collection Information.

```mermaid
sequenceDiagram
    participant A as Cosmos .NET <br> v3 SDK
    participant B as GatewayAccountReader <br> [v3 Code]
    participant C as ClientCollectionCache <br> [v3 Code]    
    participant D as PartitionKeyRangeCache <br> [v3 Code]    
    participant E as GatewayAddressCache <br> [v3 Code]    
    participant F as GatewayStoreModel <br> [v3 Code]
    participant G as CosmosHttpClient <br> [v3 Code]
    participant H as Routing Gateway <br> [Deployed on each Region]
    A->>B: Get Database <br> Account  
    B->>G: HttpRequestMessage <br> (ResourceType = DatabaseAccount)
    G->>H: HttpRequestMessage <br> (ResourceType = DatabaseAccount)
    H-->>G: DatabaseAccount <br> Metadata Properties
    G-->>B: DatabaseAccount <br> Metadata Properties
    B-->>A: DatabaseAccount <br> Metadata Properties
    A->>C: Read Collection <br> Information
    C->>F: DocumentServiceRequest <br> (OperationType = Read) <br> (ResourceType = Collection)
    F->>G: DocumentServiceRequest
    G->>H: HttpRequestMessage <br> (ResourceType = Collection)
    H-->>G: Collection <br> Metadata Information
    G-->>F: Collection <br> Metadata Information
    F-->>C: Collection <br> Metadata Information
    C-->>A: Collection <br> Metadata Information   
```

## Sequence Diagram to Read PkRange and Address Information.

```mermaid
sequenceDiagram
    participant A as Cosmos .NET <br> v3 SDK
    participant B as GatewayAccountReader <br> [v3 Code]
    participant C as ClientCollectionCache <br> [v3 Code]    
    participant D as PartitionKeyRangeCache <br> [v3 Code]    
    participant E as GatewayAddressCache <br> [v3 Code]    
    participant F as GatewayStoreModel <br> [v3 Code]
    participant G as CosmosHttpClient <br> [v3 Code]
    participant H as Routing Gateway <br> [Deployed on each Region]
    A->>D: Get PkRange Call
    D->>F: DocumentServiceRequest <br> (OperationType = ReadFeed) <br> (ResourceType = PartitionKeyRange)
    F->>G: DocumentServiceRequest
    G->>H: HttpRequestMessage <br> (ResourceType = PartitionKeyRange)
    H-->>G: PartitionKeyRange <br> Metadata Information
    G-->>F: PartitionKeyRange <br> Metadata Information 
    F-->>C: PartitionKeyRange <br> Metadata Information  
    C-->>A: PartitionKeyRange <br> Metadata Information
    A->>E: Get Address <br> Information
    E->>F: DocumentServiceRequest <br> (OperationType = Read) <br> (ResourceType = Address)
    F->>G: DocumentServiceRequest
    G->>H: HttpRequestMessage <br> (ResourceType = Address)
    H-->>G: Partition Address <br> Metadata Information
    G-->>F: Partition Address <br> Metadata Information 
    F-->>E: Partition Address <br> Metadata Information  
    E-->>A: Partition Address <br> Metadata Information      
```