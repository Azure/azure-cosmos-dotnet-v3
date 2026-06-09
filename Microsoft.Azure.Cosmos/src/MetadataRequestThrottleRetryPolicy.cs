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
        /// An instance of <see cref="Uri"/> containing the location endpoint where the partition key
        /// range http request will be sent over.
        /// </summary>
        private MetadataRetryContext retryContext;

        /// <summary>
        /// An integer capturing the current retry count on unavailable endpoint.
        /// </summary>
        private int unavailableEndpointRetryCount;

        /// <summary>
        /// The request being sent to the service.
        /// </summary>
        private DocumentServiceRequest request;

        /// <summary>
        /// Optional per-logical-operation hedging context. When non-null, the
        /// bounded probe loop in
        /// <see cref="IncrementRetryIndexOnUnavailableEndpointForMetadataRead"/>
        /// skips any preferred-location index that resolves to an endpoint the
        /// hedge has already attempted. See
        /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.7.4.
        /// </summary>
        private MetadataHedgingStrategy.MetadataHedgingContext hedgeContext;

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
            else
            {
                DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Evaluating retry for Exception of type: {0}, Message: {1}, ResourceType {2}, CollectionName {3}, ResourceID {4}", 
                    exception.GetType().Name,
                    exception.Message,
                    this.request.ResourceType,
                    this.request.CollectionName,
                    this.request.ResourceId);

            }

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

            if (MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: statusCode,
                subStatus: subStatus,
                exception: null,
                callerToken: CancellationToken.None))
            {
                if (this.IncrementRetryIndexOnUnavailableEndpointForMetadataRead())
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

            if (MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: statusCode,
                subStatus: subStatus,
                exception: null,
                callerToken: CancellationToken.None))
            {
                if (this.IncrementRetryIndexOnUnavailableEndpointForMetadataRead())
                {
                    return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.Zero));
                }
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

            Uri metadataLocationEndpoint = this.globalEndpointManager.ResolveServiceEndpoint(request);

            DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Routing the metadata request to: {0} for operation type: {1} and resource type: {2} for collection: {3} with collection rid {4}.", 
                metadataLocationEndpoint, 
                request.OperationType, 
                request.ResourceType, 
                request.CollectionName, 
                request.ResourceId);
            request.RequestContext.RouteToLocation(metadataLocationEndpoint);
        }

        /// <summary>
        /// Attach a metadata hedging context. Allows the bounded probe loop in
        /// <see cref="IncrementRetryIndexOnUnavailableEndpointForMetadataRead"/>
        /// to skip preferred-location indices that resolve to an endpoint a
        /// hedge already attempted on this operation, capping total attempts at
        /// <c>preferred-region-count</c>. Safe no-op when <paramref name="context"/>
        /// is <c>null</c>. See
        /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.7.3.
        /// </summary>
        internal void AttachHedgeContext(MetadataHedgingStrategy.MetadataHedgingContext context)
        {
            this.hedgeContext = context;
        }

        /// <summary>
        /// Increments the location index when a unavailable endpoint exception ocurrs, for any future read requests.
        /// </summary>
        /// <returns>A boolean flag indicating if the operation was successful.</returns>
        private bool IncrementRetryIndexOnUnavailableEndpointForMetadataRead()
        {
            // Bounded probe loop: in the no-hedge case (hedgeContext == null) this
            // collapses to a single iteration matching the legacy monotonic counter.
            // When a hedge attached an AttemptedEndpoints set, advance past any
            // preferred-location index whose resolved endpoint is already in that
            // set, so the next BackoffRetryUtility iteration does not retry into a
            // region the hedge just used. See design §5.7.4.
            int maxIndices = this.globalEndpointManager.ReadEndpoints?.Count ?? 1;

            for (int probe = 0; probe < maxIndices; probe++)
            {
                if (this.unavailableEndpointRetryCount++ >= this.maxUnavailableEndpointRetryCount)
                {
                    DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Retry count: {0} has exceeded the maximum permitted retry count on unavailable endpoint: {1}.", this.unavailableEndpointRetryCount, this.maxUnavailableEndpointRetryCount);
                    return false;
                }

                // Side-effect: every `return true` path leaves
                // this.retryContext.RetryLocationIndex == this.unavailableEndpointRetryCount
                // so OnBeforeSendRequest on the next BackoffRetryUtility iteration
                // routes to the advanced index. Mutate in place so RetryRequestOnPreferredLocations
                // is preserved across probes.
                this.retryContext.RetryLocationIndex = this.unavailableEndpointRetryCount;
                this.retryContext.RetryRequestOnPreferredLocations = true;

                if (this.hedgeContext == null || this.request == null)
                {
                    DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Incrementing the metadata retry location index to: {0}.", this.unavailableEndpointRetryCount);
                    return true;
                }

                // Tentatively resolve the would-be endpoint for the new RetryLocationIndex.
                // ResolveServiceEndpoint is read-only against LocationCache (no mutation per
                // probe); this is a hard invariant for any future LocationCache refactor.
                this.request.RequestContext.ClearRouteToLocation();
                this.request.RequestContext.RouteToLocation(
                    this.unavailableEndpointRetryCount,
                    usePreferredLocations: true);
                Uri probedEndpoint = this.globalEndpointManager.ResolveServiceEndpoint(this.request);

                if (probedEndpoint == null
                    || !this.hedgeContext.AttemptedEndpoints.ContainsKey(probedEndpoint.AbsoluteUri))
                {
                    DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: Incrementing the metadata retry location index to: {0}.", this.unavailableEndpointRetryCount);
                    return true;
                }

                // Hedge already tried this region; advance.
                DefaultTrace.TraceInformation("MetadataRequestThrottleRetryPolicy: Skipping retry location index {0} because endpoint {1} is in hedgeContext.AttemptedEndpoints.", this.unavailableEndpointRetryCount, probedEndpoint);
            }

            DefaultTrace.TraceWarning("MetadataRequestThrottleRetryPolicy: All preferred regions exhausted by hedge attempts; terminating retry.");
            return false;
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