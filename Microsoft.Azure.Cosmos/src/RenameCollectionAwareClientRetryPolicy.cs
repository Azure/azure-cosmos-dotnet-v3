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
    /// </summary>
    internal sealed class RenameCollectionAwareClientRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly IDocumentClientRetryPolicy retryPolicy;
        private readonly ISessionContainer sessionContainer;
        private readonly ClientCollectionCache collectionCache;
        private DocumentServiceRequest request;
        private bool hasTriggered = false;

        public RenameCollectionAwareClientRetryPolicy(ISessionContainer sessionContainer, ClientCollectionCache collectionCache, IDocumentClientRetryPolicy retryPolicy)
        {
            this.retryPolicy = retryPolicy;
            this.sessionContainer = sessionContainer;
            this.collectionCache = collectionCache;
            this.request = null;
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.request = request;
            this.retryPolicy.OnBeforeSendRequest(request);
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            HttpStatusCode? statusCode = null;
            SubStatusCodes? subStatusCode = null;

            DocumentClientException clientException = exception as DocumentClientException;
            if (clientException != null)
            {
                statusCode = clientException.StatusCode;
                subStatusCode = clientException.GetSubStatus();
            }

            return this.ShouldRetryInternalAsync(
                statusCode, 
                subStatusCode,
                this.retryPolicy.ShouldRetryAsync(exception, cancellationToken),
                cancellationToken);
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage cosmosResponseMessage, 
            CancellationToken cancellationToken)
        {
            return this.ShouldRetryInternalAsync(
                cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode,
                this.retryPolicy.ShouldRetryAsync(cosmosResponseMessage, cancellationToken),
                cancellationToken);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            Task<ShouldRetryResult> chainedRetryTask,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetry = await chainedRetryTask;

            if (this.request == null) {
                // someone didn't call OnBeforeSendRequest - nothing we can do
                return shouldRetry;
            }

            if (!shouldRetry.ShouldRetry && !this.hasTriggered)
            {
                if (this.request.IsNameBased &&
                    statusCode == HttpStatusCode.NotFound &&
                    subStatusCode == SubStatusCodes.ReadSessionNotAvailable)
                {
                    // Clear the session token, because the collection name might be reused.
                    DefaultTrace.TraceWarning("Clear the the token for named base request {0}", request.ResourceAddress);

                    this.sessionContainer.ClearTokenByCollectionFullname(request.ResourceAddress);

                    this.hasTriggered = true;

                    string oldCollectionRid = request.RequestContext.ResolvedCollectionRid;

                    request.ForceNameCacheRefresh = true;
                    request.RequestContext.ResolvedCollectionRid = null;

                    try
                    {
                        CosmosContainerSettings collectionInfo = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken);

                        if (collectionInfo == null)
                        {
                            DefaultTrace.TraceCritical("Can't recover from session unavailable exception because resolving collection name {0} returned null", request.ResourceAddress);
                        }
                        else if (!string.IsNullOrEmpty(oldCollectionRid) && !string.IsNullOrEmpty(collectionInfo.ResourceId))
                        {
                            if (!oldCollectionRid.Equals(collectionInfo.ResourceId))
                            {
                                return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        // When ResolveCollectionAsync throws an exception ignore it because it's an attempt to recover an existing
                        // error. When the recovery fails we return ShouldRetryResult.NoRetry and propaganate the original exception to the client

                        DefaultTrace.TraceCritical("Can't recover from session unavailable exception because resolving collection name {0} failed with {1}", request.ResourceAddress, e.ToString());
                    }
                }
            }

            return shouldRetry;
        }
    }
}
