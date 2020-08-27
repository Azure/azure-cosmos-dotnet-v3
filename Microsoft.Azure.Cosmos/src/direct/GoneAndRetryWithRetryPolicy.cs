//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Policy to perform backoff retry on GoneException, InvalidPartitionException and RetryWithException
    /// TArg1: Perform force refresh.
    /// TArg2: TimeSpan for completing the work in the callback
    /// </summary>
    internal sealed class GoneAndRetryWithRetryPolicy : 
        IRetryPolicy<bool>, 
        IRetryPolicy<Tuple<bool, bool, TimeSpan>>, 
        IRetryPolicy<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>
    {
        private const int defaultWaitTimeInSeconds = 30;
        private const int minExecutionTimeInSeconds = 5;
        private const int initialBackoffSeconds = 1;
        private const int backoffMultiplier = 2;
        private const int maximumBackoffTimeInSeconds = 15;

        private const int minFailedReplicaCountToConsiderConnectivityIssue = 3;

        private Stopwatch durationTimer = new Stopwatch();
        private int attemptCount = 1;
        private int attemptCountInvalidPartition = 1;
        private int regionRerouteAttemptCount = 0;

        private TimeSpan minBackoffForRegionReroute;

        private RetryWithException lastRetryWithException = null;

        private readonly int waitTimeInSeconds;

        private readonly bool detectConnectivityIssues;

        // Don't penalise first retry with delay.
        private int currentBackoffSeconds = GoneAndRetryWithRetryPolicy.initialBackoffSeconds;

        private DocumentServiceRequest request;

        public GoneAndRetryWithRetryPolicy(
            DocumentServiceRequest request = null, 
            int? waitTimeInSecondsOverride = null, 
            TimeSpan minBackoffForRegionReroute = default(TimeSpan), 
            bool detectConnectivityIssues = false)
        {
            if (waitTimeInSecondsOverride.HasValue)
            {
                this.waitTimeInSeconds = waitTimeInSecondsOverride.Value;
            }
            else
            {
                this.waitTimeInSeconds = GoneAndRetryWithRetryPolicy.defaultWaitTimeInSeconds;
            }
            
            this.request = request;
            this.detectConnectivityIssues = detectConnectivityIssues;
            this.minBackoffForRegionReroute = minBackoffForRegionReroute;

            durationTimer.Start();
        }

        bool IRetryPolicy<bool>.InitialArgumentValue
        {
            get
            {
                return false;
            }
        }

        Tuple<bool, bool, TimeSpan> IRetryPolicy<Tuple<bool, bool, TimeSpan>>.InitialArgumentValue
        {
            get
            {
                return Tuple.Create(false, false, TimeSpan.FromSeconds(this.waitTimeInSeconds));
            }
        }

        Tuple<bool, bool, TimeSpan, int, int, TimeSpan> IRetryPolicy<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.InitialArgumentValue
        {
            get
            {
                return Tuple.Create(false, false, TimeSpan.FromSeconds(this.waitTimeInSeconds), 0, 0, TimeSpan.Zero);
            }
        }

        /// <summary>
        /// ShouldRetry method
        /// </summary>
        /// <param name="exception">Exception thrown by callback</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Is the retry helper should retry</returns>
        async Task<ShouldRetryResult<bool>> IRetryPolicy<bool>.ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>> result =
                await ((IRetryPolicy<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>)this).ShouldRetryAsync(exception, cancellationToken);

            if(result.ShouldRetry)
            {
                return ShouldRetryResult<bool>.RetryAfter(result.BackoffTime, result.PolicyArg1.Item1);
            }
            else
            {
                return ShouldRetryResult<bool>.NoRetry(result.ExceptionToThrow);
            }
        }

        async Task<ShouldRetryResult<Tuple<bool, bool, TimeSpan>>> IRetryPolicy<Tuple<bool, bool, TimeSpan>>.ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>> result =
                await ((IRetryPolicy<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>)this).ShouldRetryAsync(exception, cancellationToken);

            if (result.ShouldRetry)
            {
                return ShouldRetryResult<Tuple<bool, bool, TimeSpan>>.RetryAfter(result.BackoffTime, Tuple.Create(result.PolicyArg1.Item1, result.PolicyArg1.Item2, result.PolicyArg1.Item3));
            }
            else
            {
                return ShouldRetryResult<Tuple<bool, bool, TimeSpan>>.NoRetry(result.ExceptionToThrow);
            }
        }

        /// <summary>
        /// ShouldRetry method
        /// </summary>
        /// <param name="exception">Exception thrown by callback</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Is the retry helper should retry</returns>
        Task<ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>> IRetryPolicy<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.ShouldRetryAsync(
            Exception exception, 
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Exception exceptionToThrow = null;
            TimeSpan backoffTime = TimeSpan.FromSeconds(0);
            TimeSpan timeout = TimeSpan.FromSeconds(0);
            bool forceRefreshAddressCache = false;

            if (!(exception is GoneException) &&
                !(exception is RetryWithException) &&
                !(exception is PartitionIsMigratingException && (request.ServiceIdentity == null || request.ServiceIdentity.IsMasterService)) &&
                !(exception is InvalidPartitionException && (this.request.PartitionKeyRangeIdentity == null || this.request.PartitionKeyRangeIdentity.CollectionRid == null)) &&
                !(exception is PartitionKeyRangeIsSplittingException && this.request.ServiceIdentity == null))
            {
                // Have caller propagate original exception.
                this.durationTimer.Stop();
                return Task.FromResult(ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.NoRetry());
            }
            else if (exception is RetryWithException)
            {
                this.lastRetryWithException = exception as RetryWithException;
            }

            int remainingSeconds = this.waitTimeInSeconds - Convert.ToInt32(this.durationTimer.Elapsed.TotalSeconds);
            remainingSeconds = remainingSeconds > 0 ? remainingSeconds : 0;

            int currentAttemptCount = this.attemptCount;
            // Don't penalise first retry with delay.
            if (this.attemptCount++ > 1)
            {
                if (remainingSeconds <= 0)
                {
                    if (exception is GoneException ||
                        exception is PartitionIsMigratingException ||
                        exception is InvalidPartitionException ||
                        exception is PartitionKeyRangeGoneException ||
                        exception is PartitionKeyRangeIsSplittingException)
                    {
                        string message = string.Format("Received {0} after backoff/retry", exception.GetType().Name);

                        if (this.lastRetryWithException != null)
                        {
                            DefaultTrace.TraceError(
                                "{0} including at least one RetryWithException. " +
                                "Will fail the request with RetryWithException. Exception: {1}. RetryWithException: {2}",
                                message, exception.ToStringWithData(), this.lastRetryWithException.ToStringWithData());
                            exceptionToThrow = this.lastRetryWithException;
                        }
                        else
                        {
                            DefaultTrace.TraceError("{0}. Will fail the request. {1}", message, exception.ToStringWithData());

                            if (this.detectConnectivityIssues &&
                                this.request.RequestContext.ClientRequestStatistics != null &&
                                this.request.RequestContext.ClientRequestStatistics.IsCpuOverloaded)
                            {
                                exceptionToThrow = new ServiceUnavailableException(
                                    string.Format(
                                        RMResources.ClientCpuOverload,
                                        this.request.RequestContext.ClientRequestStatistics.FailedReplicas.Count,
                                        this.request.RequestContext.ClientRequestStatistics.RegionsContacted.Count == 0 ?
                                            1 : this.request.RequestContext.ClientRequestStatistics.RegionsContacted.Count));
                            }
                            else if (this.detectConnectivityIssues &&
                                this.request.RequestContext.ClientRequestStatistics != null &&
                                this.request.RequestContext.ClientRequestStatistics.FailedReplicas.Count >= GoneAndRetryWithRetryPolicy.minFailedReplicaCountToConsiderConnectivityIssue)
                            {

                                exceptionToThrow = new ServiceUnavailableException(
                                    string.Format(
                                        RMResources.ClientUnavailable, 
                                        this.request.RequestContext.ClientRequestStatistics.FailedReplicas.Count, 
                                        this.request.RequestContext.ClientRequestStatistics.RegionsContacted.Count == 0 ?
                                            1 : this.request.RequestContext.ClientRequestStatistics.RegionsContacted.Count), 
                                    exception);
                            }
                            else
                            {
                                exceptionToThrow = new ServiceUnavailableException(exception);
                            }
                        }
                    }
                    else
                    {
                        DefaultTrace.TraceError("Received retrywith exception after backoff/retry. Will fail the request. {0}", exception.ToStringWithData());
                    }

                    this.durationTimer.Stop();

                    return Task.FromResult(ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.NoRetry(exceptionToThrow));
                }

                backoffTime = TimeSpan.FromSeconds(Math.Min(Math.Min(this.currentBackoffSeconds, remainingSeconds), GoneAndRetryWithRetryPolicy.maximumBackoffTimeInSeconds));
                this.currentBackoffSeconds *= GoneAndRetryWithRetryPolicy.backoffMultiplier;
            }

            // Calculate the remaining time based after accounting for the backoff that we will perfom
            double timeoutInSeconds = remainingSeconds - backoffTime.TotalSeconds;
            timeout = timeoutInSeconds > 0 ? TimeSpan.FromSeconds(timeoutInSeconds) :
                TimeSpan.FromSeconds(GoneAndRetryWithRetryPolicy.minExecutionTimeInSeconds);

            if (backoffTime >= this.minBackoffForRegionReroute)
            {
                this.regionRerouteAttemptCount++;
            }

            if (exception is GoneException)
            {
                forceRefreshAddressCache = true; // indicate we are in retry.
            }
            else if (exception is PartitionIsMigratingException)
            {
                this.ClearRequestContext();
                this.request.ForceCollectionRoutingMapRefresh = true;
                this.request.ForceMasterRefresh = true;
                forceRefreshAddressCache = false;
            }
            else if (exception is InvalidPartitionException)
            {
                this.ClearRequestContext();
                this.request.RequestContext.GlobalCommittedSelectedLSN = -1;

                if (this.attemptCountInvalidPartition++ > 2)
                {
                    // for second InvalidPartitionException, stop retrying.
                    DefaultTrace.TraceCritical("Received second InvalidPartitionException after backoff/retry. Will fail the request. {0}", exception.ToStringWithData());
                    return Task.FromResult(ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.NoRetry(new ServiceUnavailableException(exception)));
                }

                if (this.request != null)
                {
                    this.request.ForceNameCacheRefresh = true;
                }
                else
                {
                    DefaultTrace.TraceCritical("Received unexpected invalid collection exception, request should be non-null.", exception.ToStringWithData());
                    return Task.FromResult(ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.NoRetry(new InternalServerErrorException(exception)));
                }
                // prevent the caller from refreshing fabric caches.
                forceRefreshAddressCache = false;
            }
            else if (exception is PartitionKeyRangeIsSplittingException)
            {
                this.ClearRequestContext();
                this.request.ForcePartitionKeyRangeRefresh = true;
                forceRefreshAddressCache = false;
            }
            else
            {
                // For RetryWithException, prevent the caller
                // from refreshing any caches.
                forceRefreshAddressCache = false;
            }

            DefaultTrace.TraceWarning(
                "GoneAndRetryWithRetryPolicy Received exception, will retry, attempt: {0}, regionRerouteAttempt: {1}, backoffTime: {2}, Timeout: {3}, Exception: {4}", 
                this.attemptCount, 
                this.regionRerouteAttemptCount, 
                backoffTime, 
                timeout,
                exception.ToStringWithData());
            return Task.FromResult(ShouldRetryResult<Tuple<bool, bool, TimeSpan, int, int, TimeSpan>>.RetryAfter(
                backoffTime, 
                Tuple.Create(forceRefreshAddressCache, true, timeout, currentAttemptCount, this.regionRerouteAttemptCount, backoffTime)));
        }

        private void ClearRequestContext()
        {
            this.request.RequestContext.TargetIdentity = null;
            this.request.RequestContext.ResolvedPartitionKeyRange = null;
            this.request.RequestContext.QuorumSelectedLSN = -1;
            this.request.RequestContext.QuorumSelectedStoreResponse = null;
        }
    }
}
