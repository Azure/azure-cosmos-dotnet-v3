//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if PREVIEW
    using static Microsoft.Azure.Cosmos.Container;
#else
    using static Microsoft.Azure.Cosmos.ContainerInternal;
#endif

    /// <summary>
    /// A monitor which uses the default trace
    /// </summary>
    internal sealed class ChangeFeedProcessorHealthMonitorCore : ChangeFeedProcessorHealthMonitor
    {
        private ChangeFeedMonitorErrorDelegate errorDelegate;
        private ChangeFeedMonitorLeaseAcquireDelegate acquireDelegate;
        private ChangeFeedMonitorLeaseReleaseDelegate releaseDelegate;

        public void SetDelegate(ChangeFeedMonitorErrorDelegate delegateCallback)
        {
            this.errorDelegate = delegateCallback;
        }

        public void SetDelegate(ChangeFeedMonitorLeaseAcquireDelegate delegateCallback)
        {
            this.acquireDelegate = delegateCallback;
        }

        public void SetDelegate(ChangeFeedMonitorLeaseReleaseDelegate delegateCallback)
        {
            this.releaseDelegate = delegateCallback;
        }

        public override Task NotifyLeaseAcquireAsync(string leaseToken)
        {
            if (this.acquireDelegate != null)
            {
                return this.acquireDelegate(leaseToken);
            }

            DefaultTrace.TraceInformation("Lease with token {0}: acquired", leaseToken);

            return Task.CompletedTask;
        }

        public override Task NotifyLeaseReleaseAsync(string leaseToken)
        {
            if (this.releaseDelegate != null)
            {
                return this.releaseDelegate(leaseToken);
            }

            DefaultTrace.TraceInformation("Lease with token {0}: released", leaseToken);

            return Task.CompletedTask;
        }

        public override Task NotifyErrorAsync(
             string leaseToken,
             Exception exception)
        {
            if (this.errorDelegate != null)
            {
                return this.errorDelegate(leaseToken, exception);
            }

            Extensions.TraceException(exception);
            DefaultTrace.TraceError($"Error detected for lease {leaseToken}. ");

            return Task.CompletedTask;
        }
    }
}