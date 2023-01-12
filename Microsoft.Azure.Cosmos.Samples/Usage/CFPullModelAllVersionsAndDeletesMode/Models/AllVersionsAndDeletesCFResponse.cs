namespace Cosmos.Samples.CFPullModelAllVersionsAndDeletesMode.Models
{
    using Newtonsoft.Json;

    internal class AllVersionsAndDeletesCFResponse
    {
        [JsonProperty("current")]
        public Item Current { get; set; }

        [JsonProperty("previous")]
        public Item Previous { get; set; }

        [JsonProperty("metadata")]
        public Metadata Metadata { get; set; }
    }
}
