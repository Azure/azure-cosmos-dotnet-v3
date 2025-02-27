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
        private static readonly char[] pathSeparators = new char[] { '/' };

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
            try
            {
                TelemetryInformation data = functionFordata();

                if (data.CollectionLink != null)
                {
                    GetDatabaseAndCollectionName(data.CollectionLink, out string databaseName, out string collectionName);

                    data.DatabaseId = databaseName;
                    data.ContainerId = collectionName;
                }

                this.clientTelemetry?.PushCacheDatapoint(cacheName, data);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Error while collecting cache {0} telemetry. Exception : {1}", cacheName, ex);
            }
        }

        public void CollectOperationAndNetworkInfo(Func<TelemetryInformation> functionFordata)
        {
            try
            {
                TelemetryInformation data = functionFordata();

                this.clientTelemetry?.PushOperationDatapoint(data);

                // Collect network level telemetry only in Direct Mode
                if (this.connectionPolicy.ConnectionMode == ConnectionMode.Direct)
                {
                    SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(data.Trace);
                    this.clientTelemetry?.PushNetworkDataPoint(summaryDiagnostics.StoreResponseStatistics.Value, data.DatabaseId, data.ContainerId);
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Error while collecting operation telemetry. Exception : {0}", ex);
            }
        }

        private static void GetDatabaseAndCollectionName(string path, out string databaseName, out string collectionName)
        {
            string[] segments = path.Split(pathSeparators, StringSplitOptions.RemoveEmptyEntries);

            PathsHelper.ParseDatabaseNameAndCollectionNameFromUrlSegments(segments, out databaseName, out collectionName);
        }
    }
}