//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// The Cosmos query request options
    /// </summary>
    public class ItemFeedRequestOptions : FeedRequestOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="Cosmos.PartitionKey"/> for the current request in the Azure Cosmos DB service.
        /// </summary>
        public PartitionKey PartitionKey { get; set; }
    }
}