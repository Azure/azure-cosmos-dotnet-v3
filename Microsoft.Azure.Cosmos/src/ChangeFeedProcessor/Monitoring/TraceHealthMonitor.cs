//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// A monitor which uses the default trace
    /// </summary>
    internal sealed class TraceHealthMonitor : ChangeFeedProcessorHealthMonitor
    {
        public override Task NotifyLeaseAcquireAsync(string leaseToken)
        {
            DefaultTrace.TraceInformation("Lease with token {0}: acquired", leaseToken);

            return Task.CompletedTask;
        }

        public override Task NotifyLeaseReleaseAsync(string leaseToken)
        {
            DefaultTrace.TraceInformation("Lease with token {0}: released", leaseToken);

            return Task.CompletedTask;
        }

        public override Task NotifyErrorAsync(
             string leaseToken,
             Exception exception)
        {
            Extensions.TraceException(exception);
            DefaultTrace.TraceError($"Error detected for lease {leaseToken}. ");

            return Task.CompletedTask;
        }
    }
}