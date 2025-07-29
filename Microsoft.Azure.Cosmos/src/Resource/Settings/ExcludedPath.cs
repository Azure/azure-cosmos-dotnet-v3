//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary> 
    /// Specifies a path within a JSON document to be excluded while indexing data for the Azure Cosmos DB service.
    /// </summary>
    public sealed class ExcludedPath
    {
        /// <summary>
        /// Gets or sets the path to be excluded from indexing in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be excluded from indexing.
        /// </value>
        [JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}
