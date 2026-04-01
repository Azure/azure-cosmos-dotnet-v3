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

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                clientException?.StatusCode,
                clientException?.GetSubStatus(),
                clientException?.ResourceAddress);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextPolicy != null ? await this.nextPolicy.ShouldRetryAsync(exception, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage httpResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                httpResponseMessage.StatusCode,
                httpResponseMessage.Headers.SubStatusCode,
                httpResponseMessage.GetResourceAddress());

            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextPolicy != null ? await this.nextPolicy.ShouldRetryAsync(httpResponseMessage, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        private ShouldRetryResult ShouldRetryInternal(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            string resourceIdOrFullName)
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
