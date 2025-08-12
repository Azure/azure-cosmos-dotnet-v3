﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Policy to perform backoff retry on GoneException, InvalidPartitionException, RetryWithException, PartitionKeyRangeIsSplittingException, and PartitionKeyRangeGoneException, including their associated
    /// </summary>
    internal sealed class GoneAndRetryWithRequestRetryPolicy<TResponse> :
        IRequestRetryPolicy<GoneAndRetryRequestRetryPolicyContext, DocumentServiceRequest, TResponse> where TResponse : IRetriableResponse
    {
        struct ErrorOrResponse
        {
#pragma warning disable IDE0044 // Add readonly modifier
            private Exception exception;
            private int statusCode;
#pragma warning restore IDE0044 // Add readonly modifier

            public ErrorOrResponse(Exception ex, TResponse response)
            {
                exception = ex;
                this.statusCode = response != null ? (int)response.StatusCode : 0;
            }

            public ErrorOrResponse(Exception ex)
            {
                exception = ex;
                statusCode = 0;
            }

            private string ExceptionToString()
            {
                // If no error code is set, use WithMessageAndData
                return statusCode != 0 ? exception?.ToStringWithData() : exception?.ToStringWithMessageAndData();
            }

            public override string ToString() => exception != null ? this.ExceptionToString() : statusCode.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random());

        private const int defaultWaitTimeInMilliSeconds = 30000;
        private const int minExecutionTimeInMilliSeconds = 5000;
        private const int initialBackoffMilliSeconds = 1000;
        private const int backoffMultiplier = 2;
        private const int defaultMaximumBackoffTimeInMilliSeconds = 15000;

        // RetryWith default behavior
        private const int defaultInitialBackoffTimeForRetryWithInMilliseconds = 10;
        private const int defaultMaximumBackoffTimeForRetryWithInMilliseconds = 1000;
        private const int defaultRandomSaltForRetryWithInMilliseconds = 5;

        private const int minFailedReplicaCountToConsiderConnectivityIssue = 3;

        private readonly int maximumBackoffTimeInMilliSeconds;
        private readonly int maximumBackoffTimeInMillisecondsForRetryWith;
        private readonly int initialBackoffTimeInMilliSeconds;
        private readonly int initialBackoffTimeInMillisecondsForRetryWith;

        private readonly int? randomSaltForRetryWithMilliseconds;

#pragma warning disable IDE0044 // Add readonly modifier
        private Stopwatch durationTimer = new Stopwatch();
#pragma warning restore IDE0044 // Add readonly modifier
        private TimeSpan minBackoffForRegionReroute;
        private int attemptCount = 1;
        private int attemptCountInvalidPartition = 1;
        private int regionRerouteAttemptCount = 0;

        // Don't penalise first retry with delay.
        private int? currentBackoffMilliseconds;
        private int? currentBackoffMillisecondsForRetryWith;

        private RetryWithException lastRetryWithException = null;
        private Exception previousException = null; // Used in case of timeouts during retries

        private readonly int waitTimeInMilliseconds;
        private readonly int waitTimeInMillisecondsForRetryWith;
        private readonly bool detectConnectivityIssues;
        private readonly bool disableRetryWithPolicy;

        public GoneAndRetryWithRequestRetryPolicy(
            bool disableRetryWithPolicy,
            int? waitTimeInSecondsOverride = null,
            TimeSpan minBackoffForRegionReroute = default(TimeSpan),
            bool detectConnectivityIssues = false,
            RetryWithConfiguration retryWithConfiguration = null)
        {
            if (waitTimeInSecondsOverride.HasValue)
            {
                this.waitTimeInMilliseconds = waitTimeInSecondsOverride.Value * 1000;
            }
            else
            {
                this.waitTimeInMilliseconds = GoneAndRetryWithRequestRetryPolicy<TResponse>.defaultWaitTimeInMilliSeconds;
            }

            this.disableRetryWithPolicy = disableRetryWithPolicy;
            this.detectConnectivityIssues = detectConnectivityIssues;
            this.minBackoffForRegionReroute = minBackoffForRegionReroute;

            // Initial Context
            this.ExecuteContext.RemainingTimeInMsOnClientRequest = TimeSpan.FromMilliseconds(this.waitTimeInMilliseconds);
            this.ExecuteContext.TimeoutForInBackoffRetryPolicy = TimeSpan.Zero;

            this.initialBackoffTimeInMilliSeconds =
                GoneAndRetryWithRequestRetryPolicy<TResponse>.initialBackoffMilliSeconds;
            this.initialBackoffTimeInMillisecondsForRetryWith =
                retryWithConfiguration?.InitialRetryIntervalMilliseconds ?? GoneAndRetryWithRequestRetryPolicy<TResponse>.defaultInitialBackoffTimeForRetryWithInMilliseconds;
            this.maximumBackoffTimeInMilliSeconds =
                GoneAndRetryWithRequestRetryPolicy<TResponse>.defaultMaximumBackoffTimeInMilliSeconds;
            this.maximumBackoffTimeInMillisecondsForRetryWith =
                retryWithConfiguration?.MaximumRetryIntervalMilliseconds ?? GoneAndRetryWithRequestRetryPolicy<TResponse>.defaultMaximumBackoffTimeForRetryWithInMilliseconds;
            this.waitTimeInMillisecondsForRetryWith =
                retryWithConfiguration?.TotalWaitTimeMilliseconds ?? this.waitTimeInMilliseconds;

            this.randomSaltForRetryWithMilliseconds = retryWithConfiguration?.RandomSaltMaxValueMilliseconds ?? GoneAndRetryWithRequestRetryPolicy<TResponse>.defaultRandomSaltForRetryWithInMilliseconds;
            if (this.randomSaltForRetryWithMilliseconds != null && this.randomSaltForRetryWithMilliseconds < 1)
            {
                throw new ArgumentException($"{nameof(retryWithConfiguration.RandomSaltMaxValueMilliseconds)} must be a number greater than 1 or null");
            }

            this.durationTimer.Start();
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request) {}

        public GoneAndRetryRequestRetryPolicyContext ExecuteContext { get; } = new GoneAndRetryRequestRetryPolicyContext();

        public bool TryHandleResponseSynchronously(DocumentServiceRequest request, TResponse response, Exception exception, out ShouldRetryResult shouldRetryResult)
        {
            Exception exceptionToThrow = null;
            TimeSpan backoffTime = TimeSpan.FromSeconds(0);
            TimeSpan timeout = TimeSpan.FromSeconds(0);
            bool forceRefreshAddressCache = false;

            request.RequestContext.IsRetry = true;

            bool isRetryWith = false;
            if (!GoneAndRetryWithRequestRetryPolicy<TResponse>.IsBaseGone(response, exception) &&
                !GoneAndRetryWithRequestRetryPolicy<TResponse>.IsGoneWithLeaseNotFound(response, exception) &&
                !(request.DisableArchivalPartitionNotFoundRetry && GoneAndRetryWithRequestRetryPolicy<TResponse>.IsGoneWithArchivalPartitionNotPresent(response, exception)) &&
                !(exception is RetryWithException) &&
                !(GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionIsMigrating(response, exception) && (request.ServiceIdentity == null || request.ServiceIdentity.IsMasterService)) &&
                !(GoneAndRetryWithRequestRetryPolicy<TResponse>.IsInvalidPartition(response, exception) && (request.PartitionKeyRangeIdentity == null || request.PartitionKeyRangeIdentity.CollectionRid == null)) &&
                !(GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionKeySplitting(response, exception) && request.ServiceIdentity == null))
            {
                // Have caller propagate original exception / response.
                this.durationTimer.Stop();
                shouldRetryResult = ShouldRetryResult.NoRetry();
                return true;
            }
            else if (exception is RetryWithException)
            {
                // If the RetryWithPolicy is disabled then
                // return the no retry to let it propagate the RetryWith exception.
                if (disableRetryWithPolicy)
                {
                    DefaultTrace.TraceWarning(
                        "The GoneAndRetryWithRequestRetryPolicy is configured with disableRetryWithPolicy to true. Retries on 449(RetryWith) exceptions has been disabled. This is by design to allow users to handle the exception: {0}",
                        new ErrorOrResponse(exception));
                    this.durationTimer.Stop();
                    shouldRetryResult = ShouldRetryResult.NoRetry();
                    return true;
                }

                isRetryWith = true;
                this.lastRetryWithException = exception as RetryWithException;
            }
            else if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsGoneWithLeaseNotFound(response, exception))
            {
                DefaultTrace.TraceWarning(
                    "The GoneAndRetryWithRequestRetryPolicy has hit 410 with lease not found exception: {0}. Converting it to 503 to trigger cross regional failover",
                    new ErrorOrResponse(exception));

                exceptionToThrow = ServiceUnavailableException.Create(
                    SubStatusCodes.LeaseNotFound,
                    innerException: exception);

                this.durationTimer.Stop();

                shouldRetryResult = ShouldRetryResult.NoRetry(exceptionToThrow);
                return true;
            }
            else if (request.DisableArchivalPartitionNotFoundRetry &&
                 GoneAndRetryWithRequestRetryPolicy<TResponse>.IsGoneWithArchivalPartitionNotPresent(response, exception))
            {
                DefaultTrace.TraceInformation(
                    "The GoneAndRetryWithRequestRetryPolicy has hit 410 with archival partition not present exception. Converting it to 503.");

                exceptionToThrow = ServiceUnavailableException.Create(
                    SubStatusCodes.ArchivalPartitionNotPresent,
                    innerException: exception);
                this.durationTimer.Stop();

                shouldRetryResult = ShouldRetryResult.NoRetry(exceptionToThrow);
                return true;
            }

            int remainingMilliseconds;
            if (isRetryWith)
            {
                remainingMilliseconds = this.waitTimeInMillisecondsForRetryWith - Convert.ToInt32(this.durationTimer.Elapsed.TotalMilliseconds);
            }
            else
            {
                remainingMilliseconds = this.waitTimeInMilliseconds - Convert.ToInt32(this.durationTimer.Elapsed.TotalMilliseconds);
            }

            remainingMilliseconds = remainingMilliseconds > 0 ? remainingMilliseconds : 0;
            int currentAttemptCount = this.attemptCount;
            // Don't penalise first retry with delay.
            if (this.attemptCount++ > 1)
            {
                if (remainingMilliseconds <= 0)
                {
                    if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsBaseGone(response, exception) ||
                        GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionIsMigrating(response, exception) ||
                        GoneAndRetryWithRequestRetryPolicy<TResponse>.IsInvalidPartition(response, exception) ||
                        GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionKeyRangeGone(response, exception) ||
                        GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionKeySplitting(response, exception))
                    {
                        string message = string.Format("Received {0} after backoff/retry", exception?.GetType().Name ?? response?.StatusCode.ToString());

                        if (this.lastRetryWithException != null)
                        {
                            DefaultTrace.TraceError(
                                "{0} including at least one RetryWithException. " +
                                "Will fail the request with RetryWithException. Exception: {1}. RetryWithException: {2}",
                                message,
                                new ErrorOrResponse(exception, response),
                                new ErrorOrResponse(this.lastRetryWithException));
                            exceptionToThrow = this.lastRetryWithException;
                        }
                        else
                        {
                            DefaultTrace.TraceError("{0}. Will fail the request. {1}", message,
                                new ErrorOrResponse(exception, response));

                            SubStatusCodes exceptionSubStatus = DocumentClientException.GetExceptionSubStatusForGoneRetryPolicy(exception);
                            if (exceptionSubStatus == SubStatusCodes.TimeoutGenerated410)
                            {
                                // If it was a gone exception becuase of a timeout (thrown when the lower layers themselves are retrying like in the StoreReader), take the substatus of the previous exception
                                if (this.previousException != null)
                                {
                                    exceptionSubStatus = DocumentClientException.GetExceptionSubStatusForGoneRetryPolicy(this.previousException);
                                }
                            }

                            if (this.detectConnectivityIssues &&
                                request.RequestContext.ClientRequestStatistics != null &&
                                request.RequestContext.ClientRequestStatistics.IsCpuHigh.GetValueOrDefault(false))
                            {

                                exceptionToThrow = new ServiceUnavailableException(
                                string.Format(
                                    RMResources.ClientCpuOverload,
                                    request.RequestContext.ClientRequestStatistics.FailedReplicas.Count,
                                    request.RequestContext.ClientRequestStatistics.RegionsContacted.Count == 0 ?
                                        1 : request.RequestContext.ClientRequestStatistics.RegionsContacted.Count),
                                SubStatusCodes.Client_CPUOverload);
                            }
                            else if (this.detectConnectivityIssues &&
                                    request.RequestContext.ClientRequestStatistics != null &&
                                    request.RequestContext.ClientRequestStatistics.IsCpuThreadStarvation.GetValueOrDefault(false))
                            {
                                exceptionToThrow = new ServiceUnavailableException(
                                string.Format(
                                    RMResources.ClientCpuThreadStarvation,
                                    request.RequestContext.ClientRequestStatistics.FailedReplicas.Count,
                                    request.RequestContext.ClientRequestStatistics.RegionsContacted.Count == 0 ?
                                        1 : request.RequestContext.ClientRequestStatistics.RegionsContacted.Count),
                                SubStatusCodes.Client_ThreadStarvation);
                            }
                            else if (this.detectConnectivityIssues &&
                                    request.RequestContext.ClientRequestStatistics != null &&
                                    request.RequestContext.ClientRequestStatistics.FailedReplicas.Count >= GoneAndRetryWithRequestRetryPolicy<TResponse>.minFailedReplicaCountToConsiderConnectivityIssue)
                            {
                                exceptionToThrow = new ServiceUnavailableException(
                                    string.Format(
                                        RMResources.ClientUnavailable,
                                        request.RequestContext.ClientRequestStatistics.FailedReplicas.Count,
                                        request.RequestContext.ClientRequestStatistics.RegionsContacted.Count == 0 ?
                                            1 : request.RequestContext.ClientRequestStatistics.RegionsContacted.Count),
                                    exception,
                                    exceptionSubStatus);
                            }
                            else
                            {
                                exceptionToThrow = ServiceUnavailableException.Create(exceptionSubStatus, innerException: exception);
                            }
                        }
                    }
                    else
                    {
                        DefaultTrace.TraceError("Received retry with exception after backoff/retry. Will fail the request. {0}", new ErrorOrResponse(exception, response));
                        exceptionToThrow = exception;
                    }

                    this.durationTimer.Stop();

                    shouldRetryResult = ShouldRetryResult.NoRetry(exceptionToThrow);
                    return true;
                }

                this.currentBackoffMillisecondsForRetryWith =
                    this.currentBackoffMillisecondsForRetryWith ?? this.initialBackoffTimeInMillisecondsForRetryWith;

                this.currentBackoffMilliseconds =
                    this.currentBackoffMilliseconds ?? this.initialBackoffTimeInMilliSeconds;

                if (isRetryWith)
                {
                    int retryWithMilliseconds = this.currentBackoffMillisecondsForRetryWith.Value;
                    if (this.randomSaltForRetryWithMilliseconds != null)
                    {
                        retryWithMilliseconds +=
                            GoneAndRetryWithRequestRetryPolicy<TResponse>.Random.Value.Next(
                                1,
                                this.randomSaltForRetryWithMilliseconds.Value);
                    }

                    backoffTime = TimeSpan.FromMilliseconds(Math.Min(Math.Min(retryWithMilliseconds, remainingMilliseconds), this.maximumBackoffTimeInMillisecondsForRetryWith));

                    // Cap this.currentBackoffMillisecondsForRetryWith to maximum to avoid overflow
                    this.currentBackoffMillisecondsForRetryWith = Math.Min(this.currentBackoffMillisecondsForRetryWith.Value * GoneAndRetryWithRequestRetryPolicy<TResponse>.backoffMultiplier, this.maximumBackoffTimeInMillisecondsForRetryWith);
                }
                else
                {
                    backoffTime = TimeSpan.FromMilliseconds(Math.Min(Math.Min(this.currentBackoffMilliseconds.Value, remainingMilliseconds), this.maximumBackoffTimeInMilliSeconds));

                    // Cap this.currentBackoffMilliseconds to maximum to avoid overflow
                    this.currentBackoffMilliseconds = Math.Min(this.currentBackoffMilliseconds.Value * GoneAndRetryWithRequestRetryPolicy<TResponse>.backoffMultiplier, this.maximumBackoffTimeInMilliSeconds);
                }
            }

            // Calculate the remaining time based after accounting for the backoff that we will perform
            double timeoutInMilliSeconds = remainingMilliseconds - backoffTime.TotalMilliseconds;
            timeout = timeoutInMilliSeconds > 0 ? TimeSpan.FromMilliseconds(timeoutInMilliSeconds) :
                TimeSpan.FromMilliseconds(GoneAndRetryWithRequestRetryPolicy<TResponse>.minExecutionTimeInMilliSeconds);

            if (backoffTime >= this.minBackoffForRegionReroute)
            {
                this.regionRerouteAttemptCount++;
            }

            if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsBaseGone(response, exception))
            {
                forceRefreshAddressCache = true; // indicate we are in retry.
            }
            else if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionIsMigrating(response, exception))
            {
                GoneAndRetryWithRequestRetryPolicy<TResponse>.ClearRequestContext(request);
                request.ForceCollectionRoutingMapRefresh = true;
                request.ForceMasterRefresh = true;
                forceRefreshAddressCache = false;
            }
            else if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsInvalidPartition(response, exception))
            {
                GoneAndRetryWithRequestRetryPolicy<TResponse>.ClearRequestContext(request);
                request.RequestContext.GlobalCommittedSelectedLSN = -1;

                if (this.attemptCountInvalidPartition++ > 2)
                {
                    // for third InvalidPartitionException, stop retrying.
                    SubStatusCodes exceptionSubStatusCode = DocumentClientException.GetExceptionSubStatusForGoneRetryPolicy(exception);
                    DefaultTrace.TraceCritical("Received second InvalidPartitionException after backoff/retry. Will fail the request. {0}",
                        new ErrorOrResponse(exception, response));
                    shouldRetryResult = ShouldRetryResult.NoRetry(ServiceUnavailableException.Create(exceptionSubStatusCode, innerException: exception));
                    return true;
                }

                if (request != null)
                {
                    request.ForceNameCacheRefresh = true;
                }
                else
                {
                    DefaultTrace.TraceCritical("Received unexpected invalid collection exception, request should be non-null. {0}", new ErrorOrResponse(exception, response));
                    shouldRetryResult = ShouldRetryResult.NoRetry(new InternalServerErrorException(exception));
                    return true;
                }

                // prevent the caller from refreshing fabric caches.
                forceRefreshAddressCache = false;
            }
            else if (GoneAndRetryWithRequestRetryPolicy<TResponse>.IsPartitionKeySplitting(response, exception))
            {
                GoneAndRetryWithRequestRetryPolicy<TResponse>.ClearRequestContext(request);
                request.ForcePartitionKeyRangeRefresh = true;
                forceRefreshAddressCache = false;
            }
            else
            {
                // For RetryWithException, prevent the caller
                // from refreshing any caches.
                forceRefreshAddressCache = false;
            }

            DefaultTrace.TraceWarning(
                "GoneAndRetryWithRequestRetryPolicy Received exception, will retry, attempt: {0}, regionRerouteAttempt: {1}, backoffTime: {2}, Timeout: {3}, Exception: {4}",
                this.attemptCount,
                this.regionRerouteAttemptCount,
                backoffTime,
                timeout,
                new ErrorOrResponse(exception, response));

            shouldRetryResult = ShouldRetryResult.RetryAfter(backoffTime);
            this.previousException = exception;

            // Update context
            this.ExecuteContext.ForceRefresh = forceRefreshAddressCache;
            this.ExecuteContext.IsInRetry = true;
            this.ExecuteContext.RemainingTimeInMsOnClientRequest = timeout;
            this.ExecuteContext.ClientRetryCount = currentAttemptCount;
            this.ExecuteContext.RegionRerouteAttemptCount = this.regionRerouteAttemptCount;
            this.ExecuteContext.TimeoutForInBackoffRetryPolicy = backoffTime;
            return true;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(DocumentServiceRequest request, TResponse response, Exception exception, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private static bool IsBaseGone(TResponse response, Exception exception)
        {
            return exception is GoneException 
                || (response?.StatusCode == HttpStatusCode.Gone &&
                   (response?.SubStatusCode == SubStatusCodes.Unknown || (response != null && response.SubStatusCode.IsSDKGeneratedSubStatus())));
        }

        private static bool IsPartitionIsMigrating(TResponse response, Exception exception)
        {
            return exception is PartitionIsMigratingException 
                || (response?.StatusCode == HttpStatusCode.Gone && response?.SubStatusCode == SubStatusCodes.CompletingPartitionMigration);
        }

        private static bool IsInvalidPartition(TResponse response, Exception exception)
        {
            return exception is InvalidPartitionException
                || (response?.StatusCode == HttpStatusCode.Gone && response?.SubStatusCode == SubStatusCodes.NameCacheIsStale);
        }

        private static bool IsPartitionKeySplitting(TResponse response, Exception exception)
        {
            return exception is PartitionKeyRangeIsSplittingException
                || (response?.StatusCode == HttpStatusCode.Gone && response?.SubStatusCode == SubStatusCodes.CompletingSplit);
        }

        private static bool IsPartitionKeyRangeGone(TResponse response, Exception exception)
        {
            return exception is PartitionKeyRangeGoneException
                || (response?.StatusCode == HttpStatusCode.Gone && response?.SubStatusCode == SubStatusCodes.PartitionKeyRangeGone);
        }

        private static bool IsGoneWithLeaseNotFound(TResponse response, Exception exception)
        {
            return exception is LeaseNotFoundException
                || (response?.StatusCode == HttpStatusCode.Gone &&
                   (response?.SubStatusCode == SubStatusCodes.LeaseNotFound));
        }

        private static bool IsGoneWithArchivalPartitionNotPresent(TResponse response, Exception exception)
        {
            return exception is ArchivalPartitionNotPresentException
                  || (response?.StatusCode == HttpStatusCode.Gone &&
                     (response?.SubStatusCode == SubStatusCodes.ArchivalPartitionNotPresent));
        }

        private static void ClearRequestContext(DocumentServiceRequest request)
        {
            request.RequestContext.TargetIdentity = null;
            request.RequestContext.ResolvedPartitionKeyRange = null;
            request.RequestContext.QuorumSelectedLSN = -1;
            request.RequestContext.UpdateQuorumSelectedStoreResponse(null);
        }
    }

    internal sealed class RetryWithConfiguration
    {
        public int? InitialRetryIntervalMilliseconds { get; set; }

        public int? MaximumRetryIntervalMilliseconds { get; set; }

        public int? RandomSaltMaxValueMilliseconds { get; set; }

        public int? TotalWaitTimeMilliseconds { get; set; }
    }

    internal sealed class GoneAndRetryRequestRetryPolicyContext
    {
        public bool ForceRefresh { get; set; }

        public bool IsInRetry { get; set; }

        public TimeSpan RemainingTimeInMsOnClientRequest { get; set; }

        public int ClientRetryCount { get; set; }

        public int RegionRerouteAttemptCount { get; set; }

        public TimeSpan TimeoutForInBackoffRetryPolicy { get; set; }
    }
}
