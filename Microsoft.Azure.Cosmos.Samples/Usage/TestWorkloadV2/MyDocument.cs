namespace TestWorkloadV2
{
    using MongoDB.Bson.Serialization.Attributes;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    internal class MyDocument
    {
        [JsonProperty("id")]
        [BsonId]
        public string Id { get; set; }

        [JsonProperty("pk")]
        [BsonElement("pk")]
        public string PK { get; set; }

        //[JsonProperty("arr")]
        //public List<string> Arr { get; set; }

        [JsonProperty("other")]
        [BsonElement("other")]
        public string Other { get; set; }

        //[JsonProperty(PropertyName = "_ts")]
        //public int LastModified { get; set; }

        //[JsonProperty(PropertyName = "_rid")]
        //public string ResourceId { get; set; }
    }
}
