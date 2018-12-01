//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Caches collection information.
    /// </summary>
    internal class ClientCollectionCache : CollectionCache
    {
        private readonly IStoreModel storeModel;
        private readonly IAuthorizationTokenProvider tokenProvider;
        private readonly RetryPolicy retryPolicy;

        public ClientCollectionCache(IStoreModel storeModel, IAuthorizationTokenProvider tokenProvider, RetryPolicy retryPolicy)
        {
            if (storeModel == null)
            {
                throw new ArgumentNullException("storeModel");
            }

            this.storeModel = storeModel;
            this.tokenProvider = tokenProvider;
            this.retryPolicy = retryPolicy;
        }

        protected override Task<CosmosContainerSettings> GetByRidAsync(string collectionRid, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDocumentClientRetryPolicy retryPolicyInstance = this.retryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                  () => this.ReadCollectionAsync(PathsHelper.GeneratePath(ResourceType.Collection, collectionRid, false), cancellationToken, retryPolicyInstance),
                  retryPolicyInstance,
                  cancellationToken);
        }

        protected override Task<CosmosContainerSettings> GetByNameAsync(string resourceAddress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDocumentClientRetryPolicy retryPolicyInstance = this.retryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadCollectionAsync(resourceAddress, cancellationToken, retryPolicyInstance),
                retryPolicyInstance,
                cancellationToken);
        }

        private async Task<CosmosContainerSettings> ReadCollectionAsync(string collectionLink, CancellationToken cancellationToken, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                   OperationType.Read,
                   ResourceType.Collection,
                   collectionLink,
                   AuthorizationTokenType.PrimaryMasterKey,
                   new StringKeyValueCollection()))
            {
                request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r");

                string authorizationToken = this.tokenProvider.GetUserAuthorizationToken(
                    request.ResourceAddress,
                    PathsHelper.GetResourcePath(request.ResourceType),
                    HttpConstants.HttpMethods.Get,
                    request.Headers,
                    AuthorizationTokenType.PrimaryMasterKey);

                request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken;

                using (new ActivityScope(Guid.NewGuid()))
                {
                    if (retryPolicyInstance != null)
                    {
                        retryPolicyInstance.OnBeforeSendRequest(request);
                    }

                    using (DocumentServiceResponse response = await this.storeModel.ProcessMessageAsync(request))
                    {
                        return new ResourceResponse<CosmosContainerSettings>(response).Resource;
                    }
                }
            }
        }
    }
}
