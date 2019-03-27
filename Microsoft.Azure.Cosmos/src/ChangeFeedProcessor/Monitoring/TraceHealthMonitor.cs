//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    /// <summary>
    /// A monitor which logs the errors only.
    /// </summary>
    internal sealed class TraceHealthMonitor : HealthMonitor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        /// <inheritdoc />
        public override Task InspectAsync(HealthMonitoringRecord record)
        {
            if (record.Severity == HealthSeverity.Error)
            {
                Logger.ErrorException($"Unhealthiness detected in the operation {record.Operation} for {record.Lease}. ", record.Exception);
            }

            return Task.FromResult(true);
        }
    }
}