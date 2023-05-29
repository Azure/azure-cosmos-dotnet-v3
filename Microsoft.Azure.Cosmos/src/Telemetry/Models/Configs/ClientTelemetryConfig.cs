//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal class ClientTelemetryConfig
    {
        internal MetricsPrecision MetricsPrecisions { get; set; } = new MetricsPrecision();
        internal NetworkTelemetryConfig NetworkTelemetryConfig { get; set; } = new NetworkTelemetryConfig();
        internal ClientTelemetryServiceConfig ClientTelemetryServiceConfig { get; set; } = new ClientTelemetryServiceConfig();
        
        internal ResourceType AllowedResourceTypes { get; set; } = ResourceType.Document;
        internal TimeSpan ClientTelemetryProcessorTimeOut { get; set; } = TimeSpan.FromMinutes(5);
    }

    internal class MetricsPrecision
    {
        internal int RequestLatencyPrecision { get; set; } = 4;
        internal int RequestChargePrecision { get; set; } = 2;
        internal int CpuPrecision { get; set; } = 2;
        internal int MemoryPrecision { get; set; } = 2;
        internal int AvailableThreadsPrecision { get; set; } = 2;
        internal int ThreadWaitIntervalInMsPrecision { get; set; } = 2;
        internal int NumberOfTcpConnectionPrecision { get; set; } = 2;
    }

    internal class NetworkTelemetryConfig
    {
        // Why 5 sec? As of now, if any network request is taking more than 5 millisecond sec, we will consider it slow request this value can be revisited in future
        internal TimeSpan NetworkLatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(5);
        internal int NetworkRequestsSampleSizeThreshold { get; set; } = 10;
        internal int NetworkTelemetrySampleSize { get; set; } = 200;
        internal List<int> ExcludedStatusCodes { get; set; } = new List<int> { 404, 409, 412 };
    }

    internal class ClientTelemetryServiceConfig
    {
        internal int PayloadSizeThreshold { get; set; } = 1024 * 1024 * 2; // 2MB
    }
}
