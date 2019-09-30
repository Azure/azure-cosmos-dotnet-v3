//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
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
    }
}
