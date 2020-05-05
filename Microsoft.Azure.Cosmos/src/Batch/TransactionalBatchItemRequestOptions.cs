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
        /// the Cosmos DB response for write item operations like Create, Upsert, and Replace.
        /// This removes the resource from the response. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// This is optimal for workloads where the returned resource is not used.
        /// </remarks>
        public bool? NoContentResponseOnWrite { get; set; }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for read item operations like ReadItem
        /// This removes the resource from the response. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// This is optimal for workloads where the returned resource is not used.
        /// </remarks>
        public bool? NoContentResponseOnRead { get; set; }

        /// <summary>
        /// Options to encrypt properties of the item.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        EncryptionOptions EncryptionOptions { get; set; }

        internal static TransactionalBatchItemRequestOptions FromItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            if (itemRequestOptions == null)
            {
                return null;
            }

            TransactionalBatchItemRequestOptions batchItemRequestOptions = new TransactionalBatchItemRequestOptions();
            batchItemRequestOptions.IndexingDirective = itemRequestOptions.IndexingDirective;
            batchItemRequestOptions.IfMatchEtag = itemRequestOptions.IfMatchEtag;
            batchItemRequestOptions.IfNoneMatchEtag = itemRequestOptions.IfNoneMatchEtag;
            batchItemRequestOptions.Properties = itemRequestOptions.Properties;
            batchItemRequestOptions.NoContentResponseOnWrite = itemRequestOptions.NoContentResponseOnWrite;
            batchItemRequestOptions.NoContentResponseOnRead = itemRequestOptions.NoContentResponseOnRead;
            batchItemRequestOptions.IsEffectivePartitionKeyRouting = itemRequestOptions.IsEffectivePartitionKeyRouting;
            return batchItemRequestOptions;
        }
    }
}