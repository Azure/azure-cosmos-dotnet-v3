//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Telemetry;

    internal class ClientPipelineBuilder
    {
        private readonly CosmosClient client;
        private readonly ConsistencyLevel? requestedClientConsistencyLevel;
        private readonly PriorityLevel? requestedPriorityLevel;
        private readonly DiagnosticsHandler diagnosticsHandler;
        private readonly RequestHandler invalidPartitionExceptionRetryHandler;
        private readonly RequestHandler transportHandler;
        private readonly TelemetryHandler telemetryHandler;
        private readonly int? requestedClientThroughputBucket;

        private IReadOnlyCollection<RequestHandler> customHandlers;
        private RequestHandler retryHandler;

        public ClientPipelineBuilder(
            CosmosClient client,
            ConsistencyLevel? requestedClientConsistencyLevel,
            PriorityLevel? requestedClientPriorityLevel,
            IReadOnlyCollection<RequestHandler> customHandlers,
            TelemetryToServiceHelper telemetryToServiceHelper,
            int? requestedClientThroughputBucket)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.requestedClientConsistencyLevel = requestedClientConsistencyLevel;
            this.requestedPriorityLevel = requestedClientPriorityLevel;
            this.requestedClientThroughputBucket = requestedClientThroughputBucket;
            this.transportHandler = new TransportHandler(client);
            Debug.Assert(this.transportHandler.InnerHandler == null, nameof(this.transportHandler));

            this.invalidPartitionExceptionRetryHandler = new NamedCacheRetryHandler();
            Debug.Assert(this.invalidPartitionExceptionRetryHandler.InnerHandler == null, "The invalidPartitionExceptionRetryHandler.InnerHandler must be null to allow other handlers to be linked.");

            this.PartitionKeyRangeHandler = new PartitionKeyRangeHandler(client);
            Debug.Assert(this.PartitionKeyRangeHandler.InnerHandler == null, "The PartitionKeyRangeHandler.InnerHandler must be null to allow other handlers to be linked.");

            // Disable system usage for internal builds. Cosmos DB owns the VMs and already logs
            // the system information so no need to track it.
#if !INTERNAL
            this.diagnosticsHandler = new DiagnosticsHandler();
            Debug.Assert(this.diagnosticsHandler.InnerHandler == null, nameof(this.diagnosticsHandler));
#else
            this.diagnosticsHandler = null;
#endif
            this.telemetryHandler = new TelemetryHandler(telemetryToServiceHelper);
            Debug.Assert(this.telemetryHandler.InnerHandler == null, nameof(this.telemetryHandler));

            this.UseRetryPolicy();
            this.AddCustomHandlers(customHandlers);
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
        ///                                    |       DiagnosticHandler     |
        ///                                    |                             |
        ///                                    +-----------------------------+
        ///                                                 |
        ///                                                 |
        ///                                                 |
        ///                                    +-----------------------------+
        ///                                    |                             |
        ///                                    |       TelemetryHandler      |-> Trigger a thread to monitor system usage/operation information and sends to an API
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
            RequestInvokerHandler root = new RequestInvokerHandler(
                this.client,
                this.requestedClientConsistencyLevel,
                this.requestedPriorityLevel,
                this.requestedClientThroughputBucket);

            RequestHandler current = root;
            if (this.CustomHandlers != null && this.CustomHandlers.Any())
            {
                foreach (RequestHandler handler in this.CustomHandlers)
                {
                    current.InnerHandler = handler;
                    current = current.InnerHandler;
                }
            }

            // Public SDK should always have the diagnostics handler
#if !INTERNAL
            Debug.Assert(this.diagnosticsHandler != null, nameof(this.diagnosticsHandler));
#endif
            if (this.diagnosticsHandler != null)
            {
                current.InnerHandler = this.diagnosticsHandler;
                current = current.InnerHandler;
            }

            if (this.telemetryHandler != null)
            {
                current.InnerHandler = this.telemetryHandler;
                current = current.InnerHandler;
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

        internal static RequestHandler CreatePipeline(params RequestHandler[] requestHandlers)
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
            this.retryHandler = new RetryHandler(this.client);
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
                    this.PartitionKeyRangeHandler,
                    this.transportHandler,
                };

            return ClientPipelineBuilder.CreatePipeline(feedPipeline);
        }
    }
}
