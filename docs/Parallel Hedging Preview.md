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

The example above will create a `CosmosClient` instance with speculative processing enabled with at 500ms threhshold. This means that if a request takes longer than 500ms the SDK will send a new request to the backend in order of the Preferred Regions List. This process will repeat until a request comes back. The SDK will then return the first response that comes back from the backend. The threshold parameter is a required parameter can can be set to any value greater than 0. There will also be options to specify a all options for the `AvailabilityStrategyOptions` object at request level and enable or disable speculative processing at request level.

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    AvailabilityStrategyEnabed = true,
    AvailabilityStrageyThreshold = TimeSpan.FromMilliseconds(400),
    AvailabilityStrageyStep = TimeSpan.FromMilliseconds(50)
};
```

## Dynamic Preferred Regions

If you want to change the preferred regions for a `CosmosClient` you can do so by callinga new method.

```csharp
client.UpdatePreferredRegions(new List<string>() { "West US", "East US" });
```

This will update the preferred regions for the client. The SDK will then use the new preferred regions for all new requests.

## Region Choice at Request Level

To choose what region a request will be routed to at a per request level there is a new option on the `RequestOptions` object.

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    ExcludeLocations = new List<string> {"West US", "Central Europe"}
};

ItemResponse<MyItem> response = await container.ReadItemAsync<MyItem>("id", partitionKey, requestOptions);
```

or

```csharp
QueryRequestOptions requestOptions = new QueryRequestOptions()
{
    ExcludeLocations = new List<string> {"West US"}
};

FeedIterator<MyItem> iterator = container.GetItemQueryIterator<MyItem>("SELECT * FROM c", requestOptions);
```

This will ensure that the request is not routed to the specified region(s).

## Region Specific Conistency Levels

~~To set a region specific consistency level there is a option when creating a `CosmosClient` in `CosmosClientOptions`.~~

```csharp
CosmosClient client = new CosmosClientBuilder()
    .WithPerRegionConsistencyLevels(
        new ObservableCollection<string, ConsistencyLevel>()
        {
            { "West US", ConsistencyLevel.Eventual },
            { "East US", ConsistencyLevel.Session }
        })
    .Build();
```

~~or~~

```csharp
CosmosClientOptions options = new CosmosClientOptions()
{
    RegionConsistencyLevel = new ObservableCollection<string, ConsistencyLevel>()
    {
        { "West US", ConsistencyLevel.Eventual },
        { "East US", ConsistencyLevel.Session }
    }
};

CosmosClient client = new CosmosClient(
    accountEndpoint: "account endpoint",
    authKeyOrResourceToken: "auth key or resource token",
    clientOptions: options);
```

~~This will set the consistency level for the regions specified in the dictionary. If a region is not specified in the dictionary the SDK will use the default consistency level for the account.~~

### Option 2

```csharp
RequestOptions requestOptions = new RequestOptions()
{
    AvailabilityStrategyFallbackConsistencyLevel = ConsistencyLevel.Eventual
};
```

This will work similar to the `RquestOptions.BaseConsistencyLevel` that already exists in the SDK. Not every request supports consistency level. This allows each child to decide to expose it and use the same base logic.
