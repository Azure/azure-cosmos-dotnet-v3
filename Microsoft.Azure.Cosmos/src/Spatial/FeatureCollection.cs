// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [DataContract]
#if PREVIEW 
    public
#else
    internal
#endif
    sealed class FeatureCollection
    {
        public FeatureCollection(IReadOnlyList<Feature> features)
        {
            this.Features = features ?? throw new ArgumentNullException(nameof(features));
            if (features.Any(feature => feature is null))
            {
                throw new ArgumentException("features can not have any null elements.");
            }
        }

        [DataMember(Name = "type")]
        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeoJsonType Type => GeoJsonType.FeatureCollection;

        [DataMember(Name = "features")]
        [JsonProperty("features", Required = Required.Always)]
        public IReadOnlyList<Feature> Features { get; }
    }
}
