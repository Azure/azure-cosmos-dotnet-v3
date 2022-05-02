namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

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
            List<ITrace> retrieveCosmosElementTraces = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientDeserializationTimeNode, isKeyName: false);
            List<ITrace> retrieveQueryMetricTraces = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientParseTimeNode, isKeyName: false);
            foreach (ITrace queryMetricTrace in retrieveQueryMetricTraces)
            {
                queryStatisticsDatumVisitor.AddPocoTime(queryMetricTrace.Duration.TotalMilliseconds);
            }

            List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: BackendKeyValue, isKeyName: true);
            List<ITrace> transitMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: TransportKeyValue, isKeyName: true, currentNodeName: TransportNodeName);
            List<Tuple<ITrace, ITrace, ITrace>> backendAndClientMetrics = new();
            int i = 0;
            int j = 0;
            int k = 0;
            while (i < transitMetrics.Count && j < backendMetrics.Count && k < retrieveCosmosElementTraces.Count)
            {
                foreach (ITrace node in transitMetrics)
                {
                    foreach (KeyValuePair<string, object> kvp in node.Data)
                    {
                        ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = (ClientSideRequestStatisticsTraceDatum)kvp.Value;
                        if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 1)
                        {
                            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponse in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                            {
                                if (storeResponse.StoreResult.StatusCode == StatusCodes.Ok)
                                {
                                    backendAndClientMetrics.Add(Tuple.Create(retrieveCosmosElementTraces[k], backendMetrics[j], transitMetrics[i]));
                                    j++;
                                    k++;
                                }
                                else if (storeResponse.StoreResult.StatusCode != StatusCodes.Ok)
                                {
                                    backendAndClientMetrics.Add(Tuple.Create<ITrace, ITrace, ITrace>(null, null, transitMetrics[i]));
                                }
                            }

                            i++;
                        }
                        else
                        {
                            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponse in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                            {
                                if (storeResponse.StoreResult.StatusCode == StatusCodes.Ok)
                                {
                                    backendAndClientMetrics.Add(Tuple.Create(retrieveCosmosElementTraces[k], backendMetrics[j], transitMetrics[i]));
                                    i++;
                                    j++;
                                    k++;
                                }
                                else if (storeResponse.StoreResult.StatusCode != StatusCodes.Ok)
                                {
                                    backendAndClientMetrics.Add(Tuple.Create<ITrace, ITrace, ITrace>(null, null, transitMetrics[i]));
                                    i++;
                                }
                            }

                        }
                    }
                }
            }

            foreach (Tuple<ITrace, ITrace, ITrace> metrics in backendAndClientMetrics)
            {
                if (metrics.Item2 != null)
                {
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
                if (currentNodeName != null)
                {
                    if ((isKeyName && node.Data.ContainsKey(nodeNameOrKeyName) && node.Name == currentNodeName) ||
                        (node.Name == nodeNameOrKeyName))
                    {
                        queryMetricsNodes.Add(node);
                    }
                    foreach (ITrace child in node.Children)
                    {
                        queue.Enqueue(child);
                    }
                }
                else
                {
                    if ((isKeyName && node.Data.ContainsKey(nodeNameOrKeyName)) ||
                                                (node.Name == nodeNameOrKeyName))
                    {
                        queryMetricsNodes.Add(node);
                    }
                    foreach (ITrace child in node.Children)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return queryMetricsNodes;
        }
    }
}
