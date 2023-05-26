# .NET SDK Observability Feature Design

## Distributed Tracing (Preview)

**Source to capture operation level activities**: _Azure.Cosmos.Operation_\
**Source to capture event with request diagnostics** : _Azure-Cosmos-Operation-Request-Diagnostics_

For detail about usage of this feature, please see the [Azure Cosmos DB SDK observability](https://learn.microsoft.com/azure/cosmos-db/nosql/sdk-observability?tabs=dotnet)

```mermaid
flowchart TD
    classDef orange fill:#f96
    classDef blue fill:#6fa8dc
    subgraph ClientContextCore
        OpenTelemetryRecorderFactory --> CheckFeatureFlag{<a href='https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Fluent/CosmosClientBuilder.cs#L436'>isDistributedTracing</a> Enabled?} 
        CheckFeatureFlag --> |Yes| CreateActivity(Start an Activity or Child activity<br> with preloaded attributes) 
        CreateActivity --> HandlerPipeline{<a href='https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/docs/SdkDesign.md#handler-pipeline'>Handler Pipeline</a>}
        GetResponse --> TriggerDispose(Trigger Dispose of Diagnostic Scope)
        subgraph Dispose
            TriggerDispose --> CheckLatencyThreshold{Is high latency/errored response?}
            CheckLatencyThreshold -- Yes --> GenerateEvent(Generate <i>Warning</i> or <i>Error</i> Event With Request Diagnostics) --> StopActivity
            CheckLatencyThreshold -- No --> StopActivity   
        end
         StopActivity --> SendResponse(Send Response to Caller):::blue
    end
    OperationRequest[Operation Request]:::blue --> ClientContextCore
    subgraph Application
     OperationCall(User Application):::orange
    end
    OperationCall(User Application):::orange --> OperationRequest
    CheckFeatureFlag --> |No| HandlerPipeline 
    HandlerPipeline --> OtherLogic(Goes through TCP/HTTP calls <br> based on Connection Mode):::blue
    OtherLogic --> GetResponse(Get Response for the request)
    SendResponse --> OperationCall

```