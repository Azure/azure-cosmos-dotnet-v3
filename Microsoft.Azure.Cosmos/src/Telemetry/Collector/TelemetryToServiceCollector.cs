//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    internal class TelemetryToServiceCollector : IClientTelemetryCollectors
    {
        private readonly ClientTelemetryJob clientTelemetry = null;
        private readonly ConnectionPolicy connectionPolicy = null;

        internal TelemetryToServiceCollector(
            ClientTelemetryJob clientTelemetry,
            ConnectionPolicy connectionPolicy)
        {
            this.clientTelemetry = clientTelemetry;
            this.connectionPolicy = connectionPolicy;
        }

        public void CollectCacheInfo(string cacheName, Func<TelemetryInformation> functionFordata)
        {
            try
            {
                TelemetryInformation data = functionFordata();

                if (data.collectionLink != null)
                {
                    GetDatabaseAndCollectionName(data.collectionLink, out string databaseName, out string collectionName);

                    data.databaseId = databaseName;
                    data.containerId = collectionName;
                }

                this.clientTelemetry?.PushCacheInfo(cacheName, data);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Error while collecting cache {cacheName} telemetry. Exception : {ex}");
            }
        }

        public void CollectOperationAndNetworkInfo(Func<TelemetryInformation> functionFordata)
        {
            try
            {
                TelemetryInformation data = functionFordata();

                this.clientTelemetry?.PushOperationInfo(data);

                // Collect network level telemetry only in Direct Mode
                if (this.connectionPolicy.ConnectionMode == ConnectionMode.Direct)
                {
                    SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(data.trace);
                    this.clientTelemetry?.PushNetworkInfo(summaryDiagnostics.StoreResponseStatistics.Value, data.databaseId, data.containerId);
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Error while collecting operation telemetry. Exception : {ex}");
            }
        }

        private static void GetDatabaseAndCollectionName(string path, out string databaseName, out string collectionName)
        {
            string[] segments = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            PathsHelper.ParseDatabaseNameAndCollectionNameFromUrlSegments(segments, out databaseName, out collectionName);
        }
    }
}
