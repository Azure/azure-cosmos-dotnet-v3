//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Client policy is combination of endpoint change retry + throttling retry.
    /// </summary>
    internal sealed class ClientRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int RetryIntervalInMS = 1000; // Once we detect failover wait for 1 second before retrying request.
        private const int MaxRetryCount = 120;
        private const int MaxServiceUnavailableRetryCount = 1;

        private readonly IDocumentClientRetryPolicy throttlingRetry;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private readonly bool enableEndpointDiscovery;
        private int failoverRetryCount;

        private int sessionTokenRetryCount;
        private int serviceUnavailableRetryCount;
        private bool isReadRequest;
        private bool canUseMultipleWriteLocations;
        private Uri locationEndpoint;
        private RetryContext retryContext;
        private DocumentServiceRequest documentServiceRequest;

        public ClientRetryPolicy(
            GlobalEndpointManager globalEndpointManager,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache,
            bool enableEndpointDiscovery,
            RetryOptions retryOptions)
        {
            this.throttlingRetry = new ResourceThrottleRetryPolicy(
                retryOptions.MaxRetryAttemptsOnThrottledRequests,
                retryOptions.MaxRetryWaitTimeInSeconds);

            this.globalEndpointManager = globalEndpointManager;
            this.partitionKeyRangeLocationCache = partitionKeyRangeLocationCache;
            this.failoverRetryCount = 0;
            this.enableEndpointDiscovery = enableEndpointDiscovery;
            this.sessionTokenRetryCount = 0;
            this.serviceUnavailableRetryCount = 0;
            this.canUseMultipleWriteLocations = false;
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occurred when the operation was tried</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            this.retryContext = null;
            // Received Connection error (HttpRequestException), initiate the endpoint rediscovery
            if (exception is HttpRequestException _)
            {
                DefaultTrace.TraceWarning("Endpoint not reachable. Refresh cache and retry");
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: this.isReadRequest,
                    forceRefresh: false,
                    retryOnPreferredLocations: true);
            }

            if (exception is DocumentClientException clientException)
            {
                ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    clientException?.StatusCode,
                    clientException?.GetSubStatus());
                if (shouldRetryResult != null)
                {
                    return shouldRetryResult;
                }
            }

            return await this.throttlingRetry.ShouldRetryAsync(exception, cancellationToken);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            this.retryContext = null;

            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    cosmosResponseMessage?.StatusCode,
                    cosmosResponseMessage?.Headers.SubStatusCode);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return await this.throttlingRetry.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.isReadRequest = request.IsReadOnlyRequest;
            this.canUseMultipleWriteLocations = this.globalEndpointManager.CanUseMultipleWriteLocations(request);
            this.documentServiceRequest = request;

            // clear previous location-based routing directive
            request.RequestContext.ClearRouteToLocation();

            if (this.retryContext != null)
            {
                // set location-based routing directive based on request retry context
                request.RequestContext.RouteToLocation(this.retryContext.RetryLocationIndex, this.retryContext.RetryRequestOnPreferredLocations);
            }

            // Resolve the endpoint for the request and pin the resolution to the resolved endpoint
            // This enables marking the endpoint unavailability on endpoint failover/unreachability
            this.locationEndpoint = this.globalEndpointManager.ResolveServiceEndpoint(request);
            request.RequestContext.RouteToLocation(this.locationEndpoint);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode)
        {
            if (!statusCode.HasValue
                && (!subStatusCode.HasValue
                || subStatusCode.Value == SubStatusCodes.Unknown))
            {
                return null;
            }

            // Received 403.3 on write region, initiate the endpoint rediscovery
            if (statusCode == HttpStatusCode.Forbidden
                && subStatusCode == SubStatusCodes.WriteForbidden)
            {
                if (this.partitionKeyRangeLocationCache.TryMarkEndpointUnavailableForPartitionKeyRange(
                     this.documentServiceRequest))
                {
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }

                DefaultTrace.TraceWarning("Endpoint not writable. Refresh cache and retry");
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: false,
                    forceRefresh: true,
                    retryOnPreferredLocations: false);
            }

            // Regional endpoint is not available yet for reads (e.g. add/ online of region is in progress)
            if (statusCode == HttpStatusCode.Forbidden
                && subStatusCode == SubStatusCodes.DatabaseAccountNotFound
                && (this.isReadRequest || this.canUseMultipleWriteLocations))
            {
                DefaultTrace.TraceWarning("Endpoint not available for reads. Refresh cache and retry");
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: this.isReadRequest,
                    forceRefresh: false,
                    retryOnPreferredLocations: false);
            }

            if (statusCode == HttpStatusCode.NotFound
                && subStatusCode == SubStatusCodes.ReadSessionNotAvailable)
            {
                return this.ShouldRetryOnSessionNotAvailable();
            }

            // Received 503.0 due to client connect timeout or Gateway
            if (statusCode == HttpStatusCode.ServiceUnavailable
                && subStatusCode == SubStatusCodes.Unknown)
            {
                this.partitionKeyRangeLocationCache.TryMarkEndpointUnavailableForPartitionKeyRange(
                     this.documentServiceRequest);

                return this.ShouldRetryOnServiceUnavailable();
            }

            return null;
        }

        private async Task<ShouldRetryResult> ShouldRetryOnEndpointFailureAsync(
            bool isReadRequest,
            bool forceRefresh,
            bool retryOnPreferredLocations)
        {
            if (!this.enableEndpointDiscovery || this.failoverRetryCount > MaxRetryCount)
            {
                DefaultTrace.TraceInformation("ShouldRetryOnEndpointFailureAsync() Not retrying. Retry count = {0}", this.failoverRetryCount);
                return ShouldRetryResult.NoRetry();
            }

            this.failoverRetryCount++;

            if (this.locationEndpoint != null)
            {
                if (isReadRequest)
                {
                    this.globalEndpointManager.MarkEndpointUnavailableForRead(this.locationEndpoint);
                }
                else
                {
                    this.globalEndpointManager.MarkEndpointUnavailableForWrite(this.locationEndpoint);
                }
            }

            TimeSpan retryDelay = TimeSpan.Zero;
            if (!isReadRequest)
            {
                DefaultTrace.TraceInformation("Failover happening. retryCount {0}", this.failoverRetryCount);

                if (this.failoverRetryCount > 1)
                {
                    //if retried both endpoints, follow regular retry interval.
                    retryDelay = TimeSpan.FromMilliseconds(ClientRetryPolicy.RetryIntervalInMS);
                }
            }
            else
            {
                retryDelay = TimeSpan.FromMilliseconds(ClientRetryPolicy.RetryIntervalInMS);
            }

            await this.globalEndpointManager.RefreshLocationAsync(null, forceRefresh);

            int retryLocationIndex = this.failoverRetryCount; // Used to generate a round-robin effect
            if (retryOnPreferredLocations)
            {
                retryLocationIndex = 0; // When the endpoint is marked as unavailable, it is moved to the bottom of the preferrence list
            }

            this.retryContext = new RetryContext
            {
                RetryLocationIndex = retryLocationIndex,
                RetryRequestOnPreferredLocations = retryOnPreferredLocations,
            };

            return ShouldRetryResult.RetryAfter(retryDelay);
        }

        private ShouldRetryResult ShouldRetryOnSessionNotAvailable()
        {
            this.sessionTokenRetryCount++;

            if (!this.enableEndpointDiscovery)
            {
                // if endpoint discovery is disabled, the request cannot be retried anywhere else
                return ShouldRetryResult.NoRetry();
            }
            else
            {
                if (this.canUseMultipleWriteLocations)
                {
                    ReadOnlyCollection<Uri> endpoints = this.isReadRequest ? this.globalEndpointManager.ReadEndpoints : this.globalEndpointManager.WriteEndpoints;

                    if (this.sessionTokenRetryCount > endpoints.Count)
                    {
                        // When use multiple write locations is true and the request has been tried 
                        // on all locations, then don't retry the request
                        return ShouldRetryResult.NoRetry();
                    }
                    else
                    {
                        this.retryContext = new RetryContext()
                        {
                            RetryLocationIndex = this.sessionTokenRetryCount,
                            RetryRequestOnPreferredLocations = true
                        };

                        return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                    }
                }
                else
                {
                    if (this.sessionTokenRetryCount > 1)
                    {
                        // When cannot use multiple write locations, then don't retry the request if 
                        // we have already tried this request on the write location
                        return ShouldRetryResult.NoRetry();
                    }
                    else
                    {
                        this.retryContext = new RetryContext
                        {
                            RetryLocationIndex = 0,
                            RetryRequestOnPreferredLocations = false
                        };

                        return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                    }
                }
            }
        }

        /// <summary>
        /// For a ServiceUnavailable (503.0) we could be having a timeout from Direct/TCP locally or a request to Gateway request with a similar response due to an endpoint not yet available.
        /// We try and retry the request only if there are other regions available.
        /// </summary>
        private ShouldRetryResult ShouldRetryOnServiceUnavailable()
        {
            if (this.serviceUnavailableRetryCount++ >= ClientRetryPolicy.MaxServiceUnavailableRetryCount)
            {
                DefaultTrace.TraceInformation($"ShouldRetryOnServiceUnavailable() Not retrying. Retry count = {this.serviceUnavailableRetryCount}.");
                return ShouldRetryResult.NoRetry();
            }

            if (!this.canUseMultipleWriteLocations
                    && !this.isReadRequest)
            {
                // Write requests on single master cannot be retried, no other regions available
                return ShouldRetryResult.NoRetry();
            }

            int availablePreferredLocations = this.globalEndpointManager.PreferredLocationCount;

            if (availablePreferredLocations <= 1)
            {
                // No other regions to retry on
                DefaultTrace.TraceInformation($"ShouldRetryOnServiceUnavailable() Not retrying. No other regions available for the request. AvailablePreferredLocations = {availablePreferredLocations}.");
                return ShouldRetryResult.NoRetry();
            }

            DefaultTrace.TraceInformation($"ShouldRetryOnServiceUnavailable() Retrying. Received on endpoint {this.locationEndpoint}, IsReadRequest = {this.isReadRequest}.");

            // Retrying on second PreferredLocations
            // RetryCount is used as zero-based index
            this.retryContext = new RetryContext()
            {
                RetryLocationIndex = this.serviceUnavailableRetryCount,
                RetryRequestOnPreferredLocations = true
            };

            return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
        }

        private sealed class RetryContext
        {
            public int RetryLocationIndex { get; set; }
            public bool RetryRequestOnPreferredLocations { get; set; }
        }
    }
}