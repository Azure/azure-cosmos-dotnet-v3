namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class MetricsAccumulator
    {
        public void ReadFromTrace<T>(FeedResponse<T> response, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor)
        {
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;

            // POCO materialization occurs once per item each roundtrip for calls with status code 200
            List<ITrace> retrieveQueryMetricTraces = this.ExtractTraces(trace: trace, nodeOrKeyName: TraceDatumKeys.QueryResponseSerialization, isKeyName: false);
            foreach (ITrace queryMetricTrace in retrieveQueryMetricTraces)
            {
                queryStatisticsDatumVisitor.AddPocoTime(queryMetricTrace.Duration.TotalMilliseconds);
            }

            // Get cosmos element response occurs once per roundtrip for calls with status code 200
            List<ITrace> getCosmosElementTraces = this.ExtractTraces(trace: trace, nodeOrKeyName: TraceDatumKeys.GetCosmosElementResponse, isKeyName: false);

            // Query combinedMetrics occurs once per roundtrip for calls with status code 200
            List<ITrace> queryMetricsTraces = this.ExtractTraces(trace: trace, nodeOrKeyName: TraceDatumKeys.QueryMetrics, isKeyName: true);

            // Clientside request stats occur once per roundtrip for all status codes
            List<ITrace> clientSideRequestStatsTraces = this.ExtractTraces(
                trace: trace, 
                nodeOrKeyName: TraceDatumKeys.ClientSideRequestStats,
                isKeyName: true,
                currentNodeName: TraceDatumKeys.TransportRequest);

            List<QueryCombinedMetricsTraces> combinedMetricsList = new();
            int getCosmosElementTraceCount = 0;
            int queryMetricsTraceCount = 0;
            foreach (ITrace clientSideRequestStatsTrace in clientSideRequestStatsTraces)
            {
                Debug.Assert(clientSideRequestStatsTrace.Data.Count == 1, "Expected 1 Client Side Request Stats Traces Object");

                KeyValuePair<string, object> clientSideMetrics = clientSideRequestStatsTrace.Data.Single();
                Assert.IsInstanceOfType(clientSideMetrics.Value, typeof(ClientSideRequestStatisticsTraceDatum));
                ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = (ClientSideRequestStatisticsTraceDatum)clientSideMetrics.Value;

                foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStats in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                {
                    if (storeResponseStats.StoreResult.StatusCode == StatusCodes.Ok)
                    {
                        combinedMetricsList.Add(new QueryCombinedMetricsTraces(getCosmosElementTraces[getCosmosElementTraceCount], queryMetricsTraces[queryMetricsTraceCount], clientSideRequestStatsTrace));
                        getCosmosElementTraceCount++;
                        queryMetricsTraceCount++;
                    }
                    else
                    {
                        // Failed requests will only have Client Side Request Stats
                        combinedMetricsList.Add(new QueryCombinedMetricsTraces(null, null, clientSideRequestStatsTrace));
                    }
                }
            }

            int traceCount = 0;
            foreach (QueryCombinedMetricsTraces combinedMetrics in combinedMetricsList)
            {
                if (combinedMetrics.GetCosmosElementTrace != null)
                {
                    queryStatisticsDatumVisitor.AddGetCosmosElementResponseTime(combinedMetrics.GetCosmosElementTrace.Duration.TotalMilliseconds);
                    queryStatisticsDatumVisitor.AddRequestCharge(response.RequestCharge);

                    foreach (KeyValuePair<string, object> datum in combinedMetrics.QueryMetricsTrace.Data)
                    {
                        switch (datum.Value)
                        {
                            case TraceDatum traceDatum:
                                traceDatum.Accept(queryStatisticsDatumVisitor);
                                break;
                            default:
                                Debug.Fail("Unexpected type", $"Type not supported {datum.Value.GetType()}");
                                break;
                        }
                    }

                    // Add combinedMetrics to the list except for last roundtrip which is taken care of in ContentSerializationPerformanceTest class
                    if (traceCount < queryMetricsTraces.Count - 1)
                    {
                        queryStatisticsDatumVisitor.PopulateMetrics();
                    }
                }

                foreach (KeyValuePair<string, object> datum in combinedMetrics.ClientSideRequestStatsTrace.Data)
                {
                    switch (datum.Value)
                    {
                        case TraceDatum traceDatum:
                            traceDatum.Accept(queryStatisticsDatumVisitor);
                            break;
                        default:
                            Debug.Fail("Unexpected type", $"Type not supported {datum.Value.GetType()}");
                            break;
                    }
                }

                traceCount++;
            }
        }

        private List<ITrace> ExtractTraces(ITrace trace, string nodeOrKeyName, bool isKeyName, string currentNodeName = null)
        {
            List<ITrace> traceList = new();
            Queue<ITrace> traceQueue = new Queue<ITrace>();
            traceQueue.Enqueue(trace);

            while (traceQueue.Count > 0)
            {
                ITrace traceObject = traceQueue.Dequeue();
                if ((isKeyName && traceObject.Data.ContainsKey(nodeOrKeyName) && (currentNodeName == null || traceObject.Name == currentNodeName)) ||
                    (traceObject.Name == nodeOrKeyName))
                {
                    traceList.Add(traceObject);
                }

                foreach (ITrace childTraceObject in traceObject.Children)
                {
                    traceQueue.Enqueue(childTraceObject);
                }
            }

            return traceList;
        }

        public readonly struct QueryCombinedMetricsTraces
        {
            public QueryCombinedMetricsTraces(ITrace getCosmosElementTrace, ITrace queryMetricsTrace, ITrace clientSideRequestStatsTrace)
            {
                Debug.Assert((getCosmosElementTrace == null) == (queryMetricsTrace == null));
                Debug.Assert(clientSideRequestStatsTrace != null, "Client Side Request Stats cannot be null");

                this.GetCosmosElementTrace = getCosmosElementTrace;
                this.QueryMetricsTrace = queryMetricsTrace;
                this.ClientSideRequestStatsTrace = clientSideRequestStatsTrace;
            }

            public ITrace GetCosmosElementTrace { get; }
            public ITrace QueryMetricsTrace { get; }
            public ITrace ClientSideRequestStatsTrace { get; }
        }
    }
}
