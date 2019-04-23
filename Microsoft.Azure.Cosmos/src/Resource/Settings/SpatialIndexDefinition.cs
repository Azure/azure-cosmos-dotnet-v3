//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Spatial index specification 
    /// </summary>
    public sealed class SpatialIndexDefinition
    {
        /// <summary>
        /// Path of the Item which is spatial
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Spatila datatype for the defined path
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Types, ItemConverterType = typeof(StringEnumConverter))]
        public Collection<SpatialType> SpatialTypes { get; set; } = new Collection<SpatialType>();
    }
}
