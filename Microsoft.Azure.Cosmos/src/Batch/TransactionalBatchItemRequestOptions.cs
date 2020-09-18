//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// <see cref="RequestOptions"/> that applies to an operation within a <see cref="TransactionalBatch"/>.
    /// </summary>
    public class TransactionalBatchItemRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the indexing directive (Include or Exclude) for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The indexing directive to use with a request.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
        public IndexingDirective? IndexingDirective { get; set; }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for write item operations like Create, Upsert, Patch and Replace.
        /// This removes the resource from the response. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// This is optimal for workloads where the returned resource is not used.
        /// </remarks>
        public bool? EnableContentResponseOnWrite { get; set; }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for read item operations like ReadItem
        /// This removes the resource from the response. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// This is optimal for workloads where the returned resource is not used.
        /// </remarks>
        internal bool? EnableContentResponseOnRead { get; set; }

        internal static TransactionalBatchItemRequestOptions FromItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            if (itemRequestOptions == null)
            {
                return null;
            }

            RequestOptions requestOptions = itemRequestOptions;
            TransactionalBatchItemRequestOptions batchItemRequestOptions = new TransactionalBatchItemRequestOptions
            {
                IndexingDirective = itemRequestOptions.IndexingDirective,
                IfMatchEtag = itemRequestOptions.IfMatchEtag,
                IfNoneMatchEtag = itemRequestOptions.IfNoneMatchEtag,
                Properties = itemRequestOptions.Properties,
                EnableContentResponseOnWrite = itemRequestOptions.EnableContentResponseOnWrite,
                EnableContentResponseOnRead = itemRequestOptions.EnableContentResponseOnRead,
                IsEffectivePartitionKeyRouting = itemRequestOptions.IsEffectivePartitionKeyRouting
            };
            return batchItemRequestOptions;
        }
    }
}