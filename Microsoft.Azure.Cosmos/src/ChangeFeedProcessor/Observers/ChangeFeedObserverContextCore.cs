//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
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
    internal sealed class ChangeFeedObserverContextCore : ChangeFeedProcessorContextWithManualCheckpoint
    {
        private readonly PartitionCheckpointer checkpointer;
        private readonly ResponseMessage responseMessage;

        internal ChangeFeedObserverContextCore(
            string leaseToken, 
            ResponseMessage feedResponse, 
            PartitionCheckpointer checkpointer)
        {
            this.LeaseToken = leaseToken;
            this.responseMessage = feedResponse;
            this.checkpointer = checkpointer;
        }

        public override string LeaseToken { get; }

        public override CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;

        public override Headers Headers => this.responseMessage.Headers;

        public override async Task<(bool isSuccess, CosmosException error)> TryCheckpointAsync()
        {
            try
            {
                await this.checkpointer.CheckpointPartitionAsync(this.responseMessage.Headers.ContinuationToken);
                return (isSuccess: true, error: null);
            }
            catch (CosmosException cosmosException)
            {
                return (isSuccess: false, error: cosmosException);
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
                return (isSuccess: false, error: cosmosException);
            }
            catch (Exception exception)
            {
                CosmosException cosmosException = CosmosExceptionFactory.CreateInternalServerErrorException(
                    message: exception.Message,
                    headers: new Headers(),
                    innerException: exception);
                return (isSuccess: false, error: cosmosException);
            }
        }
    }
}