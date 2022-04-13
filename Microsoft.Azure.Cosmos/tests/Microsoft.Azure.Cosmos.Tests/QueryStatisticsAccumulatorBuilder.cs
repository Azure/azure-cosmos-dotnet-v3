using System.Collections.Generic;

internal class QueryStatisticsAccumulatorBuilder
{
    public QueryMetrics queryMetrics = new();
    public List<QueryMetrics> QueryMetricsList { get; set; } = new List<QueryMetrics>();
}

internal class QueryMetrics
{
    public long RetrievedDocumentCount { get; set; }
    public long RetrievedDocumentSize { get; set; }
    public long OutputDocumentCount { get; set; }
    public long OutputDocumentSize { get; set; }
    public double TotalQueryExecutionTime { get; set; }
    public double DocumentLoadTime { get; set; }
    public double DocumentWriteTime { get; set; }
    public double Created { get; set; }
    public double ChannelAcquisitionStarted { get; set; }
    public double Pipelined { get; set; }
    public double TransitTime { get; set; }
    public double Received { get; set; }
    public double Completed { get; set; }
    public double BadRequestCreated { get; set; }
    public double BadRequestChannelAcquisitionStarted { get; set; }
    public double BadRequestPipelined { get; set; }
    public double BadRequestTransitTime { get; set; }
    public double BadRequestReceived { get; set; }
    public double BadRequestCompleted { get; set; }
}
