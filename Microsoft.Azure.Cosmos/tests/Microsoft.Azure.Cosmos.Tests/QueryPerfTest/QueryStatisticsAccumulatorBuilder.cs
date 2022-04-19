using System.Collections.Generic;

internal class QueryStatisticsAccumulatorBuilder
{
    public QueryMetrics queryMetrics = new();
    public List<QueryMetrics> QueryMetricsList { get; set; } = new List<QueryMetrics>();
}
