namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class MetricsSerializer
    {
        private const string PrintMedian = "Median";
        private const string PrintQueryMetrics = "QueryMetrics";
        private const string PrintBadRequests = "BadRequest";
        private readonly Func<QueryMetrics, double>[] metricsDelegate = new Func<QueryMetrics, double>[]
        {
            metric => metric.RetrievedDocumentCount,
            metric => metric.RetrievedDocumentSize,
            metric => metric.OutputDocumentCount,
            metric => metric.OutputDocumentSize,
            metric => metric.TotalQueryExecutionTime,
            metric => metric.DocumentLoadTime,
            metric => metric.DocumentWriteTime,
            metric => metric.Created,
            metric => metric.ChannelAcquisitionStarted,
            metric => metric.Pipelined,
            metric => metric.TransitTime,
            metric => metric.Received,
            metric => metric.Completed,
            metric => metric.PocoTime,
            metric => metric.GetCosmosElementResponseTime,
            metric => metric.EndToEndTime
        };

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

        private List<QueryMetrics> SumOfRoundTrips(int roundTrips, IReadOnlyList<QueryMetrics> originalList)
        {
            List<QueryMetrics> sumOfRoundTripsList = new();
            int i = 0;
            int j = roundTrips;
            while (j <= originalList.Count)
            {
                Range range = i..j;
                List<QueryMetrics> metricsPerRoundTrip = originalList.Take(range).ToList();
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
                j += roundTrips;
            }

            return sumOfRoundTripsList;
        }

        private List<QueryMetrics> SumOfRoundTripsWithoutOutputDoc(int roundTrips, IReadOnlyList<QueryMetrics> originalList)
        {
            List<QueryMetrics> sumOfRoundTripsList = new();
            int i = 0;
            int j = roundTrips;
            while (j <= originalList.Count)
            {
                Range range = i..j;
                List<QueryMetrics> metricsPerRoundTrip = originalList.Take(range).ToList();
                sumOfRoundTripsList.
                    Add(new QueryMetrics
                    {
                        RetrievedDocumentCount = metricsPerRoundTrip.Sum(metric => metric.RetrievedDocumentCount),
                        RetrievedDocumentSize = metricsPerRoundTrip.Sum(metric => metric.RetrievedDocumentSize),
                        OutputDocumentCount = metricsPerRoundTrip.Sum(metric => metric.OutputDocumentCount) / roundTrips,
                        OutputDocumentSize = metricsPerRoundTrip.Sum(metric => metric.OutputDocumentSize) / roundTrips,
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
                j += roundTrips;
            }

            return sumOfRoundTripsList;
        }

        private List<QueryMetrics> EliminateWarmupIterations(IReadOnlyList<QueryMetrics> noWarmupList, int roundTrips, int warmupIterations)
        {
            int iterationsToEliminate = warmupIterations * roundTrips;
            noWarmupList = noWarmupList.Skip(iterationsToEliminate).ToList();
            return noWarmupList.ToList();
        }

        public void Serialize(TextWriter textWriter, QueryStatisticsDatumVisitor queryStatisticsDatumVisitor, int numberOfIterations, int warmupIterations, bool rawData)
        {
            int roundTrips = queryStatisticsDatumVisitor.QueryMetricsList.Count / numberOfIterations;
            if (rawData == false)
            {
                textWriter.WriteLine(roundTrips);
                List<QueryMetrics> noWarmupList = this.EliminateWarmupIterations(queryStatisticsDatumVisitor.QueryMetricsList, roundTrips, warmupIterations);

                if (roundTrips > 1)
                {
                    double distinctOutputDocCount = noWarmupList.Select(x => x.OutputDocumentCount).Distinct().Count();
                    noWarmupList = distinctOutputDocCount >= roundTrips
                        ? this.SumOfRoundTrips(roundTrips, noWarmupList)
                        : this.SumOfRoundTripsWithoutOutputDoc(roundTrips, noWarmupList);
                }

                this.CalculateAverage(noWarmupList).ForEach(textWriter.WriteLine);
                textWriter.WriteLine();
                textWriter.WriteLine(PrintMedian);
                textWriter.WriteLine();
                this.CalculateMedian(noWarmupList.ToList()).ForEach(textWriter.WriteLine);
            }
            else
            {
                textWriter.WriteLine();
                textWriter.WriteLine(PrintQueryMetrics);
                foreach (QueryMetrics metrics in queryStatisticsDatumVisitor.QueryMetricsList)
                {
                    textWriter.WriteMetrics(metrics.RetrievedDocumentCount, metrics.RetrievedDocumentSize, metrics.OutputDocumentCount,
                        metrics.OutputDocumentSize, metrics.TotalQueryExecutionTime, metrics.DocumentLoadTime, metrics.DocumentWriteTime,
                        metrics.Created, metrics.ChannelAcquisitionStarted, metrics.Pipelined, metrics.TransitTime, metrics.Received,
                        metrics.Completed, metrics.PocoTime, metrics.GetCosmosElementResponseTime, metrics.EndToEndTime);
                    textWriter.WriteLine();
                }

                textWriter.WriteLine();
                textWriter.WriteLine(PrintBadRequests);
                foreach (QueryMetrics metrics in queryStatisticsDatumVisitor.BadRequestMetricsList)
                {
                    textWriter.WriteMetrics(metrics.BadRequestCreated, metrics.BadRequestChannelAcquisitionStarted, metrics.BadRequestPipelined,
                        metrics.BadRequestTransitTime, metrics.BadRequestReceived, metrics.BadRequestCompleted);
                    textWriter.WriteLine();
                }
            }

            textWriter.Flush();
        }
    }
}
