//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class ClientPipelineBuilder
    {
        private readonly RequestHandler invalidPartitionExceptionRetryHandler;
        private readonly RequestHandler transportHandler;
        private readonly RequestHandler partitionKeyRangeGoneRetryHandler;
        private readonly IRetryPolicyFactory retryPolicyFactory;
        private readonly Func<Task<ClientCollectionCache>> getCollectionCacheAsync;
        private readonly CosmosClientOptions cosmosClientOptions;
        private readonly Func<Task> ensureClientIsValidAsync;
        private readonly Func<Task<Cosmos.ConsistencyLevel>> getAccountConsistencyLevelAsync;
        private readonly bool useMultipleWriteLocations;
        private IReadOnlyCollection<RequestHandler> customHandlers;
        private RequestHandler retryHandler;

        public ClientPipelineBuilder(
            IAuthorizationTokenProvider authorizationTokenProvider,
            Func<DocumentServiceRequest, IStoreModel> storeModelFactory,
            Action<DocumentServiceRequest, DocumentServiceResponse> captureSession,
            Func<Task<PartitionKeyRangeCache>> getPartitionKeyRangeCacheAsync,
            Func<Task<ClientCollectionCache>> getCollectionCacheAsync,
            IRetryPolicyFactory retryPolicyFactory,
            Func<Task<ConsistencyLevel>> getAccountConsistencyLevelAsync,
            Func<Task> ensureClientIsValidAsync,
            bool useMultipleWriteLocations,
            CosmosClientOptions cosmosClientOptions)
        {
            if (retryPolicyFactory == null)
            {
                throw new ArgumentNullException(nameof(retryPolicyFactory));
            }

            if (getCollectionCacheAsync == null)
            {
                throw new ArgumentNullException(nameof(getCollectionCacheAsync));
            }

            if (cosmosClientOptions == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientOptions));
            }

            if (getAccountConsistencyLevelAsync == null)
            {
                throw new ArgumentNullException(nameof(getAccountConsistencyLevelAsync));
            }

            if (ensureClientIsValidAsync == null)
            {
                throw new ArgumentNullException(nameof(ensureClientIsValidAsync));
            }

            this.retryPolicyFactory = retryPolicyFactory;
            this.getCollectionCacheAsync = getCollectionCacheAsync;
            this.cosmosClientOptions = cosmosClientOptions;
            this.ensureClientIsValidAsync = ensureClientIsValidAsync;
            this.getAccountConsistencyLevelAsync = getAccountConsistencyLevelAsync;
            this.useMultipleWriteLocations = useMultipleWriteLocations;

            this.transportHandler = new TransportHandler(
                authorizationTokenProvider,
                storeModelFactory,
                captureSession);
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));

            this.partitionKeyRangeGoneRetryHandler = new PartitionKeyRangeGoneRetryHandler(getPartitionKeyRangeCacheAsync, getCollectionCacheAsync);
            Debug.Assert(this.partitionKeyRangeGoneRetryHandler.InnerHandler == null, "The partitionKeyRangeGoneRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.invalidPartitionExceptionRetryHandler = new NamedCacheRetryHandler();
            Debug.Assert(this.invalidPartitionExceptionRetryHandler.InnerHandler == null, "The invalidPartitionExceptionRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.PartitionKeyRangeHandler = new PartitionKeyRangeHandler(getPartitionKeyRangeCacheAsync, getCollectionCacheAsync);
            Debug.Assert(this.PartitionKeyRangeHandler.InnerHandler == null, "The PartitionKeyRangeHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.UseRetryPolicy();
            this.AddCustomHandlers(cosmosClientOptions.CustomHandlers);
        }

        internal IReadOnlyCollection<RequestHandler> CustomHandlers
        {
            get => this.customHandlers;
            private set
            {
                if (value != null && value.Any(x => x?.InnerHandler != null))
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CustomHandlers));
                }

                this.customHandlers = value;
            }
        }

        internal RequestHandler PartitionKeyRangeHandler { get; set; }

        /// <summary>
        /// This is the cosmos pipeline logic for the operations. 
        /// 
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |    RequestInvokerHandler    |
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       UserHandlers          |
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       RetryHandler          |-> RetryPolicy -> ResetSessionTokenRetryPolicyFactory -> ClientRetryPolicy -> ResourceThrottleRetryPolicy
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       RouteHandler          | 
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |                             |
        ///                                    |                             |
        ///                  +-----------------------------+         +---------------------------------------+
        ///                  | !IsPartitionedFeedOperation |         |    IsPartitionedFeedOperation         |
        ///                  |      TransportHandler       |         | invalidPartitionExceptionRetryHandler |
        ///                  |                             |         |                                       |
        ///                  +-----------------------------+         +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |   partitionKeyRangeGoneRetryHandler   |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |     PartitionKeyRangeHandler          |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        ///                                                                          |
        ///                                                                          |
        ///                                                                          |
        ///                                                          +---------------------------------------+
        ///                                                          |                                       |
        ///                                                          |         TransportHandler              |
        ///                                                          |                                       |
        ///                                                          +---------------------------------------+
        /// </summary>
        /// <returns>The request invoker handler used to do calls to Cosmos DB</returns>
        public RequestInvokerHandler Build()
        {
            RequestInvokerHandler root = new RequestInvokerHandler(this.getCollectionCacheAsync, this.getAccountConsistencyLevelAsync, this.ensureClientIsValidAsync, this.useMultipleWriteLocations, this.cosmosClientOptions);

            RequestHandler current = root;
            if (this.CustomHandlers != null && this.CustomHandlers.Any())
            {
                foreach (RequestHandler handler in this.CustomHandlers)
                {
                    current.InnerHandler = handler;
                    current = current.InnerHandler;
                }
            }

            Debug.Assert(this.retryHandler != null, nameof(this.retryHandler));
            current.InnerHandler = this.retryHandler;
            current = current.InnerHandler;

            // Have a router handler
            RequestHandler feedHandler = this.CreateDocumentFeedPipeline();

            Debug.Assert(feedHandler != null, nameof(feedHandler));
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));
            RequestHandler routerHandler = new RouterHandler(
                documentFeedHandler: feedHandler,
                pointOperationHandler: this.transportHandler);

            current.InnerHandler = routerHandler;
            current = current.InnerHandler;

            return root;
        }

        private static RequestHandler CreatePipeline(params RequestHandler[] requestHandlers)
        {
            RequestHandler head = null;
            int handlerCount = requestHandlers.Length;
            for (int i = handlerCount - 1; i >= 0; i--)
            {
                RequestHandler indexHandler = requestHandlers[i];
                if (indexHandler.InnerHandler != null)
                {
                    throw new ArgumentOutOfRangeException($"The requestHandlers[{i}].InnerHandler is required to be null to allow the pipeline to chain the handlers.");
                }

                if (head != null)
                {
                    indexHandler.InnerHandler = head;
                }
                head = indexHandler;
            }

            return head;
        }

        private ClientPipelineBuilder UseRetryPolicy()
        {
            this.retryHandler = new RetryHandler(this.retryPolicyFactory);
            Debug.Assert(this.retryHandler.InnerHandler == null, "The retryHandler.InnerHandler must be null to allow other handlers to be linked.");
            return this;
        }

        private ClientPipelineBuilder AddCustomHandlers(IReadOnlyCollection<RequestHandler> customHandlers)
        {
            this.CustomHandlers = customHandlers;
            return this;
        }

        private RequestHandler CreateDocumentFeedPipeline()
        {
            RequestHandler[] feedPipeline = new RequestHandler[]
                {
                    this.invalidPartitionExceptionRetryHandler,
                    this.partitionKeyRangeGoneRetryHandler,
                    this.PartitionKeyRangeHandler,
                    this.transportHandler,
                };

            return ClientPipelineBuilder.CreatePipeline(feedPipeline);
        }
    }
}
