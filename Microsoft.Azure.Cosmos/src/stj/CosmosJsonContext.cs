namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(DatabaseProperties))]
    [JsonSerializable(typeof(DatabaseProperties[]))]
    [JsonSerializable(typeof(ContainerProperties[]))]
    [JsonSerializable(typeof(ContainerProperties))]
    [JsonSerializable(typeof(FeedResponse<DatabaseProperties>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(PartitionedQueryExecutionInfo))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public partial class CosmosJsonContext : JsonSerializerContext
    {
    }
}
