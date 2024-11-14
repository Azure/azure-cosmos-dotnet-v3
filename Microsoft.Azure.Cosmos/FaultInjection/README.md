# Azure Cosmos DB .NET SDK Fault Injection Library

The Azure Cosmos DB .NET SDK Fault Injection Library allows you to simulate network issues in the Azure Cosmos DB .NET SDK. This library is useful for testing the SDK's behavior when there are network issues. Additionally, this library can help you test your own retry policies.

## Key Concepts

### `FaultInjectionRule`

To induce faults, we will introduce a new type `FaultInjectionRule`. This type will allow you to configure how the SDK fails requests. Once created, `FaultInjectionRule`s can be added to specific containers.

The `FaultInjectionRule` has two major components: a `FaultInjectionCondition` and a `FaultInjectionResult`, in addition to an `id` for each rule.

#### `FaultInjectionResult`

The `FaultInjectionResult` component of the `FaultInjectionRule` specifies what the result of the fault that is to be injected will be. The `FaultInjectionResult` component can be one of two types: `FaultInjectionServerErrorResult` or `FaultInjectionConnectionErrorResult`.

##### `FaultInjectionServerErrorResult`

This result will return a server error to the customer. `FaultInjectionServerErrorResult`. Currently, the following server error types are supported:

| Error Type | Status Code | Description |
| ---------- | ----------- | ----------- |
| `Gone` | 410:21005 | The requested resource is no longer available at the server and no forwarding address is known. This condition should be considered permanent. |
| `RetryWith` | 449:0 | The client should retry the request using the specified URI. |
| `InternalServerError` | 500:0 |  The server encountered an unexpected condition that prevented it from fulfilling the request. |
| `TooManyRequests` | 429:3200 | The client has sent too many requests in a given amount of time. |
| `ReadSessionNotAvailable` | 404:1002 | The read session is not available. |
| `Timeout` | 408:0 |  The operation did not complete within the allocated time. |
| `PartitionIsSplitting` | 410:1007 |  The partition is currently splitting. |
| `PartitionIsMigrating` | 410:1008 | The partition is currently migrating. |
| `SendDelay` | n/a | Used to simulate transient timeout/broken connections. |
| `ResponseDelay` | n/a | Used to simulate transient timeout/broken connections. |
| `ConnectionDelay` | n/a | Used to simulate high channel acquisition. |
| `ServiceUnavailable` | 503:0 |  The service is currently unavailable. |

##### `FaultInjectionConnectionErrorResult`

This result will return a connection error to the customer. `FaultInjectionConnectionErrorResult`. Currently, the following connection error types are supported:

| Error Type | Description |
| ---------- | ----------- |
| `ReceiveStreamClosed` | The connection was closed. |
| `ReceiveFailed` | The connection was reset. |

#### `FaultInjectionCondition`

The `FaultInjectionCondition` component of the `FaultInjectionRule` specifies when the fault should be injected. `FaultInjectionCondition`s can be used to limit the faults in the following ways:


