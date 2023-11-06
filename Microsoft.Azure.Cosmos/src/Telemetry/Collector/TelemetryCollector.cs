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

    internal class TelemetryCollector : ITelemetryCollector
    {
        private readonly ClientTelemetry clientTelemetry = null;
        private readonly ConnectionPolicy connectionPolicy = null;

        internal TelemetryCollector(
            ClientTelemetry clientTelemetry,
            ConnectionPolicy connectionPolicy)
        {
            this.clientTelemetry = clientTelemetry;
            this.connectionPolicy = connectionPolicy;
        }

        public void CollectCacheInfo(string cacheName, Func<TelemetryInformation> functionFordata)
        {
            TelemetryInformation data = functionFordata();
            try
            {
                if (data.CollectionLink != null)
                {
                    TelemetryCollector.GetDatabaseAndCollectionName(data.CollectionLink, out string databaseName, out string collectionName);

                    data.DatabaseId = databaseName;
                    data.ContainerId = collectionName;
                }
                this.clientTelemetry?.PushCacheDatapoint(cacheName, data);
            }
            catch (Exception ex)
            {
                data.TraceToLog.AddDatum($"{ClientTelemetryOptions.TelemetryCollectFailedKeyPrefix}-{cacheName}", ex);
                DefaultTrace.TraceError($"Error while collecting cache {0} telemetry. Exception : {1}", cacheName, ex);
            }
        }

        public void CollectOperationAndNetworkInfo(Func<TelemetryInformation> functionFordata)
        {
            TelemetryInformation data = functionFordata();
            try
            {
                this.clientTelemetry?.PushOperationDatapoint(data);
            }
            catch (Exception ex)
            {
                data.TraceToLog.AddDatum($"{ClientTelemetryOptions.TelemetryCollectFailedKeyPrefix}-Operation", ex);
                DefaultTrace.TraceError($"Error while collecting operation telemetry. Exception : {1}", ex);
            }

            // Collect network level telemetry only in Direct Mode
            if (this.connectionPolicy.ConnectionMode == ConnectionMode.Direct)
            {
                try
                {
                    SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(data.Trace);
                    this.clientTelemetry?.PushNetworkDataPoint(summaryDiagnostics.StoreResponseStatistics.Value, data.DatabaseId, data.ContainerId);
                }
                catch (Exception ex)
                {
                    data.TraceToLog.AddDatum($"{ClientTelemetryOptions.TelemetryCollectFailedKeyPrefix}-Network", ex);
                    DefaultTrace.TraceError($"Error while collecting network telemetry. Exception : {1}", ex);
                }
            }
            
        }

        private static void GetDatabaseAndCollectionName(string path, out string databaseName, out string collectionName)
        {
            string[] segments = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            PathsHelper.ParseDatabaseNameAndCollectionNameFromUrlSegments(segments, out databaseName, out collectionName);
        }
    }
}