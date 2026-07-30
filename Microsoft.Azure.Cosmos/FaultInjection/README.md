# Azure Cosmos DB .NET SDK Fault Injection Library

The Azure Cosmos DB .NET SDK Fault Injection Library allows you to simulate network issues in the Azure Cosmos DB .NET SDK. This library is useful for testing the SDK's behavior when there are network issues. Additionally, this library can help you test your own retry policies. Note that this library is not intended for use in production environments and should only be used for testing purposes. This **library** is currently in preview, and breaking changes may occur.

> **Contributing / maintaining this library?** See [DEVELOPMENT.md](./DEVELOPMENT.md) for how to build the project, run its integration tests inside this repository, cut a release, and troubleshoot common issues.

## Installation

The library ships as the [`Microsoft.Azure.Cosmos.FaultInjection`](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.FaultInjection) NuGet package. Because it is currently in beta, install it with the `--prerelease` flag (or pin an explicit prerelease version):

```bash
# Latest prerelease
dotnet add package Microsoft.Azure.Cosmos.FaultInjection --prerelease

# Or pin an explicit version
dotnet add package Microsoft.Azure.Cosmos.FaultInjection --version 1.0.0-beta.1
```

The package brings in a compatible `Microsoft.Azure.Cosmos` SDK version as a dependency. It targets `net6.0`.

## Key Concepts

### `FaultInjectionRule`

To induce faults, we will introduce a new type: `FaultInjectionRule`. This type will allow you to configure how the SDK fails requests. Once created, `FaultInjectionRule`s can be added to specific containers.

The `FaultInjectionRule` has two major components: a `FaultInjectionCondition` and a `FaultInjectionResult`, in addition to an `id` for each rule.

#### `FaultInjectionResult`

The `FaultInjectionResult` component of the `FaultInjectionRule` specifies what the result of the fault that is to be injected will be. The `FaultInjectionResult` component can be one of two types: `FaultInjectionServerErrorResult` or `FaultInjectionConnectionErrorResult`.

##### `FaultInjectionServerErrorResult`

This result will return a server error to the customer: `FaultInjectionServerErrorResult`. Currently, the following server error types are supported:

| Error Type              | Status Code  | Description                                                                 |
| ----------------------- | ------------ | --------------------------------------------------------------------------- |
| `Gone`                  | 410:21005    | The requested resource is no longer available at the server and no forwarding address is known. This condition should be considered permanent. |
| `RetryWith`             | 449:0        | The client should retry the request using the specified URI.                |
| `InternalServerError`   | 500:0        | The server encountered an unexpected condition that prevented it from fulfilling the request. |
| `TooManyRequests`       | 429:3200     | The client has sent too many requests in a given amount of time.             |
| `ReadSessionNotAvailable`| 404:1002    | The read session is not available.                                          |
| `Timeout`               | 408:0        | The operation did not complete within the allocated time.                   |
| `PartitionIsSplitting`  | 410:1007     | The partition is currently splitting.                                       |
| `PartitionIsMigrating`  | 410:1008     | The partition is currently migrating.                                       |
| `SendDelay`             | n/a          | Will inject a delay to the request before it is sent to the backend.         |
| `ResponseDelay`         | n/a          | Will inject a delay to the request after a response is received from the backend before returning the result. |
| `ConnectionDelay`       | n/a          | Used to simulate high channel acquisition.                                  |
| `ServiceUnavailable`    | 503:0        | The service is currently unavailable.                                       |
| `DatabaseAccountNotFound`| 403:1008    | The database account was not found. Gateway mode only (rejected for Direct connection type). |
| `LeaseNotFound`         | 410:1022     | The lease required to serve the request was not found.                      |
| `Unauthorized`          | 401:0        | The request is not authorized.                                              |
| `AadTokenRevoked`       | 401:5013     | The Microsoft Entra ID (AAD) token has been revoked.                        |

> **Mode applicability:** `Gone` is only injected on **Direct** mode calls and is rejected when the rule targets the `Gateway` connection type. `DatabaseAccountNotFound` is the inverse — it is only valid for **Gateway** mode. See the validation in `FaultInjectionRuleBuilder`.

##### `FaultInjectionConnectionErrorResult`

This result will return a connection error to the customer: `FaultInjectionConnectionErrorResult`. Currently, the following connection error types are supported:

| Error Type              | Description                                                            |
| ----------------------- | ---------------------------------------------------------------------- |
| `ReceiveStreamClosed`    | The connection was closed.                                             |
| `ReceiveFailed`          | The connection was reset.                                              |

##### Other `FaultInjectionResult` Properties

When creating a **server error** `FaultInjectionResult` (via `FaultInjectionServerErrorResultBuilder`), you can also specify the following properties:

| Property       | Builder method | Description |
| -------------- | -------------- | ----------- |
| `Times`        | `WithTimes(int)` | Specifies how many times to inject the fault for a single operation (retries within one logical operation). By default, there is no limit. |
| `Delay`        | `WithDelay(TimeSpan)` | Specifies how long to delay the fault injection. Only applicable for `SendDelay`, `ResponseDelay`, and `ConnectionDelay` error types (and required for them). |
| `InjectionRate`| `WithInjectionRate(double)` | Specifies how often the rule is applied when applicable to an operation, in the range `(0, 1]`. By default, the rate is 100% (`1.0`). |
| `SuppressServiceRequest` | `WithSuppressServiceRequest(bool)` | When `true`, the real request is never sent to the backend and the injected fault is returned immediately. When `false`, the request is still sent (useful for delay-style faults). |

When creating a **connection error** `FaultInjectionResult` (via `FaultInjectionConnectionErrorResultBuilder`), the following properties apply instead:

| Property    | Builder method | Description |
| ----------- | -------------- | ----------- |
| `Interval`  | `WithInterval(TimeSpan)` | How often the connection error is injected. Must be greater than zero. |
| `Threshold` | `WithThreshold(double)` | The percentage of established connections impacted by the fault, in the range `(0, 1]`. Default is `1.0` (100%). |

#### `FaultInjectionCondition`

The `FaultInjectionCondition` component of the `FaultInjectionRule` specifies when the fault should be injected. `FaultInjectionCondition`s can be used to limit the faults in the following ways:

| Condition     | Description |
| ------------- | ----------- |
| OperationType | The fault will only be injected if the operation type matches the specified `FaultInjectionOperationType`. If not set, it will inject on all requests. |
| ConnectionType | The fault will only be injected if the connection type matches the specified `FaultInjectionConnectionType`. If not set, it will inject on all connection types. |
| Region        | The fault will only be injected if the region matches the specified region. If not set, it will inject on all regions. |
| Endpoint      | The fault will only be injected if the endpoint matches the specified endpoint. This can be used to target specific replicas and partitions. If not set, it will inject on all endpoints. |

##### `FaultInjectionOperationType`

The `FaultInjectionOperationType` specifies the type of operation that the fault should be injected on. The following operation types are supported:

| Operation Type |
| -------------- |
| `ReadItem` |
| `QueryItem` |
| `CreateItem` |
| `UpsertItem` |
| `ReplaceItem` |
| `DeleteItem` |
| `PatchItem` |
| `Batch` |
| `ReadFeed` |
| `MetadataContainer` |
| `MetadataDatabaseAccount` |
| `MetadataPartitionKeyRange` |
| `MetadataRefreshAddresses` |
| `MetadataQueryPlan` |
| `All` |

> The `Metadata*` operation types target the SDK's control-plane / metadata requests (container, database-account, partition-key-range, address refresh, and query-plan calls). In Gateway mode, metadata requests only support a subset of server error types (`TooManyRequests`, `ResponseDelay`, `SendDelay`, `DatabaseAccountNotFound`, `ServiceUnavailable`, `InternalServerError`, `LeaseNotFound`, `Unauthorized`, `AadTokenRevoked`).

##### `FaultInjectionConnectionType`

The `FaultInjectionConnectionType` specifies the type of connection that the fault should be injected on. The following connection types are supported:

| Connection Type |
| --------------- |
| `Direct` |
| `Gateway` |
| `All` |

The `Gateway` connection type also supports connection to the `ThinProxy`. 


#### Other `FaultInjectionRule` Properties

When creating a `FaultInjectionRule`, you can also specify the following properties:

| Property       | Builder method | Description |
| -------------- | -------------- | ----------- |
| `Duration`     | `WithDuration(TimeSpan)` | How long the rule is valid for. The duration is measured from the time the rule is created. By default, the rule is effective until the application ends. |
| `StartDelay`   | `WithStartDelay(TimeSpan)` | How long to wait after rule creation before the rule starts injecting faults. |
| `HitLimit`     | `WithHitLimit(int)` | The maximum number of times the rule can be applied across all operations. Must be greater than 0. |
| `IsEnabled`    | `IsEnabled(bool)` | Whether the rule is enabled. A disabled rule is never applied. Rules are enabled by default and can be toggled multiple times. |


### `FaultInjector`

The `FaultInjector` is a class that allows you to inject faults into the Azure Cosmos DB .NET SDK. The `FaultInjector` is created with a list of `FaultInjectionRule`s. Once created, the `FaultInjector` can be passed to the `CosmosClient` constructor to enable fault injection.

After conducting the tests, you can use the `FaultInjector` to get the `FaultInjectionApplicationContext`, which allows you to get the following:

- Given a rule id, get the time and activity id of all requests that were affected by the rule.
- Given an activity id, get the rule id that affected the request.

This can be useful for debugging and understanding which rules are affecting which requests.

```c#
FaultInjector faultInjector = new FaultInjector(rules);

// ... run your workload against a client configured with faultInjector ...

// Get the rule id that affected a specific request (by activity id)
string? ruleId = faultInjector.GetFaultInjectionRuleId(activityId);

// Get the full application context to inspect every rule hit
FaultInjectionApplicationContext? context = faultInjector.GetApplicationContext();
```

## Examples

The following examples demonstrate creating `FaultInjectionRule`s for some of the most common scenarios for using the Azure Cosmos DB .NET SDK Fault Injection Library. Additionally, there is also an example of how to create a `CosmosClient` that has fault injection enabled.

### High Latency in a Single Region

This rule will inject a 4-second delay in the response for read item operations 5 seconds after client creation for 30 seconds.

```c#
FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    id: "HighLatencyRule",
    condition: new FaultInjectionConditionBuilder()
        .WithOperationType(FaultInjectionOperationType.ReadItem)
        .Build(),
    result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
        .WithDelay(TimeSpan.FromSeconds(4))
        .Build())
    .WithDuration(TimeSpan.FromSeconds(30))
    .WithStartDelay(TimeSpan.FromSeconds(5))
    .Build();
```

### High Channel Acquisition

```c#
FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    id: "HighChannelAcquisitionRule",
    condition: new FaultInjectionConditionBuilder()
        .WithConnectionType(FaultInjectionConnectionType.Direct) // Only inject on direct mode connections
        .Build(),
    result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
        .WithDelay(TimeSpan.FromSeconds(6)) // Default connection timeout is 5 seconds
        .Build())
    .Build();
```

### Server Return Gone

This rule will return a 410 Gone error for read item operations. `Gone` is a Direct-mode error; a rule using the `Gone` result is rejected if it targets the `Gateway` connection type.

```c#
FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    id: "GoneRule",
    condition: new FaultInjectionConditionBuilder()
        .WithOperationType(FaultInjectionOperationType.ReadItem)
        .Build(),
    result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
        .Build())
    .Build();
```

### Server Unavailable

This rule will return a 503 Service Unavailable error for 10% of all operations in the East US region. Note that the injection rate is set on the **result** builder (`WithInjectionRate`), not the rule builder.

```c#
FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    id: "ServiceUnavailableRule",
    condition: new FaultInjectionConditionBuilder()
        .WithRegion("East US")
        .Build(),
    result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
        .WithInjectionRate(0.1)
        .Build())
    .Build();
```

### Random Connection Closed

This rule will randomly close 30% of connections for all operations every 5 seconds for 30 seconds.

```c#
FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    id: "RandomConnectionClosedRule",
    condition: new FaultInjectionConditionBuilder()
        .WithEndpoint(new FaultInjectionEndpointBuilder("dbName", "containerName", feedRange).Build())
        .Build(),
    result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionConnectionErrorType.ReceiveStreamClosed)
        .WithInterval(TimeSpan.FromSeconds(5)) // Inject every 5 seconds
        .WithThreshold(0.3)
        .Build())
    .WithDuration(TimeSpan.FromSeconds(30))
    .Build();
```

### Create a `CosmosClient` with Fault Injection Enabled

```c#

List<FaultInjectionRule> rules = new List<FaultInjectionRule>
{
    // Add rules here
};

FaultInjector faultInjector = new FaultInjector(rules);

```

Once you have created the `FaultInjector`, you can pass it to the `CosmosClient` via the `CosmosClientBuilder`:

```c#

CosmosClient client = new CosmosClientBuilder("connectionString")
    .WithFaultInjection(faultInjector)
    .Build();

```

or via `CosmosClientOptions`:

```c#

CosmosClientOptions options = new CosmosClientOptions
{
    FaultInjector = faultInjector
};

CosmosClient client = new CosmosClient("connectionString", options);

```

## Related documentation

- [DEVELOPMENT.md](./DEVELOPMENT.md) — build, test, release, and maintenance guide for contributors.
- [changelog.md](./changelog.md) — release notes for the `Microsoft.Azure.Cosmos.FaultInjection` package.
- [Repository CONTRIBUTING guide](../../CONTRIBUTING.md) — changelog rules, PR title format, and general contribution workflow.

