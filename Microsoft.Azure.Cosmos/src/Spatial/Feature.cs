// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;

    [DataContract]
#if PREVIEW 
    public
#else
    internal
#endif
    sealed class Feature
    {
        public Feature(Geometry geometry, IReadOnlyDictionary<string, object> properties, double? id = null)
            : this(geometry, properties, (object)id)
        {
            // Work is done in the instance constructor
        }

        public Feature(Geometry geometry, IReadOnlyDictionary<string, object> properties, string id = null)
            : this(geometry, properties, (object)id)
        {
            // Work is done in the instance constructor
        }

        private Feature(Geometry geometry, IReadOnlyDictionary<string, object> properties, object id = null)
        {
            this.Geometry = geometry;
            this.Properties = properties;
            this.Id = id;
        }

        [DataMember(Name = "id")]
        [JsonProperty("type", Required = Required.DisallowNull)]
        public object Id { get; }

        [DataMember(Name = "type")]
        [JsonProperty("type", Required = Required.Always)]
        public GeoJsonType Type => GeoJsonType.Feature;

        [DataMember(Name = "geometry")]
        [JsonProperty("geometry", Required = Required.AllowNull)]
        public Geometry Geometry { get; }

        [DataMember(Name = "properties")]
        [JsonProperty("properties", Required = Required.AllowNull)]
        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
