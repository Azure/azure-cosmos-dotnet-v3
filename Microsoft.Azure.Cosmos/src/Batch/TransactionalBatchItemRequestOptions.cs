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

            RequestOptions requestOptions = itemRequestOptions as RequestOptions;
            TransactionalBatchItemRequestOptions batchItemRequestOptions = new TransactionalBatchItemRequestOptions();
            batchItemRequestOptions.IndexingDirective = itemRequestOptions.IndexingDirective;
            batchItemRequestOptions.IfMatchEtag = requestOptions.IfMatchEtag;
            batchItemRequestOptions.IfNoneMatchEtag = requestOptions.IfNoneMatchEtag;
            batchItemRequestOptions.Properties = requestOptions.Properties;
            batchItemRequestOptions.IsEffectivePartitionKeyRouting = requestOptions.IsEffectivePartitionKeyRouting;
            return batchItemRequestOptions;
        }
    }
}