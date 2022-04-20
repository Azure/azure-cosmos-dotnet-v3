using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal class MetricsSerializer
{
    private readonly Func<QueryMetrics, double>[] metricsDelegate = new Func<QueryMetrics, double>[] { metric => metric.RetrievedDocumentCount,
            metric => metric.RetrievedDocumentSize, metric => metric.OutputDocumentCount, metric => metric.OutputDocumentSize, metric => metric.TotalQueryExecutionTime,
            metric => metric.DocumentLoadTime, metric => metric.DocumentWriteTime, metric => metric.Created, metric => metric.ChannelAcquisitionStarted, metric => metric.Pipelined,
            metric => metric.TransitTime, metric => metric.Received, metric => metric.Completed,metric => metric.PocoTime, metric => metric.GetCosmosElementResponseTime,
            metric => metric.EndToEndTime};

    private List<double> CalculateAverage(List<QueryMetrics> getAverageList)
    {
        List<double> avgList = new();
        foreach (Func<QueryMetrics, double> metric in this.metricsDelegate)
        {
            avgList.Add(getAverageList.Average(metric));
        }

        return avgList;
    }

    private List<double> CalculateMedian(List<QueryMetrics> getMedianList)
    {
        List<double> medianList = new();
        foreach (Func<QueryMetrics, double> metric in this.metricsDelegate)
        {
            medianList.Add(this.GetMedian(getMedianList, getMedianList.ConvertAll(new Converter<QueryMetrics, double>(metric))));
        }

        return medianList;
    }

    public double GetMedian(List<QueryMetrics> getMedianList, List<double> sortedNumbers)
    {
        List<double> metricList = sortedNumbers;
        int mid = getMedianList.Count() / 2;
        metricList.Sort();
        double median = (getMedianList.Count() % 2) == 0
            ? (metricList.ElementAt(mid) + metricList.ElementAt(mid - 1)) / 2
            : metricList.ElementAt(mid);
        return median;
    }

    private List<QueryMetrics> SumOfRoundTrips(int roundTrips, List<QueryMetrics> originalList)
    {
        List<QueryMetrics> sumOfRoundTripsList = new();
        int i = 0;
        int j = roundTrips;
        while (i < originalList.Count - 1)
        {
            List<QueryMetrics> metricsPerRoundTrip = new();
            metricsPerRoundTrip = originalList.GetRange(i, j);
            sumOfRoundTripsList.
                Add(new QueryMetrics
                {
                    RetrievedDocumentCount = metricsPerRoundTrip.Sum(metric => metric.RetrievedDocumentCount),
                    RetrievedDocumentSize = metricsPerRoundTrip.Sum(metric => metric.RetrievedDocumentSize),
                    OutputDocumentCount = metricsPerRoundTrip.Sum(metric => metric.OutputDocumentCount),
                    OutputDocumentSize = metricsPerRoundTrip.Sum(metric => metric.OutputDocumentSize),
                    TotalQueryExecutionTime = metricsPerRoundTrip.Sum(metric => metric.TotalQueryExecutionTime),
                    DocumentLoadTime = metricsPerRoundTrip.Sum(metric => metric.DocumentLoadTime),
                    DocumentWriteTime = metricsPerRoundTrip.Sum(metric => metric.DocumentWriteTime),
                    Created = metricsPerRoundTrip.Sum(metric => metric.Created),
                    ChannelAcquisitionStarted = metricsPerRoundTrip.Sum(metric => metric.ChannelAcquisitionStarted),
                    Pipelined = metricsPerRoundTrip.Sum(metric => metric.Pipelined),
                    TransitTime = metricsPerRoundTrip.Sum(metric => metric.TransitTime),
                    Received = metricsPerRoundTrip.Sum(metric => metric.Received),
                    Completed = metricsPerRoundTrip.Sum(metric => metric.Completed),
                    PocoTime = metricsPerRoundTrip.Sum(metric => metric.PocoTime),
                    GetCosmosElementResponseTime = metricsPerRoundTrip.Sum(metric => metric.GetCosmosElementResponseTime),
                    EndToEndTime = metricsPerRoundTrip.Sum(metric => metric.EndToEndTime)
                });

            i += roundTrips;
        }

        return sumOfRoundTripsList;
    }

    private List<T> EliminateWarmupIterations<T>(List<T> noWarmupList, int roundTrips, int warmupIterations)
    {
        int iterationsToEliminate = warmupIterations * roundTrips;
        noWarmupList = noWarmupList.GetRange(iterationsToEliminate, noWarmupList.Count - iterationsToEliminate);
        return noWarmupList;
    }

    public void SerializeAsync(TextWriter textWriter, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor, int numberOfIterations, int warmupIterations, bool rawData)
    {
        int roundTrips = queryStatisticsDatumVisitor.queryStatisticsAccumulator.QueryMetricsList.Count / numberOfIterations;
        if (rawData == false)
        {
            textWriter.WriteLine(roundTrips);
            List<QueryMetrics> noWarmupList = this.EliminateWarmupIterations(
                            queryStatisticsDatumVisitor.queryStatisticsAccumulator.QueryMetricsList, roundTrips, warmupIterations);

            if (roundTrips > 1)
            {
                noWarmupList = this.SumOfRoundTrips(roundTrips, noWarmupList);
            }

            this.CalculateAverage(noWarmupList).ForEach(textWriter.WriteLine);
            textWriter.WriteLine();
            textWriter.WriteLine("Median");
            textWriter.WriteLine();
            this.CalculateMedian(noWarmupList).ForEach(textWriter.WriteLine);
            textWriter.Flush();
            textWriter.Close();
        }

        else
        {
            textWriter.WriteLine();
            textWriter.WriteLine("QueryMetrics");
            foreach (QueryMetrics metrics in queryStatisticsDatumVisitor.queryStatisticsAccumulator.QueryMetricsList)
            {
                textWriter.WriteMetrics(metrics.RetrievedDocumentCount, metrics.RetrievedDocumentSize, metrics.OutputDocumentCount,
                    metrics.OutputDocumentSize, metrics.TotalQueryExecutionTime, metrics.DocumentLoadTime, metrics.DocumentWriteTime,
                    metrics.Created, metrics.ChannelAcquisitionStarted, metrics.Pipelined, metrics.TransitTime, metrics.Received,
                    metrics.Completed, metrics.PocoTime, metrics.GetCosmosElementResponseTime, metrics.EndToEndTime);
                textWriter.WriteLine();
            }

            textWriter.WriteLine();
            textWriter.WriteLine("BadRequest");
            foreach (QueryMetrics metrics in queryStatisticsDatumVisitor.queryStatisticsAccumulator.QueryMetricsList)
            {
                textWriter.WriteMetrics(metrics.BadRequestCreated, metrics.BadRequestChannelAcquisitionStarted, metrics.BadRequestPipelined,
                    metrics.BadRequestTransitTime, metrics.BadRequestReceived, metrics.BadRequestCompleted);
                textWriter.WriteLine();
            }

            textWriter.Flush();
            textWriter.Close();
        }
    }
}
