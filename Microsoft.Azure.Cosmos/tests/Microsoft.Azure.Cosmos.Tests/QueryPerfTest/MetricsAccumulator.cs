namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class MetricsAccumulator
    {
        public void GetTrace<T>(FeedResponse<T> Response, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor)
        {
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            string backendKeyValue = "Query Metrics";
            string transportKeyValue = "Client Side Request Stats";
            List<ITrace> transitMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: transportKeyValue, hasKey: true);
            List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: backendKeyValue, hasKey: true);
            foreach (ITrace node in backendMetrics.Concat(transitMetrics))
            {
                foreach (KeyValuePair<string, object> kvp in node.Data)
                {
                    switch (kvp.Value)
                    {
                        case TraceDatum:
                            ((TraceDatum)kvp.Value).Accept(queryStatisticsDatumVisitor);
                            break;
                        default:
                            Console.WriteLine("Unexpected trace type");
                            break;
                    }
                }
            }

            string clientParseTimeNode = "POCO Materialization";
            string clientDeserializationTimeNode = "Get Cosmos Element Response";
            List<ITrace> getCosmosElementResponse = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: clientDeserializationTimeNode, hasKey: false);
            List<ITrace> poco = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: clientParseTimeNode, hasKey: false);
            foreach (ITrace p in poco)
            {
                queryStatisticsDatumVisitor.queryMetrics.PocoTime = p.Duration.TotalMilliseconds;
            }

            foreach (ITrace getCosmos in getCosmosElementResponse)
            {
                queryStatisticsDatumVisitor.queryMetrics.GetCosmosElementResponseTime = getCosmos.Duration.TotalMilliseconds;
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
                if (hasKey == true && node.Data.ContainsKey(nodeNameOrKeyName))
                {
                    queryMetricsNodes.Add(node);
                }

                else if (node.Name == nodeNameOrKeyName)
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
