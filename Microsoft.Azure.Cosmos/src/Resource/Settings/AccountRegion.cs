//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The AccountLocation class represents an Azure Cosmos DB database account in a specific region.
    /// </summary>
    public class AccountRegion
    {
        /// <summary>
        /// Gets the name of the database account location in the Azure Cosmos DB service. For example,
        /// "West US" as the name of the database account location in the West US region.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Name)]
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the URL of the database account location in the Azure Cosmos DB service. For example,
        /// "https://contoso-WestUS.documents.azure.com:443/" as the URL of the 
        /// database account location in the West US region.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DatabaseAccountEndpoint)]
        public string Endpoint { get; internal set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
