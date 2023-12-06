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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    // Retry when we receive the throttling from server.
    internal sealed class MetadataRequestThrottleRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly Uri locationEndpoint;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly IDocumentClientRetryPolicy throttlingRetryPolicy;

        public MetadataRequestThrottleRetryPolicy(
            Uri locationEndpoint,
            GlobalEndpointManager endpointManager,
            int maxRetryAttemptsOnThrottledRequests,
            int maxRetryWaitTimeInSeconds)
        {
            this.locationEndpoint = locationEndpoint;
            this.globalEndpointManager = endpointManager;
            this.throttlingRetryPolicy = new ResourceThrottleRetryPolicy(
                maxRetryAttemptsOnThrottledRequests,
                maxRetryWaitTimeInSeconds);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occured when the operation was tried</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            DefaultTrace.TraceInformation(
                    "Inside MetadataRequestThrottleRetryPolicy.ShouldRetryAsync(). Exception: {0} ",
                    this.GetExceptionMessage(exception));

            if (exception is CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.ServiceUnavailable
                    && cosmosException.Headers.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone)
                {
                    DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: SubStatusCodes.PartitionKeyRangeGone hit. Calling globalEndpointManager.MarkEndpointUnavailableForRead() for URI: {0}.", this.locationEndpoint);
                    this.globalEndpointManager.MarkEndpointUnavailableForRead(this.locationEndpoint);
                }
            }

            return this.throttlingRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            if (cosmosResponseMessage?.StatusCode == HttpStatusCode.ServiceUnavailable
                && cosmosResponseMessage?.Headers.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone)
            {
                DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: SubStatusCodes.PartitionKeyRangeGone hit. Calling globalEndpointManager.MarkEndpointUnavailableForRead()..");
                this.globalEndpointManager.MarkEndpointUnavailableForRead(this.locationEndpoint);
            }

            return this.throttlingRetryPolicy.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        private object GetExceptionMessage(Exception exception)
        {
            if (exception is DocumentClientException dce && dce.StatusCode != null && (int)dce.StatusCode < (int)StatusCodes.InternalServerError)
            {
                // for client related errors, don't print out the whole call stack.
                // simply return the message to prevent CPU overhead on ToString() 
                return exception.Message;
            }

            return exception;
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
        }
    }
}