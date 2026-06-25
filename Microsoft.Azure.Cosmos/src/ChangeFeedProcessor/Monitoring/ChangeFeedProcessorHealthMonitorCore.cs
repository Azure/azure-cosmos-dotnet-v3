//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// A monitor which uses the default trace
    /// </summary>
    internal sealed class ChangeFeedProcessorHealthMonitorCore : ChangeFeedProcessorHealthMonitor
    {
        /// <summary>
        /// Number of consecutive identical errors observed on a single lease before the monitor escalates
        /// to a critical "stuck lease" trace. A lease that keeps failing on the same error (for example a
        /// poison-message deserialization failure) never advances its continuation, so this escalation makes
        /// the otherwise-silent loop visible even when no error delegate has been registered.
        /// </summary>
        private const int StuckLeaseConsecutiveErrorThreshold = 5;

        private readonly ConcurrentDictionary<string, LeaseErrorState> leaseErrorStates = new ConcurrentDictionary<string, LeaseErrorState>();

        private ChangeFeedMonitorErrorDelegate errorDelegate;
        private ChangeFeedMonitorLeaseAcquireDelegate acquireDelegate;
        private ChangeFeedMonitorLeaseReleaseDelegate releaseDelegate;

        public void SetErrorDelegate(ChangeFeedMonitorErrorDelegate delegateCallback)
        {
            this.errorDelegate = delegateCallback;
        }

        public void SetLeaseAcquireDelegate(ChangeFeedMonitorLeaseAcquireDelegate delegateCallback)
        {
            this.acquireDelegate = delegateCallback;
        }

        public void SetLeaseReleaseDelegate(ChangeFeedMonitorLeaseReleaseDelegate delegateCallback)
        {
            this.releaseDelegate = delegateCallback;
        }

        public override async Task NotifyLeaseAcquireAsync(string leaseToken)
        {
            DefaultTrace.TraceInformation("Lease with token {0}: acquired", leaseToken);

            if (this.acquireDelegate != null)
            {
                try
                {
                    await this.acquireDelegate(leaseToken);
                }
                catch (Exception ex)
                {
                    Extensions.TraceException(ex);
                    DefaultTrace.TraceError($"Lease acquire notification failed for {leaseToken}. ");
                }
            }
        }

        public override async Task NotifyLeaseReleaseAsync(string leaseToken)
        {
            DefaultTrace.TraceInformation("Lease with token {0}: released", leaseToken);

            if (this.releaseDelegate != null)
            {
                try
                {
                    await this.releaseDelegate(leaseToken);
                }
                catch (Exception ex)
                {
                    Extensions.TraceException(ex);
                    DefaultTrace.TraceError($"Lease release notification failed for {leaseToken}. ");
                }
            }
        }

        public override async Task NotifyErrorAsync(
             string leaseToken,
             Exception exception)
        {
            this.NotifyErrorDefault(leaseToken, exception);

            if (this.errorDelegate != null)
            {
                try
                {
                    await this.errorDelegate(leaseToken, exception);
                }
                catch (Exception ex)
                {
                    Extensions.TraceException(ex);
                    DefaultTrace.TraceError($"Error notification failed for {leaseToken}. ");
                }
            }
        }

        /// <summary>
        /// Always-on default error notification. This runs regardless of whether the customer registered an
        /// error delegate so that change feed processing failures (most importantly poison-message
        /// deserialization loops) are never silent. It emits structured diagnostics, distinguishes user/observer
        /// failures from infrastructure failures, and escalates to a critical trace once the same error has
        /// repeated enough times on a single lease to indicate the lease is stuck with no forward progress.
        /// </summary>
        private void NotifyErrorDefault(string leaseToken, Exception exception)
        {
            Extensions.TraceException(exception);

            int consecutiveCount = this.TrackConsecutiveError(leaseToken, exception);

            if (exception is ChangeFeedProcessorUserException userException)
            {
                ChangeFeedProcessorContext context = userException.ChangeFeedProcessorContext;
                Exception innerException = userException.InnerException ?? userException;

                DefaultTrace.TraceError(
                    "Change feed processor delegate failed for lease {0} (feed range: {1}). " +
                    "Inner exception {2}: {3}. The current batch will be retried and the lease will not advance " +
                    "until the delegate succeeds for this batch. Consecutive failures on this lease: {4}. " +
                    "Register an error delegate via WithErrorNotification or use a stream/manual-checkpoint handler " +
                    "to identify and skip the offending document if this is a poison message.",
                    leaseToken,
                    context?.FeedRange?.ToString() ?? "unknown",
                    innerException.GetType().FullName,
                    innerException.Message,
                    consecutiveCount);
            }
            else
            {
                DefaultTrace.TraceError(
                    "Error detected for lease {0}. Exception {1}: {2}. Consecutive failures on this lease: {3}.",
                    leaseToken,
                    exception?.GetType().FullName ?? "unknown",
                    exception?.Message ?? string.Empty,
                    consecutiveCount);
            }

            if (consecutiveCount == StuckLeaseConsecutiveErrorThreshold)
            {
                DefaultTrace.TraceCritical(
                    "Lease {0} appears to be stuck: the same error has occurred {1} consecutive times and the lease " +
                    "is making no forward progress. This is the signature of a poison message that fails to process " +
                    "(for example a deserialization error) and is reprocessed indefinitely. To recover, register an " +
                    "error delegate via WithErrorNotification, or switch to a stream handler with per-document " +
                    "try/catch and manual checkpoint so the offending document can be identified and skipped.",
                    leaseToken,
                    consecutiveCount);
            }
        }

        /// <summary>
        /// Tracks the number of consecutive failures sharing the same error signature for a given lease. The
        /// counter resets whenever the error signature changes, so transient unrelated failures do not trigger a
        /// false stuck-lease escalation.
        /// </summary>
        private int TrackConsecutiveError(string leaseToken, Exception exception)
        {
            string signature = ChangeFeedProcessorHealthMonitorCore.BuildErrorSignature(exception);

            LeaseErrorState state = this.leaseErrorStates.AddOrUpdate(
                leaseToken,
                addValueFactory: _ => new LeaseErrorState { Signature = signature, ConsecutiveCount = 1 },
                updateValueFactory: (_, existing) =>
                {
                    if (string.Equals(existing.Signature, signature, StringComparison.Ordinal))
                    {
                        existing.ConsecutiveCount++;
                    }
                    else
                    {
                        existing.Signature = signature;
                        existing.ConsecutiveCount = 1;
                    }

                    return existing;
                });

            return state.ConsecutiveCount;
        }

        private static string BuildErrorSignature(Exception exception)
        {
            if (exception == null)
            {
                return "null";
            }

            Exception inner = (exception as ChangeFeedProcessorUserException)?.InnerException ?? exception.InnerException;

            return inner != null
                ? $"{exception.GetType().FullName}|{inner.GetType().FullName}"
                : exception.GetType().FullName;
        }

        private sealed class LeaseErrorState
        {
            public string Signature { get; set; }

            public int ConsecutiveCount { get; set; }
        }
    }
}