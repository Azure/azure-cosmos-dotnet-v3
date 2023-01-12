namespace Cosmos.Samples.CFPullModelLatestVersionMode.Models
{
    using Newtonsoft.Json;

    internal class Item
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        public double Value { get; set; }

        public string Pk { get; set; }
    }
}
