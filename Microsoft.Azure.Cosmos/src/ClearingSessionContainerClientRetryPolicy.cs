//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;

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

        public async Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetry = await this.retryPolicy.ShouldRetryAsync(exception, cancellationToken);

            if (this.request == null)
            {
                // someone didn't call OnBeforeSendRequest - nothing we can do
                return shouldRetry;
            }

            if (!shouldRetry.ShouldRetry && !this.hasTriggered)
            {
                DocumentClientException clientException = exception as DocumentClientException;

                if (clientException != null && this.request.IsNameBased && 
                    clientException.StatusCode == HttpStatusCode.NotFound &&
                    clientException.GetSubStatus() == SubStatusCodes.ReadSessionNotAvailable)
                {
                    // Clear the session token, because the collection name might be reused.
                    DefaultTrace.TraceWarning("Clear the the token for named base request {0}", request.ResourceAddress);

                    this.sessionContainer.ClearTokenByCollectionFullname(request.ResourceAddress);

                    this.hasTriggered = true;
                }
            }

            return shouldRetry;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage cosmosResponseMessage, 
            CancellationToken cancellationToken)
        {
            // Only used for collection cache whcih doesn't participate in pipeline
            throw new NotImplementedException();
        }
    }
}
