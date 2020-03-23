//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This retry policy is designed to work with in a pair with ClientRetryPolicy.
    /// The inner retryPolicy must be a ClientRetryPolicy or a rety policy delegating to it.
    /// 
    /// The expectation that is the outer retry policy in the retry policy chain and nobody can overwrite ShouldRetryResult.
    /// Once we clear the session we expect call to fail and throw exceptio to the client. Otherwise we may violate session consistency.
    /// </summary>
    internal sealed class ClearingSessionContainerClientRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly IDocumentClientRetryPolicy retryPolicy;
        private readonly ISessionContainer sessionContainer;
        private DocumentServiceRequest request;
        private bool hasTriggered = false;

        public ClearingSessionContainerClientRetryPolicy(ISessionContainer sessionContainer, IDocumentClientRetryPolicy retryPolicy)
        {
            this.retryPolicy = retryPolicy;
            this.sessionContainer = sessionContainer;
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.request = request;
            this.retryPolicy.OnBeforeSendRequest(request);
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetry = await this.retryPolicy.ShouldRetryAsync(exception, cancellationToken);

            DocumentClientException clientException = exception as DocumentClientException;

            return this.ShouldRetryInternal(
                clientException?.StatusCode,
                clientException?.GetSubStatus(),
                shouldRetry);
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            // Only used for collection cache whcih doesn't participate in pipeline
            throw new NotImplementedException();
        }

        private ShouldRetryResult ShouldRetryInternal(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            ShouldRetryResult shouldRetryResult)
        {
            if (this.request == null)
            {
                // someone didn't call OnBeforeSendRequest - nothing we can do
                return shouldRetryResult;
            }

            if (!shouldRetryResult.ShouldRetry && !this.hasTriggered && statusCode.HasValue && subStatusCode.HasValue)
            {
                if (this.request.IsNameBased &&
                    statusCode.Value == HttpStatusCode.NotFound &&
                    subStatusCode.Value == SubStatusCodes.ReadSessionNotAvailable)
                {
                    // Clear the session token, because the collection name might be reused.
                    DefaultTrace.TraceWarning("Clear the the token for named base request {0}", this.request.ResourceAddress);

                    this.sessionContainer.ClearTokenByCollectionFullname(this.request.ResourceAddress);

                    this.hasTriggered = true;
                }
            }

            return shouldRetryResult;
        }
    }
}
