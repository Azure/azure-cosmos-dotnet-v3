//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class InvalidPartitionExceptionRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly IDocumentClientRetryPolicy nextPolicy;
        private DocumentServiceRequest documentServiceRequest;

        private bool retried;

        public InvalidPartitionExceptionRetryPolicy(
            IDocumentClientRetryPolicy nextPolicy)
        {
            this.nextPolicy = nextPolicy;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                clientException?.StatusCode,
                clientException?.GetSubStatus());
            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            return this.nextPolicy != null ? this.nextPolicy.ShouldRetryAsync(exception, cancellationToken) : Task.FromResult(ShouldRetryResult.NoRetry());
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage httpResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                httpResponseMessage.StatusCode,
                httpResponseMessage.Headers.SubStatusCode);

            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            return this.nextPolicy != null ? this.nextPolicy.ShouldRetryAsync(httpResponseMessage, cancellationToken) : Task.FromResult(ShouldRetryResult.NoRetry());
        }

        private ShouldRetryResult ShouldRetryInternal(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode)
        {
            if (!statusCode.HasValue
                && (!subStatusCode.HasValue
                || subStatusCode.Value == SubStatusCodes.Unknown))
            {
                return null;
            }

            if (statusCode == HttpStatusCode.Gone
                && subStatusCode == SubStatusCodes.NameCacheIsStale)
            {
                if (!this.retried)
                {
                    if (this.documentServiceRequest == null)
                    {
                        throw new InvalidOperationException("OnBeforeSendRequest was never called");
                    }

                    this.documentServiceRequest.ForceNameCacheRefresh = true;
                    this.documentServiceRequest.ClearRoutingHints();

                    this.retried = true;
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }
                else
                {
                    return ShouldRetryResult.NoRetry();
                }
            }

            return null;
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.documentServiceRequest = request;
            this.nextPolicy?.OnBeforeSendRequest(request);
        }
    }
}
