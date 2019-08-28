//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

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
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }
    }
}
