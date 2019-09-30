//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Data.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Spatial index specification
    /// </summary>
    public sealed class SpatialPath
    {
        private Collection<SpatialType> spatialTypesInternal;

        /// <summary>
        /// Path in JSON document to index
        /// </summary>
        [JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Path's spatial type
        /// </summary>
        [JsonPropertyName(Constants.Properties.Types)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Collection<SpatialType> SpatialTypes
        {
            get
            {
                if (this.spatialTypesInternal == null)
                {
                    this.spatialTypesInternal = new Collection<SpatialType>();
                }
                return this.spatialTypesInternal;
            }
            internal set
            {
                if (value == null)
                {
                    throw new ArgumentNullException();
                }

                this.spatialTypesInternal = value;
            }
        }
    }
}
