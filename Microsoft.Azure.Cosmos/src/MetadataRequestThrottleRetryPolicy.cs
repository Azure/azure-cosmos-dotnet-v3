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

    /// <summary>
    /// Metadata Request Throttle Retry Policy is combination of endpoint change retry + throttling retry.
    /// </summary>
    internal sealed class MetadataRequestThrottleRetryPolicy : IDocumentClientRetryPolicy
    {
        /// <summary>
        /// A callback delegate to fetch the location endpoint at a later point of time.
        /// </summary>
        private readonly Func<Uri> locationEndpointCallbackUri;

        /// <summary>
        /// An instance of <see cref="GlobalEndpointManager"/>.
        /// </summary>
        private readonly GlobalEndpointManager globalEndpointManager;

        /// <summary>
        /// Defines the throttling retry policy that is used as the underlying retry policy.
        /// </summary>
        private readonly IDocumentClientRetryPolicy throttlingRetryPolicy;

        /// <summary>
        /// The constructor to initialize an instance of <see cref="MetadataRequestThrottleRetryPolicy"/>.
        /// </summary>
        /// <param name="locationEndpointCallbackUri">A callback delegate to fetch the location endpoint at a later point of time.</param>
        /// <param name="endpointManager">An instance of <see cref="GlobalEndpointManager"/></param>
        /// <param name="maxRetryAttemptsOnThrottledRequests">An integer defining the maximum number
        /// of attempts to retry when requests are throttled.</param>
        /// <param name="maxRetryWaitTimeInSeconds">An integer defining the maximum wait time in seconds.</param>
        public MetadataRequestThrottleRetryPolicy(
            Func<Uri> locationEndpointCallbackUri,
            GlobalEndpointManager endpointManager,
            int maxRetryAttemptsOnThrottledRequests,
            int maxRetryWaitTimeInSeconds)
        {
            this.locationEndpointCallbackUri = locationEndpointCallbackUri;
            this.globalEndpointManager = endpointManager;
            this.throttlingRetryPolicy = new ResourceThrottleRetryPolicy(
                maxRetryAttemptsOnThrottledRequests,
                maxRetryWaitTimeInSeconds);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occured when the operation was tried</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is CosmosException cosmosException
                && cosmosException.StatusCode == HttpStatusCode.ServiceUnavailable
                && cosmosException.Headers.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone)
            {
                if (!this.MarkEndpointUnavailableForRead())
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }
            }

            return this.throttlingRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            if (cosmosResponseMessage?.StatusCode == HttpStatusCode.ServiceUnavailable
                && cosmosResponseMessage?.Headers.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone)
            {
                if (!this.MarkEndpointUnavailableForRead())
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }
            }

            return this.throttlingRetryPolicy.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
        }

        /// <summary>
        /// Marks an endpoint unavailable in the global endpoint manager, for any future read requests.
        /// </summary>
        /// <returns>A boolean flag indicating if the operation was successful.</returns>
        private bool MarkEndpointUnavailableForRead()
        {
            Uri location = this.locationEndpointCallbackUri?.Invoke();
            if (location != null)
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Marking the following endpoint unavailable for reads: {0}.", location);
                this.globalEndpointManager.MarkEndpointUnavailableForRead(location);
                return true;
            }
            else
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: location endpoint couldn't be resolved. Skip marking endpoint unavailable for reads.");
                return false;
            }
        }
    }
}