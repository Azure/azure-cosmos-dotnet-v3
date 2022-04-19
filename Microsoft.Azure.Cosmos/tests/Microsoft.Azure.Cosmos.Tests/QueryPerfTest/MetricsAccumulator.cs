using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Diagnostics;
using Microsoft.Azure.Cosmos.Tests;
using Microsoft.Azure.Cosmos.Tracing;
using Microsoft.Azure.Cosmos.Tracing.TraceData;

internal class MetricsAccumulator
{
    public void GetTrace<T>(FeedResponse<T> Response, QueryStatisticsAccumulator queryStatisticsAccumulator)
    {
        string backendKeyValue = "Query Metrics";
        ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
        List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: backendKeyValue, hasKey: true);
        if (backendMetrics != null)
        {
            foreach (ITrace node in backendMetrics)
            {
                foreach (KeyValuePair<string, object> kvp in node.Data)
                {
                    queryStatisticsAccumulator.Visit((QueryMetricsTraceDatum)kvp.Value);
                }
            }
        }
        string transportKeyValue = "Client Side Request Stats";
        List<ITrace> transitMetrics = this.FindQueryMetrics(trace, nodeNameOrKeyName: transportKeyValue, hasKey: true);
        if (transitMetrics != null)
        {
            foreach (ITrace node in transitMetrics)
            {
                foreach (KeyValuePair<string, object> kvp in node.Data)
                {
                    queryStatisticsAccumulator.Visit((ClientSideRequestStatisticsTraceDatum)kvp.Value);
                }
            }
        }
        string clientParseTimeNode = "POCO Materialization";
        List<ITrace> poco = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: clientParseTimeNode, hasKey: false);
        if (poco != null)
        {
            foreach (ITrace p in poco)
            {
                queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.queryMetrics.PocoTimeList = p.Duration.TotalMilliseconds;
            }
        }
        string clientDeserializationTimeNode = "Get Cosmos Element Response";
        List<ITrace> getCosmosElementResponse = this.FindQueryMetrics(trace: trace, nodeNameOrKeyName: clientDeserializationTimeNode, hasKey: false);
        if (getCosmosElementResponse != null)
        {
            foreach (ITrace getCosmos in getCosmosElementResponse)
            {
                queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.queryMetrics.GetCosmosElementResponseTimeList = getCosmos.Duration.TotalMilliseconds;
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
