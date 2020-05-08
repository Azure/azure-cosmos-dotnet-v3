//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;

    internal abstract class ContainerInternal : Container
    {
        internal abstract Uri LinkUri { get; }

        internal abstract CosmosClientContext ClientContext { get; }

        internal abstract BatchAsyncContainerExecutor BatchExecutor { get; }

        internal abstract Task<ThroughputResponse> ReadThroughputIfExistsAsync(
           RequestOptions requestOptions,
           CancellationToken cancellationToken);

        internal abstract Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            ThroughputProperties throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken);

        internal abstract Task<string> GetRIDAsync(CancellationToken cancellationToken);

        internal abstract Task<Documents.PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(
            CancellationToken cancellationToken);

        internal abstract Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        internal abstract Task<string[]> GetPartitionKeyPathTokensAsync(CancellationToken cancellationToken = default(CancellationToken));

        internal abstract Task<Documents.Routing.PartitionKeyInternal> GetNonePartitionKeyValueAsync(
            CancellationToken cancellationToken);

        internal abstract Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken);

        internal abstract Task<((Exception, PartitionedQueryExecutionInfo), (bool, QueryIterator))> TryExecuteQueryAsync(
            QueryFeatures supportedQueryFeatures,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken));

        internal abstract FeedIterator GetStandByFeedIterator(
            string continuationToken = default,
            int? maxItemCount = default,
            ChangeFeedRequestOptions requestOptions = default);

        internal abstract FeedIteratorInternal GetItemQueryStreamIteratorInternal(
            SqlQuerySpec sqlQuerySpec,
            bool isContinuationExcpected,
            string continuationToken,
            FeedRangeInternal feedRange,
            QueryRequestOptions requestOptions);

        internal abstract Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            CancellationToken cancellation);

        internal abstract Task<IEnumerable<string>> GetChangeFeedTokensAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Throw an exception if the partition key is null or empty string
        /// </summary>
        internal static void ValidatePartitionKey(object partitionKey, RequestOptions requestOptions)
        {
            if (partitionKey != null)
            {
                return;
            }

            if (requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting)
            {
                return;
            }

            throw new ArgumentNullException(nameof(partitionKey));
        }

#if !PREVIEW
        public abstract Task<ResponseMessage> DeleteItemsInPartitionKeyAsync(
               Cosmos.PartitionKey partitionKey,
               ItemRequestOptions requestOptions = null,
               CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        public abstract FeedIterator GetChangeFeedStreamIterator(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator GetChangeFeedStreamIterator(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator GetChangeFeedStreamIterator(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);
 
        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);
      
        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null);

        public abstract FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
#endif
    }
}
