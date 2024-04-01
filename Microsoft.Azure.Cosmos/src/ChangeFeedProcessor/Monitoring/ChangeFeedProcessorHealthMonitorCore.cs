//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// A monitor which uses the default trace
    /// </summary>
    internal sealed class ChangeFeedProcessorHealthMonitorCore : ChangeFeedProcessorHealthMonitor
    {
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
            Extensions.TraceException(exception);
            DefaultTrace.TraceError($"Error detected for lease {leaseToken}. ");

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
    }
}