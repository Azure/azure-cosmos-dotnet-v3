namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
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
        private const string BackendKeyValue = "Query Metrics";
        private const string TransportKeyValue = "Client Side Request Stats";
        private const string ClientParseTimeNode = "POCO Materialization";
        private const string ClientDeserializationTimeNode = "Get Cosmos Element Response";
        private const string TransportNodeName = "Microsoft.Azure.Documents.ServerStoreModel Transport Request";


        public void ReadFromTrace<T>(FeedResponse<T> Response, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor)
        {
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;

            //POCO Materialization occurs once per iteration including all the roundtrips
            List<ITrace> retrieveQueryMetricTraces = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientParseTimeNode, isKeyName: false);
            foreach (ITrace queryMetricTrace in retrieveQueryMetricTraces)
            {
                queryStatisticsDatumVisitor.AddPocoTime(queryMetricTrace.Duration.TotalMilliseconds);
            }

            //Get Cosmos Element Response occurs once per roundtrip for calls with status code 200
            List<ITrace> retrieveCosmosElementTraces = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientDeserializationTimeNode, isKeyName: false);

            //Query metrics occurs once per roundtrip for calls with status code 200
            List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: BackendKeyValue, isKeyName: true);

            //Client metrics occurs once per roundtrip for all status codes
            List<ITrace> transitMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: TransportKeyValue, isKeyName: true, currentNodeName: TransportNodeName);
            List<Tuple<ITrace, ITrace, ITrace>> backendAndClientMetrics = new();
            int i = 0;
            int j = 0;
            int k = 0;
            foreach (ITrace node in transitMetrics)
            {
                Debug.Assert(node.Data.Count == 1, "Exactly one transit metric expected");
                KeyValuePair<string, object> kvp = node.Data.Single();
                Assert.IsInstanceOfType(kvp.Value, typeof(ClientSideRequestStatisticsTraceDatum));
                ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = (ClientSideRequestStatisticsTraceDatum)kvp.Value;
                foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponse in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                {
                    if (storeResponse.StoreResult.StatusCode == StatusCodes.Ok)
                    {
                        backendAndClientMetrics.Add(Tuple.Create(retrieveCosmosElementTraces[k], backendMetrics[j], transitMetrics[i]));
                        j++;
                        k++;
                    }
                    else
                    {
                        //We add null values to the tuple since status codes other than Ok will not have data for 'Query Metrics' and 'Get Cosmos Element Response'
                        backendAndClientMetrics.Add(Tuple.Create<ITrace, ITrace, ITrace>(null, null, transitMetrics[i]));
                    }
                }

                i++;
            }

            Debug.Assert(i == transitMetrics.Count, "All 'transit metrics' must be grouped.");
            Debug.Assert(j == backendMetrics.Count, "All 'backend metrics' must be grouped.");
            Debug.Assert(k == retrieveCosmosElementTraces.Count, "All 'Get Cosmos Element Response' traces must be grouped.");

            int l = 1;
            foreach (Tuple<ITrace, ITrace, ITrace> metrics in backendAndClientMetrics)
            {
                if (metrics.Item2 != null)
                {
                    Debug.Assert(metrics.Item1 == null, "'Get Cosmos Element Response' is null");
                    queryStatisticsDatumVisitor.AddGetCosmosElementResponseTime(metrics.Item1.Duration.TotalMilliseconds);
                    foreach (KeyValuePair<string, object> kvp in metrics.Item2.Data)
                    {
                        switch (kvp.Value)
                        {
                            case TraceDatum traceDatum:
                                traceDatum.Accept(queryStatisticsDatumVisitor);
                                break;
                            default:
                                Debug.Fail("Unexpected type", $"Type not supported {metrics.Item2.GetType()}");
                                break;
                        }
                    }

                    //add metrics to the list except for last roundtrip which is taken care of in ContentSerializationPerformanceTest class
                    if (l != backendMetrics.Count)
                    {
                        queryStatisticsDatumVisitor.PopulateMetrics();
                    }
                    l++;
                }

                foreach (KeyValuePair<string, object> kvp in metrics.Item3.Data)
                {
                    switch (kvp.Value)
                    {
                        case TraceDatum traceDatum:
                            traceDatum.Accept(queryStatisticsDatumVisitor);
                            break;
                        default:
                            Debug.Fail("Unexpected type", $"Type not supported {metrics.Item3.GetType()}");
                            break;
                    }
                }
            }
        }

        private List<ITrace> FindQueryMetrics(ITrace trace, string nodeNameOrKeyName, bool isKeyName, string currentNodeName = null)
        {
            List<ITrace> queryMetricsNodes = new();
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if ((isKeyName && node.Data.ContainsKey(nodeNameOrKeyName) && (currentNodeName == null || node.Name == currentNodeName)) ||
                    (node.Name == nodeNameOrKeyName))
                {
                    queryMetricsNodes.Add(node);
                }
                foreach (ITrace child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }

            return queryMetricsNodes;
        }
    }
}
