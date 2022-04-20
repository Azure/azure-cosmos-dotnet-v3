namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal sealed class Tags
    {
        [JsonProperty(PropertyName = "words")]
        public List<string> Words { get; set; }

        [JsonProperty(PropertyName = "numbers")]
        public string Numbers { get; set; }
    }
}