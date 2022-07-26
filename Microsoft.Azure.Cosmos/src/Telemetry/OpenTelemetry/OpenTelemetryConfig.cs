// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;

#if PREVIEW
    public
#else
    internal
#endif
        class OpenTelemetryConfig
    {
        public TimeSpan LatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(250);
    }
}
