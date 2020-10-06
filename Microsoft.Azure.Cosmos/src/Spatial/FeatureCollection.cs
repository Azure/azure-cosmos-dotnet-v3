// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;

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
        }

        [DataMember(Name = "features")]
        [JsonProperty("features", Required = Required.Always)]
        public IReadOnlyList<Feature> Features { get; }

        [DataMember(Name = "type")]
        [JsonProperty("type", Required = Required.Always)]
        public GeoJsonType Type => GeoJsonType.FeatureCollection;
    }
}
