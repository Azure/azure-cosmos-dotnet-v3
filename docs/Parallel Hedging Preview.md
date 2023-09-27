# Parallel Hedging Preview

## Parallel Hedging

When Building a new `CosmosClient` there will be an option to include Parallel hedging in that client.

```csharp
CosmosClient client = new CosmosClientBuilder("connection string")
    .WithAvailabilityStrategy(
        type: AvailabilityStrategy.ParallelHedging,
        threshold: TimeSpan.FromMilliseconds(500),
        step: TimeSpan.FromMilliseconds(100))
    .Build();
```

or

```csharp
CosmosClientOptions options = new CosmosClientOptions()
{
    AvailabilityStrategyOptions
     = new AvailabilityStrategyOptions(
        type: AvailabilityStrategy.ParallelHedging,
        threshold: TimeSpan.FromMilliseconds(500),
        step: TimeSpan.FromMilliseconds(100))
};

CosmosClient client = new CosmosClient(
    accountEndpoint: "account endpoint",
    authKeyOrResourceToken: "auth key or resource token",
    clientOptions: options);
```

The example above will create a `CosmosClient` instance with AvailabilityStrategy enabled with at 500ms threhshold. This means that if a request takes longer than 500ms the SDK will send a new request to the backend in order of the Preferred Regions List. If still no response comes back after the step time, another parallel request will be made to the next region.  The SDK will then return the first response that comes back from the backend. The threshold parameter is a required parameter can can be set to any value greater than 0. There will also be options to specify all options for the `AvailabilityStrategyOptions` object at request level and enable or disable at request level.

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    AvailabilityStrategyEnabed = true,
    AvailabilityStrageyThreshold = TimeSpan.FromMilliseconds(400),
    AvailabilityStrageyStep = TimeSpan.FromMilliseconds(50)
};
```

## Exclude Regions

In request options, uses can specify a list of regions to exclude from the request. This will ensure that the request is not routed to the specified region(s).

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    ExcludeLocations = new List<string> {"West US", "Central Europe"}
};

ItemResponse<MyItem> response = await container.ReadItemAsync<MyItem>("id", partitionKey, requestOptions);
```

This can be used in scenarios where one regions is experiencing an outage and the user wants to ensure that the request is not routed to that region. Additional use cases can include scenarios where a users wants to route a request to a region that is not the primary region. In this cases, by excluding the primary region from the request, the request will be routed to the next region in the Preferred Regions List.

## Availability Strategy Conistency Levels

When using an availaibity strategy, you can now specify what consistency level you want to use for the requests made in the case where parallel hedging is made.

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    AvailabilityStrategyRequestConsistencyLevel = ConsistencyLevel.Eventual
};
```

This will work similar to the `RquestOptions.BaseConsistencyLevel` that already exists in the SDK. Not every request supports consistency level.
