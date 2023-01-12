namespace Cosmos.Samples.CFPullModelAllVersionsAndDeletesMode.Models
{
    using System;
    using Newtonsoft.Json;

    internal class Metadata
    {
        [JsonProperty("operationType")]
        public string OperationType { get; set; }

        [JsonProperty("timeToLiveExpired")]
        public Boolean TimeToLiveExpired { get; set; }
    }
}
