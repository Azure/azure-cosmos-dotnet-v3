//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Caches collection information.
    /// </summary>
    internal class ClientCollectionCache : CollectionCache
    {
        private readonly IStoreModel storeModel;
        private readonly ICosmosAuthorizationTokenProvider tokenProvider;
        private readonly IRetryPolicyFactory retryPolicy;
        private readonly ISessionContainer sessionContainer;

        public ClientCollectionCache(
            ISessionContainer sessionContainer,
            IStoreModel storeModel,
            ICosmosAuthorizationTokenProvider tokenProvider,
            IRetryPolicyFactory retryPolicy)
        {
            this.storeModel = storeModel ?? throw new ArgumentNullException("storeModel");
            this.tokenProvider = tokenProvider;
            this.retryPolicy = retryPolicy;
            this.sessionContainer = sessionContainer;
        }

        protected override Task<ContainerProperties> GetByRidAsync(string apiVersion,
                                                    string collectionRid,
                                                    ITrace trace,
                                                    IClientSideRequestStatistics clientSideRequestStatistics,
                                                    CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDocumentClientRetryPolicy retryPolicyInstance = new ClearingSessionContainerClientRetryPolicy(
                this.sessionContainer, this.retryPolicy.GetRequestPolicy());
            return TaskHelper.InlineIfPossible(
                  () => this.ReadCollectionAsync(
                      PathsHelper.GeneratePath(ResourceType.Collection, collectionRid, false),
                      retryPolicyInstance,
                      trace,
                      clientSideRequestStatistics,
                      cancellationToken),
                  retryPolicyInstance,
                  cancellationToken);
        }

        protected override Task<ContainerProperties> GetByNameAsync(string apiVersion,
                                                string resourceAddress,
                                                ITrace trace,
                                                IClientSideRequestStatistics clientSideRequestStatistics,
                                                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDocumentClientRetryPolicy retryPolicyInstance = new ClearingSessionContainerClientRetryPolicy(
                this.sessionContainer, this.retryPolicy.GetRequestPolicy());
            return TaskHelper.InlineIfPossible(
                () => this.ReadCollectionAsync(
                    resourceAddress, retryPolicyInstance, trace, clientSideRequestStatistics, cancellationToken),
                retryPolicyInstance,
                cancellationToken);
        }

        internal override Task<ContainerProperties> ResolveByNameAsync(
            string apiVersion,
            string resourceAddress,
            bool forceRefesh,
            ITrace trace,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken)
        {
            if (forceRefesh && this.sessionContainer != null)
            {
                return TaskHelper.InlineIfPossible(
                    async () =>
                    {
                        string oldRid = (await base.ResolveByNameAsync(
                            apiVersion,
                            resourceAddress,
                            forceRefesh: false,
                            trace,
                            clientSideRequestStatistics,
                            cancellationToken))?.ResourceId;

                        ContainerProperties propertiesAfterRefresh = await base.ResolveByNameAsync(
                            apiVersion,
                            resourceAddress,
                            forceRefesh,
                            trace,
                            clientSideRequestStatistics,
                            cancellationToken);

                        if (oldRid != null && oldRid != propertiesAfterRefresh?.ResourceId)
                        {
                            string resourceFullName = PathsHelper.GetCollectionPath(resourceAddress);
                            this.sessionContainer.ClearTokenByCollectionFullname(resourceFullName);
                        }

                        return propertiesAfterRefresh;
                    },
                    retryPolicy: null,
                    cancellationToken);
            }

            return TaskHelper.InlineIfPossible(
                () => base.ResolveByNameAsync(
                    apiVersion, resourceAddress, forceRefesh, trace, clientSideRequestStatistics, cancellationToken),
                retryPolicy: null,
                cancellationToken);
        }

        public override Task<ContainerProperties> ResolveCollectionAsync(
            DocumentServiceRequest request, CancellationToken cancellationToken, ITrace trace)
        {
            return TaskHelper.InlineIfPossible(
                () => this.ResolveCollectionWithSessionContainerCleanupAsync(
                    request,
                    () => base.ResolveCollectionAsync(request, cancellationToken, trace)),
                retryPolicy: null,
                cancellationToken);
        }

        public override Task<ContainerProperties> ResolveCollectionAsync(
            DocumentServiceRequest request, TimeSpan refreshAfter, CancellationToken cancellationToken, ITrace trace)
        {
            return TaskHelper.InlineIfPossible(
                () => this.ResolveCollectionWithSessionContainerCleanupAsync(
                    request,
                    () => base.ResolveCollectionAsync(request, refreshAfter, cancellationToken, trace)),
                retryPolicy: null,
                cancellationToken);
        }

        private async Task<ContainerProperties> ResolveCollectionWithSessionContainerCleanupAsync(
            DocumentServiceRequest request,
            Func<Task<ContainerProperties>> resolveContainerProvider)
        {
            string previouslyResolvedCollectionRid = request?.RequestContext?.ResolvedCollectionRid;

            ContainerProperties properties = await resolveContainerProvider();

            if (this.sessionContainer != null &&
                previouslyResolvedCollectionRid != null &&
                previouslyResolvedCollectionRid != properties.ResourceId)
            {
                this.sessionContainer.ClearTokenByResourceId(previouslyResolvedCollectionRid);
            }

            return properties;
        }

        private async Task<ContainerProperties> ReadCollectionAsync(
            string collectionLink,
            IDocumentClientRetryPolicy retryPolicyInstance,
            ITrace trace,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken)
        {
            using (ITrace childTrace = trace.StartChild("Read Collection", TraceComponent.Transport, TraceLevel.Info))
            {
                cancellationToken.ThrowIfCancellationRequested();

                RequestNameValueCollection headers = new RequestNameValueCollection();
                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                       OperationType.Read,
                       ResourceType.Collection,
                       collectionLink,
                       AuthorizationTokenType.PrimaryMasterKey,
                       headers))
                {
                    headers.XDate = DateTime.UtcNow.ToString("r");

                    request.RequestContext.ClientRequestStatistics = clientSideRequestStatistics ?? new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace.Summary);
                    if (clientSideRequestStatistics == null)
                    {
                        childTrace.AddDatum(
                            "Client Side Request Stats",
                            request.RequestContext.ClientRequestStatistics);
                    }

                    string authorizationToken = await this.tokenProvider.GetUserAuthorizationTokenAsync(
                        request.ResourceAddress,
                        PathsHelper.GetResourcePath(request.ResourceType),
                        HttpConstants.HttpMethods.Get,
                        request.Headers,
                        AuthorizationTokenType.PrimaryMasterKey,
                        childTrace);

                    headers.Authorization = authorizationToken;

                    using (new ActivityScope(Guid.NewGuid()))
                    {
                        retryPolicyInstance?.OnBeforeSendRequest(request);

                        try
                        {
                            using (DocumentServiceResponse response =
                                await this.storeModel.ProcessMessageAsync(request))
                            {
                                return CosmosResource.FromStream<ContainerProperties>(response);
                            }
                        }
                        catch (DocumentClientException ex)
                        {
                            childTrace.AddDatum("Exception Message", ex.Message);
                            throw;
                        }
                    }
                }
            }
        }
    }
}