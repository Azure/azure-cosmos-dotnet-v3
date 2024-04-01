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
        /// A constant integer defining the default maximum retry wait time in seconds.
        /// </summary>
        private const int DefaultMaxWaitTimeInSeconds = 60;

        /// <summary>
        /// A constant integer defining the default maximum retry count on service unavailable.
        /// </summary>
        private const int DefaultMaxServiceUnavailableRetryCount = 1;

        /// <summary>
        /// An instance of <see cref="IGlobalEndpointManager"/>.
        /// </summary>
        private readonly IGlobalEndpointManager globalEndpointManager;

        /// <summary>
        /// Defines the throttling retry policy that is used as the underlying retry policy.
        /// </summary>
        private readonly IDocumentClientRetryPolicy throttlingRetryPolicy;

        /// <summary>
        /// An integer defining the maximum retry count on service unavailable.
        /// </summary>
        private readonly int maxServiceUnavailableRetryCount;

        /// <summary>
        /// An instance of <see cref="Uri"/> containing the location endpoint where the partition key
        /// range http request will be sent over.
        /// </summary>
        private MetadataRetryContext retryContext;

        /// <summary>
        /// An integer capturing the current retry count on service unavailable.
        /// </summary>
        private int serviceUnavailableRetryCount;

        /// <summary>
        /// The constructor to initialize an instance of <see cref="MetadataRequestThrottleRetryPolicy"/>.
        /// </summary>
        /// <param name="endpointManager">An instance of <see cref="GlobalEndpointManager"/></param>
        /// <param name="maxRetryAttemptsOnThrottledRequests">An integer defining the maximum number
        /// of attempts to retry when requests are throttled.</param>
        /// <param name="maxRetryWaitTimeInSeconds">An integer defining the maximum wait time in seconds.</param>
        public MetadataRequestThrottleRetryPolicy(
            IGlobalEndpointManager endpointManager,
            int maxRetryAttemptsOnThrottledRequests,
            int maxRetryWaitTimeInSeconds = DefaultMaxWaitTimeInSeconds)
        {
            this.globalEndpointManager = endpointManager;
            this.maxServiceUnavailableRetryCount = Math.Max(
                MetadataRequestThrottleRetryPolicy.DefaultMaxServiceUnavailableRetryCount,
                this.globalEndpointManager.PreferredLocationCount);

            this.throttlingRetryPolicy = new ResourceThrottleRetryPolicy(
                maxRetryAttemptsOnThrottledRequests,
                maxRetryWaitTimeInSeconds);

            this.retryContext = new MetadataRetryContext
            {
                RetryLocationIndex = 0,
                RetryRequestOnPreferredLocations = true,
            };
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
                && cosmosException.Headers.SubStatusCode == SubStatusCodes.TransportGenerated503)
            {
                if (this.IncrementRetryIndexOnServiceUnavailableForMetadataRead())
                {
                    return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.Zero));
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
                && cosmosResponseMessage?.Headers?.SubStatusCode == SubStatusCodes.TransportGenerated503)
            {
                if (this.IncrementRetryIndexOnServiceUnavailableForMetadataRead())
                {
                    return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.Zero));
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
            // Clear the previous location-based routing directive.
            request.RequestContext.ClearRouteToLocation();
            request.RequestContext.RouteToLocation(
                this.retryContext.RetryLocationIndex,
                this.retryContext.RetryRequestOnPreferredLocations);

            Uri metadataLocationEndpoint = this.globalEndpointManager.ResolveServiceEndpoint(request);

            DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Routing the metadata request to: {0} for operation type: {1} and resource type: {2}.", metadataLocationEndpoint, request.OperationType, request.ResourceType);
            request.RequestContext.RouteToLocation(metadataLocationEndpoint);
        }

        /// <summary>
        /// Increments the location index when a service unavailable exception ocurrs, for any future read requests.
        /// </summary>
        /// <returns>A boolean flag indicating if the operation was successful.</returns>
        private bool IncrementRetryIndexOnServiceUnavailableForMetadataRead()
        {
            if (this.serviceUnavailableRetryCount++ >= this.maxServiceUnavailableRetryCount)
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Retry count: {0} has exceeded the maximum permitted retry count on service unavailable: {1}.", this.serviceUnavailableRetryCount, this.maxServiceUnavailableRetryCount);
                return false;
            }

            // Retrying on second PreferredLocations.
            // RetryCount is used as zero-based index.
            DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Incrementing the metadata retry location index to: {0}.", this.serviceUnavailableRetryCount);
            this.retryContext = new MetadataRetryContext()
            {
                RetryLocationIndex = this.serviceUnavailableRetryCount,
                RetryRequestOnPreferredLocations = true,
            };

            return true;
        }

        /// <summary>
        /// A helper class containing the required attributes for
        /// metadata retry context.
        /// </summary>
        internal sealed class MetadataRetryContext
        {
            /// <summary>
            /// An integer defining the current retry location index.
            /// </summary>
            public int RetryLocationIndex { get; set; }

            /// <summary>
            /// A boolean flag indicating if the request should retry on
            /// preferred locations.
            /// </summary>
            public bool RetryRequestOnPreferredLocations { get; set; }
        }
    }
}