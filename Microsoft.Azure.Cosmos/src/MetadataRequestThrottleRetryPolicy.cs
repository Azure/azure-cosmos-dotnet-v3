//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Metadata Request Throttle Retry Policy is combination of endpoint change retry + throttling retry.
    /// On regional failures the policy marks the endpoint unavailable and retries on the next
    /// preferred region. Once all regions have been attempted, the exception propagates to the
    /// operation-level retry policy (e.g. <see cref="ClientRetryPolicy"/>) for cross-region failover.
    /// </summary>
    internal sealed class MetadataRequestThrottleRetryPolicy : IDocumentClientRetryPolicy
    {
        /// <summary>
        /// A constant integer defining the default maximum retry wait time in seconds.
        /// </summary>
        private const int DefaultMaxWaitTimeInSeconds = 60;

        /// <summary>
        /// A constant integer defining the default maximum retry count on unavailable endpoint.
        /// </summary>
        private const int DefaultMaxUnavailableEndpointRetryCount = 1;

        /// <summary>
        /// An instance of <see cref="IGlobalEndpointManager"/>.
        /// </summary>
        private readonly IGlobalEndpointManager globalEndpointManager;

        /// <summary>
        /// Defines the throttling retry policy that is used as the underlying retry policy.
        /// </summary>
        private readonly IDocumentClientRetryPolicy throttlingRetryPolicy;

        /// <summary>
        /// An integer defining the maximum retry count on unavailable endpoint.
        /// </summary>
        private readonly int maxUnavailableEndpointRetryCount;

        /// <summary>
        /// An instance of <see cref="MetadataRetryContext"/> containing the location index
        /// and preferred-location flag used to route the next retry attempt.
        /// </summary>
        private MetadataRetryContext retryContext;

        /// <summary>
        /// An integer capturing the current retry count on unavailable endpoint.
        /// </summary>
        private int unavailableEndpointRetryCount;

        /// <summary>
        /// The resolved location endpoint for the current attempt. Used to mark
        /// the endpoint as unavailable in the <see cref="IGlobalEndpointManager"/> when
        /// a regional failure is detected.
        /// </summary>
        private Uri locationEndpoint;

        /// <summary>
        /// The request being sent to the service.
        /// </summary>
        private DocumentServiceRequest request;

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
            this.maxUnavailableEndpointRetryCount = Math.Max(
                MetadataRequestThrottleRetryPolicy.DefaultMaxUnavailableEndpointRetryCount,
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
            if (exception is CosmosException cosmosException)
            {
                DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Evaluating retry for CosmosException with StatusCode: {0}, SubStatusCode: {1}, ResourceType {2}, CollectionName {3}, ResourceID {4}.", 
                    cosmosException.StatusCode, 
                    cosmosException.SubStatusCode,
                    this.request.ResourceType,
                    this.request.CollectionName,
                    this.request.ResourceId);
                return this.ShouldRetryInternalAsync(
                    cosmosException.StatusCode, 
                    (SubStatusCodes)cosmosException.SubStatusCode,
                    exception, 
                    cancellationToken);
            }

            if (exception is DocumentClientException clientException)
            {
                DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Evaluating retry for DocumentClientException with StatusCode: {0}, SubStatusCode: {1}, ResourceType {2}, CollectionName {3}, ResourceID {4}", 
                    clientException.StatusCode, 
                    clientException.GetSubStatus(),
                    this.request.ResourceType,
                    this.request.CollectionName,
                    this.request.ResourceId);
                return this.ShouldRetryInternalAsync(
                    clientException.StatusCode,
                    clientException.GetSubStatus(),
                    exception, cancellationToken);
            }

            if (exception is HttpRequestException)
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: HttpRequestException received. Marking endpoint {0} unavailable. ResourceType {1}, CollectionName {2}, ResourceID {3}.",
                    this.locationEndpoint,
                    this.request?.ResourceType,
                    this.request?.CollectionName,
                    this.request?.ResourceId);

                return Task.FromResult(this.HandleRegionalFailure());
            }

            if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Non-user OperationCanceledException received. Marking endpoint {0} unavailable. ResourceType {1}, CollectionName {2}, ResourceID {3}.",
                    this.locationEndpoint,
                    this.request?.ResourceType,
                    this.request?.CollectionName,
                    this.request?.ResourceId);

                return Task.FromResult(this.HandleRegionalFailure());
            }

            DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Evaluating retry for Exception of type: {0}, Message: {1}, ResourceType {2}, CollectionName {3}, ResourceID {4}", 
                exception.GetType().Name,
                exception.Message,
                this.request.ResourceType,
                this.request.CollectionName,
                this.request.ResourceId);

            return this.throttlingRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        private Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode, 
            SubStatusCodes subStatus,
            Exception exception, 
            CancellationToken cancellationToken)
        {
            if (statusCode == null)
            {
                return this.throttlingRetryPolicy.ShouldRetryAsync(exception, cancellationToken);
            }

            if (statusCode == HttpStatusCode.ServiceUnavailable 
                || statusCode == HttpStatusCode.InternalServerError
                || (statusCode == HttpStatusCode.Gone && subStatus == SubStatusCodes.LeaseNotFound)
                || (statusCode == HttpStatusCode.Forbidden && subStatus == SubStatusCodes.DatabaseAccountNotFound))
            {
                DefaultTrace.TraceWarning(
                    "MetadataRequestThrottleRetryPolicy: Regional failure detected (StatusCode: {0}, SubStatusCode: {1}). "
                    + "Marking endpoint {2} unavailable. ResourceType {3}, CollectionName {4}, ResourceID {5}.",
                    statusCode,
                    subStatus,
                    this.locationEndpoint,
                    this.request?.ResourceType,
                    this.request?.CollectionName,
                    this.request?.ResourceId);

                return Task.FromResult(this.HandleRegionalFailure());
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
            return this.ShouldRetryInternalAsync(
                cosmosResponseMessage.StatusCode,
                (SubStatusCodes)Convert.ToInt32(cosmosResponseMessage.Headers[WFConstants.BackendHeaders.SubStatus]),
                cosmosResponseMessage,
                cancellationToken);
        }

        private Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes subStatus,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken)
        {
            if (statusCode == null)
            {
                return this.throttlingRetryPolicy.ShouldRetryAsync(responseMessage, cancellationToken);
            }

            if (statusCode == HttpStatusCode.ServiceUnavailable 
                || statusCode == HttpStatusCode.InternalServerError
                || (statusCode == HttpStatusCode.Gone && subStatus == SubStatusCodes.LeaseNotFound)
                || (statusCode == HttpStatusCode.Forbidden && subStatus == SubStatusCodes.DatabaseAccountNotFound))
            {
                DefaultTrace.TraceWarning(
                    "MetadataRequestThrottleRetryPolicy: Regional failure detected in response (StatusCode: {0}, SubStatusCode: {1}). "
                    + "Marking endpoint {2} unavailable. ResourceType {3}, CollectionName {4}, ResourceID {5}.",
                    statusCode,
                    subStatus,
                    this.locationEndpoint,
                    this.request?.ResourceType,
                    this.request?.CollectionName,
                    this.request?.ResourceId);

                return Task.FromResult(this.HandleRegionalFailure());
            }

            return this.throttlingRetryPolicy.ShouldRetryAsync(responseMessage, cancellationToken);
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            // Clear the previous location-based routing directive.
            this.request = request;
            request.RequestContext.ClearRouteToLocation();
            request.RequestContext.RouteToLocation(
                this.retryContext.RetryLocationIndex,
                this.retryContext.RetryRequestOnPreferredLocations);

            this.locationEndpoint = this.globalEndpointManager.ResolveServiceEndpoint(request);

            DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Routing the metadata request to: {0} for operation type: {1} and resource type: {2} for collection: {3} with collection rid {4}.", 
                this.locationEndpoint, 
                request.OperationType, 
                request.ResourceType, 
                request.CollectionName, 
                request.ResourceId);
            request.RequestContext.RouteToLocation(this.locationEndpoint);
        }

        /// <summary>
        /// Marks the current endpoint as unavailable and attempts to increment the
        /// retry location index so the next attempt targets a different region.
        /// </summary>
        /// <returns>
        /// <see cref="ShouldRetryResult"/> with <c>ShouldRetry = true</c> if there are still
        /// regions left to try; <see cref="ShouldRetryResult"/> with <c>ShouldRetry = false</c> otherwise,
        /// allowing the exception to propagate to the operation-level retry policy.
        /// </returns>
        private ShouldRetryResult HandleRegionalFailure()
        {
            this.MarkEndpointUnavailable();

            if (this.IncrementRetryIndexOnUnavailableEndpointForMetadataRead())
            {
                return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
            }

            return ShouldRetryResult.NoRetry();
        }

        /// <summary>
        /// Marks the current <see cref="locationEndpoint"/> as unavailable for reads
        /// in the <see cref="IGlobalEndpointManager"/>. This acts as a hint to the
        /// <see cref="LocationCache"/> so that all subsequent calls to
        /// <see cref="IGlobalEndpointManager.ResolveServiceEndpoint"/> will prefer
        /// other regions.
        /// </summary>
        private void MarkEndpointUnavailable()
        {
            if (this.locationEndpoint != null)
            {
                DefaultTrace.TraceWarning(
                    "MetadataRequestThrottleRetryPolicy: Marking endpoint {0} unavailable for reads.",
                    this.locationEndpoint);

                this.globalEndpointManager.MarkEndpointUnavailableForRead(this.locationEndpoint);
            }
        }

        /// <summary>
        /// Increments the location index when an unavailable endpoint exception occurs, for any future read requests.
        /// </summary>
        /// <returns>A boolean flag indicating if there are still regions left to try.</returns>
        private bool IncrementRetryIndexOnUnavailableEndpointForMetadataRead()
        {
            if (this.unavailableEndpointRetryCount++ >= this.maxUnavailableEndpointRetryCount)
            {
                DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Retry count: {0} has exceeded the maximum permitted retry count on unavailable endpoint: {1}.", this.unavailableEndpointRetryCount, this.maxUnavailableEndpointRetryCount);
                return false;
            }

            DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Incrementing the metadata retry location index to: {0}.", this.unavailableEndpointRetryCount);
            this.retryContext = new MetadataRetryContext()
            {
                RetryLocationIndex = this.unavailableEndpointRetryCount,
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