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
        internal int AggregationIntervalInSeconds { get; } = 600;

        internal MetricsPrecision MetricsPrecisions { get; } = new MetricsPrecision();
        internal NetworkTelemetryConfig NetworkTelemetryConfig { get; } = new NetworkTelemetryConfig();
        internal ClientTelemetryServiceConfig ClientTelemetryServiceConfig { get; } = new ClientTelemetryServiceConfig();
        
        internal ResourceType AllowedResourceTypes { get; } = ResourceType.Document;
        internal TimeSpan ClientTelemetryProcessorTimeOut { get; } = TimeSpan.FromMinutes(5);
    }

    internal class MetricsPrecision
    {
        internal int RequestLatencyPrecision { get; } = 4;
        internal int RequestChargePrecision { get; } = 2;
        internal int CpuPrecision { get; } = 2;
        internal int MemoryPrecision { get; } = 2;
        internal int AvailableThreadsPrecision { get; } = 2;
        internal int ThreadWaitIntervalInMsPrecision { get; } = 2;
        internal int NumberOfTcpConnectionPrecision { get; } = 2;
    }

    internal class NetworkTelemetryConfig
    {
        // Why 5 sec? As of now, if any network request is taking more than 5 millisecond sec, we will consider it slow request this value can be revisited in future
        internal TimeSpan NetworkLatencyThreshold { get; } = TimeSpan.FromMilliseconds(5);
        internal int NetworkRequestsSampleSizeThreshold { get; } = 10;
        internal int NetworkTelemetrySampleSize { get; } = 200;
        internal List<int> ExcludedStatusCodes { get; } = new List<int> { 404, 409, 412 };
    }

    internal class ClientTelemetryServiceConfig
    {
        internal int PayloadSizeThreshold { get; set; } = 1024 * 1024 * 2; // 2MB
    }
}
