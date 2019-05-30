namespace Cosmos.Samples.Shared
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class Metadata
    {
        /// <summary>
        /// Gets the time to live in seconds of the item in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "ttl")]
        public int? TimeToLive { get; set; }

        /// <summary>
        /// Gets the entity tag associated with the item from the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty("_etag")]
        public string Etag { get; set; }

        /// <summary>
        /// Gets the last modified timestamp associated with the item from the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty("_ts")]
        public DateTime Timestamp { get; set; }
    }
}
