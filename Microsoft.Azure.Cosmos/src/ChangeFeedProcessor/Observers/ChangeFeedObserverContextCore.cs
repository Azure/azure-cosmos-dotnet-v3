//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// The context passed to <see cref="ChangeFeedObserver"/> events.
    /// </summary>
    internal sealed class ChangeFeedObserverContextCore
    {
        private readonly PartitionCheckpointer checkpointer;
        private readonly ResponseMessage responseMessage;

        internal ChangeFeedObserverContextCore(
            string leaseToken, 
            ResponseMessage feedResponse, 
            PartitionCheckpointer checkpointer,
            FeedRange feedRange)
        {
            this.LeaseToken = leaseToken;
            this.responseMessage = feedResponse;
            this.checkpointer = checkpointer;
            this.FeedRange = feedRange;
        }

        public string LeaseToken { get; }

        public CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;

        public Headers Headers => this.responseMessage.Headers;

        public FeedRange FeedRange { get; }

        public async Task CheckpointAsync()
        {
            try
            {
                await this.checkpointer.CheckpointPartitionAsync(this.responseMessage.Headers.ContinuationToken);
            }
            catch (LeaseLostException leaseLostException)
            {
                // LeaseLost means another instance stole the lease due to load balancing, so the right status is 412
                CosmosException cosmosException = CosmosExceptionFactory.Create(
                    statusCode: HttpStatusCode.PreconditionFailed,
                    message: "Lease was lost due to load balancing and will be processed by another instance",
                    stackTrace: leaseLostException.StackTrace,
                    headers: new Headers(),
                    trace: NoOpTrace.Singleton,
                    error: null,
                    innerException: leaseLostException);
                throw cosmosException;
            }
        }

        public static implicit operator ChangeFeedProcessorContext(ChangeFeedObserverContextCore contextCore)
        {
            return new ChangeFeedProcessorContextCore(contextCore);
        }
    }
}