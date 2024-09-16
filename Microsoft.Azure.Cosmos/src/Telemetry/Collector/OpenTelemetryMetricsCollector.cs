//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

    /// <summary>
    /// The OpenTelemetryMetricsCollector class is responsible for collecting and recording Cosmos DB operational metrics, such as item counts, request latency, request units, and regions contacted. 
    /// This data is captured using the OpenTelemetry metrics API, which allows tracking and analysis of Cosmos DB operations at a granular level.
    /// </summary>
    internal class OpenTelemetryMetricsCollector : ITelemetryCollector
    {
        private readonly string clientId;
        private readonly string accountName;

        private static readonly ConcurrentBag<Tuple<int, KeyValuePair<string, object>[]>> maxItemCounts = new ();

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryMetricsCollector class.
        /// </summary>
        /// <param name="clientId">A unique identifier for the Cosmos DB client instance</param>
        /// <param name="accountName">The Cosmos DB account name.</param>
        public OpenTelemetryMetricsCollector(string clientId, string accountName)
        {
            this.clientId = clientId;
            this.accountName = accountName;
        }

        public void CollectCacheInfo(string cacheName, Func<TelemetryInformation> getTelemetryInformation)
        {
            // No OP
        }

        /// <summary>
        /// Collects telemetry data related to operations and network information, including request performance, item counts, and regions contacted.
        /// </summary>
        /// <param name="getTelemetryInformation"> A function that provides telemetry details such as operation type, status code, consistency level, and request charge.</param>
        /// <remarks>This method gathers telemetry information, including details such as the database, container, operation type, status code, consistency level, and partition key range ID. It uses these dimensions to push metrics to OpenTelemetry, enabling tracking of performance metrics such as request latency, request charge, and item counts.</remarks>
        public void CollectOperationAndNetworkInfo(Func<TelemetryInformation> getTelemetryInformation)
        {
            TelemetryInformation telemetryInformation = getTelemetryInformation();

            KeyValuePair<string, object>[] dimensions = new[]
            {
                new KeyValuePair<string, object>("Container", $"{this.accountName}/{telemetryInformation.DatabaseId}/{telemetryInformation.ContainerId}"),
                new KeyValuePair<string, object>("Operation", telemetryInformation.OperationType),
                new KeyValuePair<string, object>("OperationStatusCode", telemetryInformation.StatusCode),
                new KeyValuePair<string, object>("ClientCorrelationId", this.clientId),
                new KeyValuePair<string, object>("ConsistencyLevel", telemetryInformation.ConsistencyLevel),
                new KeyValuePair<string, object>("PartitionKeyRangeId", telemetryInformation.PartitionKeyRangeId),
            };

            PushOperationLevelMetrics(telemetryInformation, dimensions);
        }

        /// <summary>
        /// Pushes various operation-level metrics to OpenTelemetry.
        /// </summary>
        /// <param name="telemetryInformation">Contains telemetry data related to the operation, such as item counts, request charge, and latency.</param>
        /// <param name="dimensions">Key-value pairs representing various metadata about the operation (e.g., container, operation type, consistency level).</param>
        private static void PushOperationLevelMetrics(TelemetryInformation telemetryInformation, KeyValuePair<string, object>[] dimensions)
        {
            maxItemCounts.Add(new Tuple<int, KeyValuePair<string, object>[]>(Convert.ToInt32(telemetryInformation.MaxItemCount), dimensions));

            OpenTelemetryMetrics.RequestUnitsHistogram.Record(telemetryInformation.RequestCharge, dimensions);
            OpenTelemetryMetrics.RequestLatencyHistogram.Record(telemetryInformation.RequestLatency.Value.Milliseconds, dimensions);
            OpenTelemetryMetrics.NumberOfOperationCallCounter.Add(1, dimensions);
        }

        public static IEnumerable<Measurement<int>> GetMaxItemCount()
        {
            foreach (Tuple<int, KeyValuePair<string, object>[]> maxItemCount in maxItemCounts)
            {
                yield return new Measurement<int>(maxItemCount.Item1, maxItemCount.Item2);
            }
        }   

    }
}
