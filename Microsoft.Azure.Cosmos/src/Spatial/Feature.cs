// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    [DataContract]
#if PREVIEW 
    public
#else
    internal
#endif
    sealed class Feature
    {
        public Feature(Geometry geometry, JObject properties)
            : this(GeoJsonType.Feature, geometry, properties, id: null)
        {
            // Work is done in the instance constructor
        }

        public Feature(Geometry geometry, JObject properties, double? id = null)
            : this(GeoJsonType.Feature, geometry, properties, id)
        {
            // Work is done in the instance constructor
        }

        public Feature(Geometry geometry, JObject properties, string id = null)
            : this(GeoJsonType.Feature, geometry, properties, id)
        {
            // Work is done in the instance constructor
        }

        [JsonConstructor]
        private Feature(GeoJsonType type, Geometry geometry, JObject properties, JToken id = null)
        {
            if ((id != null) && (id.Type != JTokenType.String) && (id.Type != JTokenType.Integer) && (id.Type != JTokenType.Float))
            {
                throw new ArgumentException($"{nameof(id)} must be a string or number.");
            }

            this.Type = type == GeoJsonType.Feature ? type : throw new ArgumentException($"{type} must be {GeoJsonType.Feature}");
            this.Geometry = geometry;
            this.Properties = properties;
            this.Id = id;
        }

        [DataMember(Name = "id")]
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Id { get; }

        [DataMember(Name = "type")]
        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeoJsonType Type { get; }

        [DataMember(Name = "geometry")]
        [JsonProperty("geometry", Required = Required.AllowNull)]
        public Geometry Geometry { get; }

        [DataMember(Name = "properties")]
        [JsonProperty("properties", Required = Required.AllowNull)]
        public JObject Properties { get; }
    }
}
