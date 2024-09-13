// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Text;

    /// <summary>
    /// The OpenTelemetryMetrics class contains internal static members to create and record Cosmos DB SDK metrics using OpenTelemetry. These metrics allow you to monitor the performance and resource consumption of Cosmos DB operations.
    /// </summary>
    internal static class OpenTelemetryMetrics
    {
        private static readonly Meter Meter = new Meter("Azure.Cosmos.SDK.Metrics");

        internal static readonly Counter<int> NumberOfOperationCallCounter =
            Meter.CreateCounter<int>("cosmos.client.op.calls", "#", "Number of operation calls");

        internal static readonly Histogram<double> RequestLatencyHistogram =
            Meter.CreateHistogram<double>("cosmos.client.op.latency", "#", "Total end-to-end duration of the operation");

        internal static readonly Histogram<double> RequestUnitsHistogram =
            Meter.CreateHistogram<double>("cosmos.client.op.RUs", "#", "Total request units per operation (sum of RUs for all requested needed when processing an operation)");

        internal static readonly Counter<int> MaxItemCounter =
           Meter.CreateCounter<int>("cosmos.client.op.maxItemCount", "#", "For feed operations (query, readAll, readMany, change feed) and batch operations this meter capture the requested maxItemCount per page/request");

        internal static readonly Counter<int> ActualItemCounter =
           Meter.CreateCounter<int>("cosmos.client.op.actualItemCount", "#", "For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service");

        internal static readonly Counter<int> RegionsContactedCounter =
           Meter.CreateCounter<int>("cosmos.client.op.regionsContacted", "#", "Number of regions contacted when executing an operation");
    }
}
