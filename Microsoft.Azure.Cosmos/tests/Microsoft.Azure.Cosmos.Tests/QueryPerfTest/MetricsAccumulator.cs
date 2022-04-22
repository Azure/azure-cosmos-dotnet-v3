namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class MetricsAccumulator
    {
        private const string BackendKeyValue = "Query Metrics";
        private const string TransportKeyValue = "Client Side Request Stats";
        private const string ClientParseTimeNode = "POCO Materialization";
        private const string ClientDeserializationTimeNode = "Get Cosmos Element Response";

        public void ReadFromTrace<T>(FeedResponse<T> Response, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor)
        {
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            List<ITrace> getCosmosElementResponse = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientDeserializationTimeNode, hasKey: false);
            List<ITrace> poco = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: ClientParseTimeNode, hasKey: false);
            foreach (ITrace p in poco)
            {
                queryStatisticsDatumVisitor.AddPocoTime(p.Duration.TotalMilliseconds);
            }

            foreach (ITrace getCosmos in getCosmosElementResponse)
            {
                queryStatisticsDatumVisitor.AddGetCosmosElementResponseTime(getCosmos.Duration.TotalMilliseconds);
            }

            List<ITrace> transitMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: TransportKeyValue, hasKey: true);
            List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: BackendKeyValue, hasKey: true);
            foreach (ITrace node in backendMetrics.Concat(transitMetrics))
            {
                foreach (KeyValuePair<string, object> kvp in node.Data)
                {
                    switch (kvp.Value)
                    {
                        case TraceDatum traceDatum:
                            traceDatum.Accept(queryStatisticsDatumVisitor);
                            break;
                        default:
                            Console.WriteLine($"Unexpected type {kvp.Value.GetType()}");
                            break;
                    }
                }
            }
        }

        private List<ITrace> FindQueryMetrics(ITrace trace, string nodeNameOrKeyName, bool hasKey)
        {
            List<ITrace> queryMetricsNodes = new();
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if ((hasKey && node.Data.ContainsKey(nodeNameOrKeyName)) ||
                     node.Name == nodeNameOrKeyName)
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
