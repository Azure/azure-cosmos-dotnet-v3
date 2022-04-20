namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal sealed class States
    {
        [JsonProperty(PropertyName = "myPartitionKey")]
        public string MyPartitionKey { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "city")]
        public string City { get; set; }

        [JsonProperty(PropertyName = "postalcode")]
        public string PostalCode { get; set; }

        [JsonProperty(PropertyName = "region")]
        public string Region { get; set; }

        [JsonProperty(PropertyName = "userDefinedId")]
        public int UserDefinedID { get; set; }

        [JsonProperty(PropertyName = "wordsArray")]
        public List<string> WordsArray { get; set; }

        [JsonProperty(PropertyName = "tags")]
        public Tags Tags { get; set; }

        [JsonProperty(PropertyName = "recipientList")]
        public List<RecipientList> RecipientList { get; set; }
    }
}