//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    internal interface ISampler<T>
    {
        public bool ShouldSample(T requestInfo);
    }
}
