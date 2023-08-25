//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents bounding box for geometry spatial path in the Azure Cosmos DB service
    /// </summary>
    /// <example>
    /// <![CDATA[
    ///    {
    ///       "id": "DocumentCollection Id",
    ///       "indexingPolicy":{
    ///             "spatialIndexes":
    ///             [{  
    ///                 "path":"/'region'/?",
    ///                 "types":["Polygon"],
    ///                 "boundingBox": 
    ///                 {
    ///                    "xmin":0, 
    ///                    "ymin":0,
    ///                    "xmax":10, 
    ///                    "ymax":10
    ///                 }
    ///             }]
    ///            },
    ///       "geospatialConfig": 
    ///       {
    ///           "type": "Geometry"
    ///       }
    ///    }
    /// ]]>
    /// </example>
    public sealed class BoundingBoxProperties
    {
        /// <summary>
        /// Gets the x-coordinate of the lower-left corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = "xmin")]
        public double Xmin
        {
            get; set;
        }

        /// <summary>
        /// Gets the y-coordinate of the lower-left corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = "ymin")]
        public double Ymin
        {
            get; set;
        }

        /// <summary>
        /// Gets the x-coordinate of the upper-right corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = "xmax")]
        public double Xmax
        {
            get; set;
        }

        /// <summary>
        /// Gets the y-coordinate of the upper-right corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = "ymax")]
        public double Ymax
        {
            get; set;
        }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}