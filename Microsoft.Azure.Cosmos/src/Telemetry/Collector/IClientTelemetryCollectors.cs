//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

    internal interface IClientTelemetryCollectors : IDisposable
    {
        public void CollectCacheInfo(Func<CacheTelemetryData> functionFordata);

        public void CollectOperationInfo(Func<OperationTelemetryData> functionFordata);
    }
}
