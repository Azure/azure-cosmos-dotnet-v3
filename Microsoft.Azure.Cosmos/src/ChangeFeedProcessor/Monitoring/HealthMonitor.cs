//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Monitoring
{
    using System.Threading.Tasks;

    /// <summary>
    /// A strategy for handling the situation when the change feed processor is not able to acquire lease due to unknown reasons.
    /// </summary>
    internal abstract class HealthMonitor
    {
        /// <summary>
        /// A logic to handle that exceptional situation.
        /// </summary>
        public abstract Task InspectAsync(HealthMonitoringRecord record);
    }
}