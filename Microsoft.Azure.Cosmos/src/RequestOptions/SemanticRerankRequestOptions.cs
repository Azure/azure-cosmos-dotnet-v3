//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Request options for semantic rerank operations in Azure Cosmos DB.
    /// </summary>
    public class SemanticRerankRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to return the documents text in the response. Default is true.
        /// </summary>
        public bool ReturnDocuments { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of top documents to return. Default all documents are returned.
        /// </summary>
        public int TopK { get; set; }

        /// <summary>
        /// Batch size for internal scoring operations
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// Whether to sort the results by relevance score in descending order. 
        /// </summary>
        public bool Sort { get; set; } = true;
    }
}
