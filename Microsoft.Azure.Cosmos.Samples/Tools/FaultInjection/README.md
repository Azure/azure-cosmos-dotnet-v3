# Azure Cosmos DB Fault Injection Library for .NET

The Azure Cosmos DB Fault Injection Library for .NET is a library that allows you to inject faults into the Azure Cosmos DB .NET SDK. This library is designed to help you test the resiliency of your application when using Azure Cosmos DB. The library is built on top of the [Azure Cosmos DB SDK for .NET](https://github.com/Azure/azure-cosmos-dotnet-v3)

## Getting Started

The Azure Cosmos DB Fault Injection Library for .NET is available as a NuGet package. You can install it

### Prerequisites

- [.NET 6.0](https://dotnet.microsoft.com/download/dotnet/5.0)
- An active [Azure Cosmos DB Account](https://docs.microsoft.com/en-us/azure/cosmos-db/create-cosmosdb-resources-portal). Alternativly you can use the [Azure Cosmos DB Emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator) for local development.

## Key Concepts

The Azure Cosmos DB Fault Injection Library for .NET is a library that allows you to inject faults into the Azure Cosmos DB .NET SDK. The faults come in the form of Fault Injection Rules. A Fault Injection Rules is made up of two main parts: A Fault Injection Condition which determines when the fault should be injected and a Fault Injection Result which determines what type of fault should be injected.

### Fault Injection Conditions

Fault Injection Conditions are uses as filters to determine whether a request should have a Fault Injection Result applied to it.

| Condition | Description |
| --- | --- |
| OperationType | Allows you to only have rules injected on specific operations. By default, rules will be applied to all operation types. |
| ConnectionType | Allows errors to be injected on only Direct or Gateway connections. By default, rules will be applied to all connection types. |
| Region | Allows you to only have rules injected on specific regions. By default, rules will be applied to all regions. |
| Endpoint | Allows you to only have rules injected on specific endpoints. By default, rules will be applied to all endpoints. |

#### Endpoint

By including a `FaultInjectionEndpoint` in your `FaultInjectionCondition` you can cause failues on a specific set of physical addresses that your fault injection rule will be applied to. When creating a `FaultInjectionEndpoint` you must provide the name of the database, the name of the container, and the `FeedRange` that you want to target. You additionally have the option of specifying the number of physical addresses that can be appied to the final rule by setting the `replicaCount` parameter and the ability to specify whether to include the primary replica by setting the `includePrimary` parameter. By default all replicas including the primary will have the rule applied to them.

### Fault Injection Results

`FaultInjectionResults` are the actions that are taken when a `FaultInjectionCondition` is met. There are currently two types of `FaultInjectionResults`:

#### Connection Results

`ConnectionErrorResults` are used to simulate network errors in `Direct` mode. There are two types of `ConnectionErrorResults`: `ReceiveStreamClosed`, which emulates a connection close becuse of a recieved stream close; and `ReceiveFailed`, which emulates a connection close because of a failure to recieve a response from the service.

For connection error results, you able to specify the interval at which the error should be injected by setting the `interval` parameter. The `interval` parameter is a `TimeSpan` that represents the time between each error. By default, the `interval` is set to 0.

You are also able to specify the aproxomate percent of connections that will be closed each time the rule is applied by setting the 'thresholdPercentage' parameter. The `thresholdPercentage` is a `double` that represents the percentage of connections that will be closed each time the rule is applied. By default, the `thresholdPercentage` is set to 1.0, meaning that all connections will be closed. Each time the rule is applied, the library will randomly select a subset of connections to close based on the `thresholdPercentage`.

***Note***: `ConnectionErrorResults` are only valid for `Direct` mode connections.

#### Server Results

`FaultInjectionServerErrorResults` are used to simulate errors comming from the server. There are several types of server errors the library can simulate:

| Error Type | Status Code | Description |
| --- | --- | --- |
| Gone | 410 | Gone error from server |
| RetryWith | 449 | RetryWith error from server |
| InternalServerError | 500 | InternalServerError error from server |
| TooManyRequests | 429 | TooManyRequests error from server |
| ReadSessionNotAvailable | 404-1002 | ReadSessionNotAvailable error from server |
| Timeout | 408 | Request timeout |
| PartitionIsSplitting | 401-1007 | PartitionIsSplitting error from server |
| PartitionIsMigrating | 401-1008 | PartitionIsMigrating error from server |
| ResponseDelay | N/A | Used to simulate a transient timeout/broken connection over a request timeout, the request will be sent out before the delay is applied. |
| SendDelay | N/A | Used to simulate a transient timeout/broken connection over a request timeout, the request will be sent out after the delay is applied. |
| ConnectionDelay | N/A | Used to simulate high channel acquisiton. When over a connection timeout, can simulate connectionTimeoutException. Only applicable for Direct mode. |
| ServiceUnavailable | 503 | ServiceUnavailable error from server |

When using `ResponseDelay`, `SendDelay`, or `ConnectionDelay` you must also specify how long the delay should be by setting the `delay` parameter. The `delay` parameter is a `TimeSpan` that represents the time the delay lasts.

For sever error results you can also specify the number of times a rule can be applied for a single operation by setting the `times` parameter, by default there is no limit.

Server error results also allows you to supress service requests by setting the `suppressServiceRequest` parameter. If not specified, the `suppressServiceRequest` parameter is set to `false`.

Finally, you can specify the percent of requests that will be affected by the rule by setting the `applyPercentage` parameter. The `applyPercentage` is a `double` that represents the percentage of requests that will be affected by the rule. By default, the `applyPercentage` is set to 1.0, meaning that all requests will be affected. Each time the rule can be applied, the library will decide whether the rule can be applied based on `applyPercentage`.

### Fault Injection Rules

When creating a fault injection rule, other than specifying a `FaultInjectionCondition` and `FaultInjectionResult`, you must also specify an unique `RuleId` that will be used to identify the rule. Fault Injection rules can also have a `duration` and `startDelay` which will determine how long the rule will be applied and how long the rule will wait before initially being applied respectivly. By default, the `duration` is set to `TimeSpan.MaxValue` and the `startDelay` is set to 0. You can also specify a `hitLimit` to a fault injection rule which will determine how many times the rule can be applied in total. By default, the `hitLimit` is set to `int.MaxValue`.

## Use Cases

The Azure Cosmos DB Fault Injection Library for .NET is designed to help you test the resiliency of your application when using Azure Cosmos DB. You can use the library to simulate network errors and server errors to see how your application behaves under different conditions.

## Examples

### Creating a Fault Injection Rule

```csharp
// Create a Fault Injection Rule that will inject a Gone error on all read operations in the East US region
// To create a rule use the Fault Injection Rule Builder
FaultInjectionRule readGoneRule = new FaultInjectionRuleBuilder(
    id: "DirectModeReadGoneRule",
    condition: new FaultInjectionConditionBuilder()
        .WithOperationType(OperationType.Read)
        .WithConnectionType(FaultInjectionConnectionType.Direct)
        .Build(),
    result: new FaultInjectionServerErrorResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
        .WithTimes(1)
        .Build())
    .WithDuration(TimeSpan.FromMinutes(5))
    .Build();

FaultInjectionRule eastUsTimeoutRule = new FaultInjectionRuleBuilder(
    id: "EastUsTimeoutRule",
    condition: new FaultInjectionConditionBuilder()
        .WithRegion("East US")
        .Build(),
    result: new FaultInjectionServerErrorResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
        .WithApplyPercentage(0.75)
        .Build())
    .WithDuration(TimeSpan.FromMinutes(5))
    .Build();

// Next add all rules you want applied to create a FaultInjector Instance
List<FaultInjectionRule> rules = new List<FaultInjectionRule> { readGoneRule, eastUsTimeoutRule };
FaultInjector faultInjector = new FaultInjector(rules);

// Configure your CosmosClient
CosmosClientOptions clientOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Direct,
    ApplicationPreferredRegions = new List<string>() { "East US", "Central US", "West US" },
};

// Use the FaultInjector and your client options to create a CosmosClient with FaultInjection
CosmosClient clinet = new CosmosClient(
    connectionString: "<YourConnectionString>", 
    clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

```

### High Channell Acquisiton

```csharp
FaultInjectionRule serverConnectionDelayRule = new FaultInjectionRuleBuilder(
    id: "ServerConnectionDelayRule",
    condition: new FaultInjectionConditionBuilder()
        .WithConnectionType(FaultInjectionConnectionType.Direct)
        .Build(),
    result: new FaultInjectionServerErrorResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
        .WithDelay(TimeSpan.FromSeconds(6)) //Default connection timeout is 5 seconds
        .Times(1)
        .Build())
    .WithDuration(TimeSpan.FromMinutes(5))
    .Build();
```

### Simulate a Regional Outage

If an outage occurs in a region, what will usually first happen is that your direct mode calls with start to return `Gone` exceptions. This will then trigger an address refresh on the client which will then send a call to the Gateway. This flow can be simulated by the fault injection libary to see how your application behaves when this happens.

```csharp

FaultInjectionRule outageStarts = new FaultInjectionRuleBuilder(
    id: "OutageStarts",
    condition: new FaultInjectionConditionBuilder()
        .WithConnectionType(FaultInjectionConnectionType.Direct)
        .WithRegion("East US")
        .Build(),
    result: new FaultInjectionServerErrorResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
        .Build())
    .WithDuration(TimeSpan.FromMinutes(5))
    .WithStartDelay(TimeSpan.FromSeconds(60))
    .Build(); 

FaultInjectionRule startGatewayFailure = new FaultInjectionRuleBuilder(
    id: "StartGatewayFailure",
    condition: new FaultInjectionConditionBuilder()
        .WithConnectionType(FaultInjectionConnectionType.Gateway)
        .WithRegion("East US")
        .Build(),
    result: new FaultInjectionServerErrorResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.HttpRequestException)
        .Build())
    .WithDuration(TimeSpan.FromMinutes(5))
    .Build().Disable();

List<FaultInjectionRule> rules = new List<FaultInjectionRule> { outageStarts, startGatewayFailure };

// Configure your CosmosClient
CosmosClientOptions clientOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Direct,
    ApplicationPreferredRegions = new List<string>() { "East US", "Central US", "West US" },
};

// Use the FaultInjector and your client options to create a CosmosClient with FaultInjection
CosmosClient clinet = new CosmosClient(
    connectionString: "<YourConnectionString>", 
    clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

this.OutageStartListiner(outageStart, startGateWayFailure);

//Start Workload
```

```csharp
private async Task OutageStartListiner(
    FaultInjectionRule outageStart, 
    FaultInjectionRule startGateWayFailure)
{
    await Task.Delay(outageStart.GetStartDelay());
    while (true)
    {
        if (outageStart.GetHitCount() > 0)
        {
            startGateWayFailure.Enable();
            break;
        }
    }
}
```

## Troubleshooting

## Next Steps

## Contributing
