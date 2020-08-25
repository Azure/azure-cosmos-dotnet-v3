//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;

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
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class GeospatialConfig : JsonSerializable, ICloneable
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
        [JsonProperty(PropertyName = Constants.Properties.GeospatialType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GeospatialType GeospatialType
        {
            get
            {
                GeospatialType result = GeospatialType.Geography;
                string strValue = base.GetValue<string>(Constants.Properties.GeospatialType);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (GeospatialType)Enum.Parse(typeof(GeospatialType), strValue, true);
                }
                return result;
            }

            set
            {
                base.SetValue(Constants.Properties.GeospatialType, value.ToString());
            }
        }

        public object Clone()
        {
            GeospatialConfig cloned = new GeospatialConfig();
            cloned.GeospatialType = this.GeospatialType;

            return cloned;
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateEnumProperties<GeospatialType>(this.GeospatialType);
        }
    }
}
