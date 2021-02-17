//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used only in the context of Bulk Stream operations.
    /// </summary>
    /// <see cref="BatchAsyncBatcher"/>
    /// <see cref="ItemBatchOperationContext"/>
    internal sealed class BulkExecutionRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int MaxRetryOn410 = 10;
        private readonly IDocumentClientRetryPolicy nextRetryPolicy;
        private readonly OperationType operationType;
        private readonly ContainerInternal container;
        private int retriesOn410 = 0;

        public BulkExecutionRetryPolicy(
            ContainerInternal container,
            OperationType operationType,
            IDocumentClientRetryPolicy nextRetryPolicy)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.operationType = operationType;
            this.nextRetryPolicy = nextRetryPolicy;
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is CosmosException clientException)
            {
                ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    clientException.StatusCode,
                    (SubStatusCodes)clientException.SubStatusCode,
                    cancellationToken);

                if (shouldRetryResult != null)
                {
                    return shouldRetryResult;
                }

                if (this.nextRetryPolicy == null)
                {
                    return ShouldRetryResult.NoRetry();
                }
            }

            return await this.nextRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode,
                cancellationToken);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            if (this.nextRetryPolicy == null)
            {
                return ShouldRetryResult.NoRetry();
            }

            return await this.nextRetryPolicy.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextRetryPolicy.OnBeforeSendRequest(request);
        }

        private bool IsReadRequest => this.operationType == OperationType.Read;

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            CancellationToken cancellationToken)
        {
            if (statusCode == HttpStatusCode.Gone)
            {
                this.retriesOn410++;

                if (this.retriesOn410 > MaxRetryOn410)
                {
                    return ShouldRetryResult.NoRetry();
                }

                if (subStatusCode == SubStatusCodes.PartitionKeyRangeGone
                    || subStatusCode == SubStatusCodes.CompletingSplit
                    || subStatusCode == SubStatusCodes.CompletingPartitionMigration)
                {
                    PartitionKeyRangeCache partitionKeyRangeCache = await this.container.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                    string containerRid = await this.container.GetCachedRIDAsync(
                        forceRefresh: false, 
                        NoOpTrace.Singleton, 
                        cancellationToken: cancellationToken);
                    await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                        containerRid,
                        FeedRangeEpk.FullRange.Range,
                        NoOpTrace.Singleton, 
                        forceRefresh: true);
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }

                if (subStatusCode == SubStatusCodes.NameCacheIsStale)
                {
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }
            }

            // Batch API can return 413 which means the response is bigger than 4Mb.
            // Operations that exceed the 4Mb limit are returned as 413, while the operations within the 4Mb limit will be 200
            if (this.IsReadRequest
                && statusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
            }

            return null;
        }
    }
}
