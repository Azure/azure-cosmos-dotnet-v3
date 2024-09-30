//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The OpenTelemetryMetricsCollector class is responsible for collecting and recording Cosmos DB operational metrics, such as item counts, request latency, request units, and regions contacted. 
    /// This data is captured using the OpenTelemetry metrics API, which allows tracking and analysis of Cosmos DB operations at a granular level.
    /// </summary>
    internal class OpenTelemetryMetricsCollector : ITelemetryCollector
    {
        private readonly string clientId;
        private readonly string accountName;

        /// <summary>
        /// Initializes a new instance of the OpenTelemetryMetricsCollector class.
        /// </summary>
        /// <param name="clientId">A unique identifier for the Cosmos DB client instance</param>
        /// <param name="accountName">The Cosmos DB account name.</param>
        public OpenTelemetryMetricsCollector(string clientId, string accountName)
        {
            this.clientId = clientId;
            this.accountName = accountName;

            CosmosOperationMeter.Initialize();
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

            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new[]
                {
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.AccountName, this.accountName),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, telemetryInformation.ContainerId),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, telemetryInformation.DatabaseId),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.OperationType, telemetryInformation.OperationType),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, (int)telemetryInformation.StatusCode),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, (int)telemetryInformation.SubStatusCode),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ClientId, this.clientId),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.Consistency, telemetryInformation.ConsistencyLevel),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.PartitionKeyRangeId, telemetryInformation.PartitionKeyRangeId),
                };

            CosmosOperationMeter.RecordMaxItemCount(Convert.ToInt32(telemetryInformation.MaxItemCount), dimensionsFunc);
            CosmosOperationMeter.RecordActualItemCount(Convert.ToInt32(telemetryInformation.ActualItemCount), dimensionsFunc);
            CosmosOperationMeter.RecordRegionContactedCount(telemetryInformation.RegionsContactedList.Count, dimensionsFunc);
            CosmosOperationMeter.RecordRequestUnit(telemetryInformation.RequestCharge, dimensionsFunc);
            CosmosOperationMeter.RecordRequestLatency(telemetryInformation.RequestLatency, dimensionsFunc);
            CosmosOperationMeter.RecordOperationCallCount(dimensionsFunc);
        }
    }
}
