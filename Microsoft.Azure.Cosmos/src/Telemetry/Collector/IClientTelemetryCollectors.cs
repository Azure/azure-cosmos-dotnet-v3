//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

    internal interface IClientTelemetryCollectors : IDisposable
    {
        /// <summary>
        /// Collect information required to collect the telemetry information for the cache.
        /// </summary>
        /// <param name="functionFordata"> delegate that encapsulates a method that returns a CacheTelemetryInformation object</param>
        public void CollectCacheInfo(Func<CacheTelemetryInformation> functionFordata);

        /// <summary>
        /// Collect information required to collect the telemetry information for the operation.
        /// </summary>
        /// <param name="functionFordata"> delegate that encapsulates a method that returns a OperationTelemetryInformation object</param>
        public void CollectOperationInfo(Func<OperationTelemetryInformation> functionFordata);
    }
}
