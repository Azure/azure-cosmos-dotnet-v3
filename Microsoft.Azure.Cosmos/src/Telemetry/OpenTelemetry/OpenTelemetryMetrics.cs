// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Text;
    using Microsoft.Azure.Cosmos.Telemetry;

    /// <summary>
    /// The OpenTelemetryMetrics class contains internal static members to create and record Cosmos DB SDK metrics using OpenTelemetry. These metrics allow you to monitor the performance and resource consumption of Cosmos DB operations.
    /// </summary>
    internal static class OpenTelemetryMetrics
    {
        internal static readonly Meter CosmosMeter = new Meter("Azure.Cosmos.Client");

        internal static readonly Counter<int> NumberOfOperationCallCounter =
            CosmosMeter.CreateCounter<int>(name: "cosmos.client.op.calls", 
                unit: "#", 
                description: "Number of operation calls");

        internal static readonly Histogram<double> RequestLatencyHistogram =
            CosmosMeter.CreateHistogram<double>(name: "cosmos.client.op.latency", 
                unit: "#", 
                description: "Total end-to-end duration of the operation");

        internal static readonly Histogram<double> RequestUnitsHistogram =
            CosmosMeter.CreateHistogram<double>(name: "cosmos.client.op.RUs", 
                unit: "#", 
                description: "Total request units per operation (sum of RUs for all requested needed when processing an operation)");

        internal static readonly ObservableGauge<int> maxItemGauge = 
            CosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.maxItemCount",
               observeValue: () => OpenTelemetryMetricsCollector.GetMaxItemCount(),
               unit: "#",
               description: "For feed operations (query, readAll, readMany, change feed) and batch operations this meter capture the requested maxItemCount per page/request");

        /*internal static readonly ObservableGauge<int> ActualItemCounter =
           CosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.actualItemCount", 
               unit: "#", 
               description: "For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service");

        internal static readonly ObservableGauge<int> RegionsContactedCounter =
           CosmosMeter.CreateObservableGauge<int>(name: "cosmos.client.op.regionsContacted", 
               unit: "#", 
               description: "Number of regions contacted when executing an operation");*/

    }
}
