using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Cosmos.Samples.Shared
{
    internal class AnalyticalProperties : ContainerProperties
    {
        [JsonProperty(PropertyName = "analyticalStorageTtl", NullValueHandling = NullValueHandling.Ignore)]
        public int? AnalyticalStoreTimeToLiveInSeconds { get; set; }
    }
}