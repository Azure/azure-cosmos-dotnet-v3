//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// A monitor which logs the errors only.
    /// </summary>
    internal sealed class TraceHealthMonitor : HealthMonitor
    {
        /// <inheritdoc />
        public override Task InspectAsync(HealthMonitoringRecord record)
        {
            if (record.Severity == HealthSeverity.Error)
            {
                Extensions.TraceException(record.Exception);
                DefaultTrace.TraceError($"Unhealthiness detected in the operation {record.Operation} for {record.Lease}. ");
            }

            return Task.FromResult(true);
        }
    }
}