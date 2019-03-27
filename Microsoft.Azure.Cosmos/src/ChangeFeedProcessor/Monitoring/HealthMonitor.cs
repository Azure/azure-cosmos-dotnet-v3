//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring
{
    using System.Threading.Tasks;

    /// <summary>
    /// A strategy for handling the situation when the change feed processor is not able to acquire lease due to unknown reasons.
    /// </summary>
    public abstract class HealthMonitor
    {
        /// <summary>
        /// A logic to handle that exceptional situation.
        /// </summary>
        public abstract Task InspectAsync(HealthMonitoringRecord record);
    }
}