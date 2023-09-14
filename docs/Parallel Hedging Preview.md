# Paralell Hedging Preview

## Paralell Hedging

When Building a new `CosmosClient` there will be an option to include paralell hedging in that client.

```csharp
CosmosClient client = new CosmosClientBuilder("connection string")
    .WithSpeculativeProcessing(speculativeThreshold: TimeSpan.FromMilliseconds(500))
    .Build();
```

or

```csharp
CosmosClientOptions options = new CosmosClientOptions()
{
    SpeculativeProcessor = new ThresholdSpeculator(TimeSpan.FromMilliseconds(500))
};

CosmosClient client = new CosmosClient(
    accountEndpoint: "account endpoint",
    authKeyOrResourceToken: "auth key or resource token",
    clientOptions: options);
```

The example above will create a `CosmosClient` instance with speculative processing enabled with at 500ms threhshold. This means that if a request takes longer than 500ms the SDK will send a new request to the backend in order of the Preferred Regions List. This process will repeat until a request comes back. The SDK will then return the first response that comes back from the backend. The threshold parameter is a required parameter can can be set to any value greater than 0. There are also methods that will allow a user to disable/enabled speculative processing after the client has been created. If you include speculative processing when creating the client it will be enabled by default.

```csharp
client.DisableSpeculativeProcessing(); // Disables speculative processing
client.EnableSpeculativeProcessing(); // Enables speculative processing
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
    LocationToRouteTo = "West US"
};

ItemResponse<MyItem> response = await container.ReadItemAsync<MyItem>("id", partitionKey, requestOptions);
```

```csharp
QueryRequestOptions requestOptions = new QueryRequestOptions()
{
    LocationToRouteTo = "West US"
};

FeedIterator<MyItem> iterator = container.GetItemQueryIterator<MyItem>("SELECT * FROM c", requestOptions);
```

This will override any logic the SDK does to determine where to route requests and will route the request to the region specified in the `LocationToRouteTo` property.

## Region Specific Conistency Levels

To set a region specific consistency level there is a option when creating a `CosmosClient` in `CosmosClientOptions`.

```csharp
CosmosClient client = new CosmosClientBuilder()
    .WithPerRegionConsistencyLevels(
        new Dictionary<string, ConsistencyLevel>()
        {
            { "West US", ConsistencyLevel.Eventual },
            { "East US", ConsistencyLevel.Session }
        })
    .Build();
```

or

```csharp
CosmosClientOptions options = new CosmosClientOptions()
{
    RegionConsistencyLevel = new Dictionary<string, ConsistencyLevel>()
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

This will set the consistency level for the regions specified in the dictionary. If a region is not specified in the dictionary the SDK will use the default consistency level for the account.
