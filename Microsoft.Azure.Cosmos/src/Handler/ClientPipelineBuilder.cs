//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class ClientPipelineBuilder
    {
        private readonly CosmosClient client;
        private readonly CosmosRequestHandler invalidPartitionExceptionRetryHandler;
        private readonly CosmosRequestHandler transportHandler;
        private readonly CosmosRequestHandler partitionKeyRangeGoneRetryHandler;
        private ReadOnlyCollection<CosmosRequestHandler> customHandlers;
        private CosmosRequestHandler retryHandler;

        public ClientPipelineBuilder(
            CosmosClient client,
            IRetryPolicyFactory retryPolicyFactory,
            ReadOnlyCollection<CosmosRequestHandler> customHandlers)
        {
            this.client = client;
            this.transportHandler = new TransportHandler(client);
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));

            this.partitionKeyRangeGoneRetryHandler = new PartitionKeyRangeGoneRetryHandler(this.client);
            Debug.Assert(this.partitionKeyRangeGoneRetryHandler.InnerHandler == null, "The partitionKeyRangeGoneRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.invalidPartitionExceptionRetryHandler = new NamedCacheRetryHandler(this.client);
            Debug.Assert(this.invalidPartitionExceptionRetryHandler.InnerHandler == null, "The invalidPartitionExceptionRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.PartitionKeyRangeHandler = new PartitionKeyRangeHandler(client);
            Debug.Assert(this.PartitionKeyRangeHandler.InnerHandler == null, "The PartitionKeyRangeHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.UseRetryPolicy(retryPolicyFactory);
            this.AddCustomHandlers(customHandlers);
        }

        internal ReadOnlyCollection<CosmosRequestHandler> CustomHandlers
        {
            get => this.customHandlers;
            private set
            {
                if (value != null && value.Any(x => x?.InnerHandler != null))
                {
                    throw new ArgumentOutOfRangeException(nameof(CustomHandlers));
                }

                this.customHandlers = value;
            }
        }

        internal CosmosRequestHandler PartitionKeyRangeHandler { get; set; }

        public RequestInvokerHandler Build()
        {
            RequestInvokerHandler root = new RequestInvokerHandler(this.client);

            CosmosRequestHandler current = root;
            if (this.CustomHandlers != null && this.CustomHandlers.Any())
            {
                foreach (CosmosRequestHandler handler in this.CustomHandlers)
                {
                    current.InnerHandler = handler;
                    current = (CosmosRequestHandler)current.InnerHandler;
                }
            }

            Debug.Assert(this.retryHandler != null, nameof(this.retryHandler));
            current.InnerHandler = this.retryHandler;
            current = (CosmosRequestHandler)current.InnerHandler;

            // Have a router handler
            CosmosRequestHandler feedHandler = this.CreateDocumentFeedPipeline();

            Debug.Assert(feedHandler != null, nameof(feedHandler));
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));
            CosmosRequestHandler routerHandler = new RouterHandler(feedHandler, this.transportHandler);
            current.InnerHandler = routerHandler;
            current = (CosmosRequestHandler)current.InnerHandler;

            return root;
        }

        private static CosmosRequestHandler CreatePipeline(params CosmosRequestHandler[] requestHandlers)
        {
            CosmosRequestHandler head = null;
            int handlerCount = requestHandlers.Length;
            for (int i = handlerCount - 1; i >= 0; i--)
            {
                CosmosRequestHandler indexHandler = requestHandlers[i];
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

        private ClientPipelineBuilder UseRetryPolicy(IRetryPolicyFactory retryPolicyFactory)
        {
            this.retryHandler = new RetryHandler(retryPolicyFactory);
            Debug.Assert(this.retryHandler.InnerHandler == null, "The retryHandler.InnerHandler must be null to allow other handlers to be linked.");
            return this;
        }

        private ClientPipelineBuilder AddCustomHandlers(ReadOnlyCollection<CosmosRequestHandler> customHandlers)
        {
            this.CustomHandlers = customHandlers;
            return this;
        }

        private CosmosRequestHandler CreateDocumentFeedPipeline()
        {
            CosmosRequestHandler[] feedPipeline = new CosmosRequestHandler[]
                {
                    this.partitionKeyRangeGoneRetryHandler,
                    this.invalidPartitionExceptionRetryHandler,
                    this.PartitionKeyRangeHandler,
                    this.transportHandler,
                };

            return (CosmosRequestHandler)ClientPipelineBuilder.CreatePipeline(feedPipeline);
        }
    }
}
