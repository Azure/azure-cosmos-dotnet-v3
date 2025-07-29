namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(DatabaseProperties))]
    [JsonSerializable(typeof(ContainerProperties))]
    [JsonSerializable(typeof(FeedResponse<DatabaseProperties>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    public partial class CosmosJsonContext : JsonSerializerContext
    {
    }
}
