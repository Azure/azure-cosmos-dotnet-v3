//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Sampler
{
    using System;

    internal interface IClientTelemetrySampler<T>
    {
        internal bool ShouldSample(T statisticsObj);
    }
}
