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
    /// <example>
    /// <![CDATA[
    ///     "spatialIndexes":
    ///     [
    ///         {  
    ///             "path":"/'region'/?",
    ///             "types":["Polygon"],
    ///             "boundingBox": 
    ///                 {
    ///                    "xmin":0, 
    ///                    "ymin":0,
    ///                    "xmax":10, 
    ///                    "ymax":10
    ///                 }
    ///        }
    ///   ]
    /// ]]>
    /// </example>
    public sealed class SpatialPath
    {
        private Collection<SpatialType> spatialTypesInternal;

        /// <summary>
        /// Path in JSON document to index
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Path's spatial type
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Types, ItemConverterType = typeof(StringEnumConverter))]
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
                this.spatialTypesInternal = value ?? throw new ArgumentNullException();
            }
        }

        /// <summary>
        /// Gets or sets the bounding box
        /// </summary>
        [JsonProperty(PropertyName = "boundingBox", NullValueHandling = NullValueHandling.Ignore)]
        public BoundingBoxProperties BoundingBox
        {
            get; set;
        }
    }
}
