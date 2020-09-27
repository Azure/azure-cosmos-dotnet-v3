namespace Cosmos.Samples.Geospatial
{
    using System;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Spatial;

    public partial class Area
    {
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the creature.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the location of the creature.
        /// </summary>
        [JsonProperty("boundary")]
        public Polygon Boundary { get; set; }

        /// <summary>
        /// Returns the JSON string representation.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
