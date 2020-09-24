//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

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
    }
}