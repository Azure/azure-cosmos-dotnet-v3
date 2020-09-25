namespace Cosmos.Samples.Geospatial
{
    using System;
    using Microsoft.Azure.Cosmos.Spatial;
    using Newtonsoft.Json;

    public partial class Creature
    {
        // <summary>
        /// Gets or sets the id of the creature.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the creature.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the species of the creature.
        /// </summary>
        [JsonProperty(PropertyName = "species")]
        public string Species { get; set; }

        /// <summary>
        /// Gets or sets the location of the creature.
        /// </summary>
        [JsonProperty(PropertyName = "location")]
        public Point Location { get; set; }
        
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
