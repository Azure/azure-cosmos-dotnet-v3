//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Spatial index specification
    /// </summary>
    public sealed class SpatialSpec
    {
        [JsonProperty(PropertyName = Constants.Properties.Types, ItemConverterType = typeof(StringEnumConverter))]
        private Collection<SpatialType> spatialTypesInternal;

        /// <summary>
        /// Path in JSON document to index
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Path's spatial type
        /// </summary>
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
            set
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
