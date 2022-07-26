// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    using System;

    /// <summary>
    /// Open Telemetry Configuration
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class OpenTelemetryConfig
    {
        /// <summary>
        /// Latency Threshold to send request diagnostics in Open Telemetry Attributes
        /// </summary>
        public TimeSpan LatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(250);
    }
}
