namespace TestWorkloadV2
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    internal class MyDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("pk")]
        public string PK { get; set; }

        //[JsonProperty("arr")]
        //public List<string> Arr { get; set; }

        [JsonProperty("other")]
        public string Other { get; set; }

        //[JsonProperty(PropertyName = "_ts")]
        //public int LastModified { get; set; }

        //[JsonProperty(PropertyName = "_rid")]
        //public string ResourceId { get; set; }
    }
}
