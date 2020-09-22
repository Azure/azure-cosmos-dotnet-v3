namespace Cosmos.Samples.Geospatial
{
    using Newtonsoft.Json;
    using System;
    using Microsoft.Azure.Cosmos.Spatial;

    class Area
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
