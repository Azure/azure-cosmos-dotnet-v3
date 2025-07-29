//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

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
        [JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Path's spatial type
        /// </summary>
        [JsonPropertyName(Constants.Properties.Types)]
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
            internal set => this.spatialTypesInternal = value ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// Gets or sets the bounding box
        /// </summary>
        [JsonPropertyName("boundingBox")]
        public BoundingBoxProperties BoundingBox
        {
            get; set;
        }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}
