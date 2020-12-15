//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used only in the context of Bulk Stream operations.
    /// </summary>
    /// <see cref="BatchAsyncBatcher"/>
    /// <see cref="ItemBatchOperationContext"/>
    internal sealed class BulkPartitionKeyRangeGoneRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int MaxRetries = 5;

        private readonly IDocumentClientRetryPolicy nextRetryPolicy;

        private int retriesAttempted;

        public BulkPartitionKeyRangeGoneRetryPolicy(IDocumentClientRetryPolicy nextRetryPolicy)
        {
            this.nextRetryPolicy = nextRetryPolicy;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is DocumentClientException clientException)
            {
                ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                    clientException?.StatusCode,
                    clientException?.GetSubStatus());

                if (shouldRetryResult != null)
                {
                    return Task.FromResult(shouldRetryResult);
                }

                if (this.nextRetryPolicy == null)
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }
            }

            return this.nextRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode);
            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            if (this.nextRetryPolicy == null)
            {
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            return this.nextRetryPolicy.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextRetryPolicy.OnBeforeSendRequest(request);
        }

        private ShouldRetryResult ShouldRetryInternal(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode)
        {
            if (statusCode == HttpStatusCode.Gone
                && (subStatusCode == SubStatusCodes.PartitionKeyRangeGone 
                || subStatusCode == SubStatusCodes.NameCacheIsStale 
                || subStatusCode == SubStatusCodes.CompletingSplit
                || subStatusCode == SubStatusCodes.CompletingPartitionMigration))
            {
                if (this.retriesAttempted < MaxRetries)
                {
                    this.retriesAttempted++;
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }
                else
                {
                    // Got too many 410s
                    return ShouldRetryResult.NoRetry();
                }
            }

            return null;
        }
    }
}
