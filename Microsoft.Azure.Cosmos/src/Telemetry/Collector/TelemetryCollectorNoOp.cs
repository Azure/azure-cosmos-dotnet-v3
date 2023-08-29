﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System;

    internal class TelemetryCollectorNoOp : ITelemetryCollector
    {
        public void CollectCacheInfo(string cacheName, Func<TelemetryInformation> functionFordata)
        {
            //NoOps
        }

        public void CollectOperationAndNetworkInfo(Func<TelemetryInformation> functionFordata)
        {
            //NoOps
        }
    }
}