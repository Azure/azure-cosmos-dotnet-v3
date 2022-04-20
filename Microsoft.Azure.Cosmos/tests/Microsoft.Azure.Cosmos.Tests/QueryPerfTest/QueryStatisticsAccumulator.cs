namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;

    internal class QueryStatisticsAccumulator
    {
        public List<QueryMetrics> QueryMetricsList { get; } = new();
    }
}