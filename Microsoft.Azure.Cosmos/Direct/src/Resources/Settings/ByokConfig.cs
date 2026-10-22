//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents byok configuration for a collection in the Azure Cosmos DB service
    /// </summary>
    /// <example>
    /// <![CDATA[
    ///    {
    ///       "id": "CollectionId",
    ///       "indexingPolicy":...,
    ///       "byokConfig": 
    ///       {
    ///           "byokStatus": "Active"
    ///       }
    ///    }
    /// ]]>
    /// </example>
    internal sealed class ByokConfig : JsonSerializable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByokConfig"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// byok Status is set to None by default.
        /// </remarks>
        public ByokConfig()
        {
            this.ByokStatus = ByokStatus.None;
        }

        public ByokConfig(ByokStatus byokStatus)
        {
            this.ByokStatus = byokStatus;
        }

        /// <summary>
        /// Gets or sets the byok status in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.ByokStatus"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ByokStatus)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ByokStatus ByokStatus
        {
            get
            {
                ByokStatus result = ByokStatus.None;
                string strValue = base.GetValue<string>(Constants.Properties.ByokStatus);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (ByokStatus)Enum.Parse(typeof(ByokStatus), strValue, true);
                }
                return result;
            }

            set
            {
                base.SetValue(Constants.Properties.ByokStatus, value.ToString());
            }
        }
    }
}
