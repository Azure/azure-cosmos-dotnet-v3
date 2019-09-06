//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// <see cref="RequestOptions"/> that apply to operations within a <see cref="Batch"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class BatchItemRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the indexing directive (Include or Exclude) for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The indexing directive to use with a request.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
        /// <seealso cref="IndexingDirective"/>
        public IndexingDirective? IndexingDirective { get; set; }

        internal static BatchItemRequestOptions FromItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            if (itemRequestOptions == null)
            {
                return null;
            }

            RequestOptions requestOptions = itemRequestOptions as RequestOptions;
            BatchItemRequestOptions batchItemRequestOptions = new BatchItemRequestOptions();
            batchItemRequestOptions.IndexingDirective = itemRequestOptions.IndexingDirective;
            batchItemRequestOptions.IfMatchEtag = requestOptions.IfMatchEtag;
            batchItemRequestOptions.IfNoneMatchEtag = requestOptions.IfNoneMatchEtag;
            batchItemRequestOptions.Properties = requestOptions.Properties;
            batchItemRequestOptions.IsEffectivePartitionKeyRouting = requestOptions.IsEffectivePartitionKeyRouting;
            return batchItemRequestOptions;
        }
    }
}