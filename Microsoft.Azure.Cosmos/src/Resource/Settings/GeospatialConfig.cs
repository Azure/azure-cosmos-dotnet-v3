//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents geospatial configuration for a collection in the Azure Cosmos DB service
    /// </summary>
    /// <example>
    /// <![CDATA[
    ///    {
    ///       "id": "CollectionId",
    ///       "indexingPolicy":...,
    ///       "geospatialConfig": 
    ///       {
    ///           "type": "Geography"
    ///       }
    ///    }
    /// ]]>
    /// </example>
    public sealed class GeospatialConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeospatialConfig"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Geospatial type is set to Geography by default.
        /// </remarks>
        public GeospatialConfig()
        {
            this.GeospatialType = GeospatialType.Geography;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeospatialConfig"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="geospatialType">Specifies GeospatialType of collection, which can be either Geography or Geometry</param>
        public GeospatialConfig(GeospatialType geospatialType)
        {
            this.GeospatialType = geospatialType;
        }

        /// <summary>
        /// Gets or sets the geospatial type (geography or geometry) in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.GeospatialType"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = "type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeospatialType GeospatialType
        {
            get; set;
        }
    }
}
