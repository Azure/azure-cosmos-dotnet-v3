namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
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

        private readonly Func<QueryStatisticsMetrics, double>[] metricsDelegate = new Func<QueryStatisticsMetrics, double>[]
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

        private List<double> CalculateAverage(List<QueryStatisticsMetrics> getAverageList)
        {
            List<double> avgList = new();
            foreach (Func<QueryStatisticsMetrics, double> metric in this.metricsDelegate)
            {
                avgList.Add(getAverageList.Average(metric));
            }

            return avgList;
        }

        private List<double> CalculateMedian(List<QueryStatisticsMetrics> getMedianList)
        {
            List<double> medianList = new();
            foreach (Func<QueryStatisticsMetrics, double> metric in this.metricsDelegate)
            {
                medianList.Add(this.GetMedian(getMedianList, getMedianList.ConvertAll(new Converter<QueryStatisticsMetrics, double>(metric))));
            }

            return medianList;
        }

        public double GetMedian(List<QueryStatisticsMetrics> getMedianList, List<double> sortedNumbers)
        {
            List<double> metricList = sortedNumbers;
            int mid = getMedianList.Count() / 2;
            metricList.Sort();
            double median = (getMedianList.Count() % 2) == 0
                ? (metricList.ElementAt(mid) + metricList.ElementAt(mid - 1)) / 2
                : metricList.ElementAt(mid);
            return median;
        }

        private List<QueryStatisticsMetrics> SumOfRoundTrips(int roundTrips, IReadOnlyList<QueryStatisticsMetrics> originalList)
        {
            List<QueryStatisticsMetrics> sumOfRoundTripsList = new();
            int i = 0;
            int j = roundTrips;
            while (j <= originalList.Count)
            {
                Range range = i..j;
                List<QueryStatisticsMetrics> metricsPerRoundTrip = originalList.Take(range).ToList();
                sumOfRoundTripsList.
                    Add(new QueryStatisticsMetrics
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

        private List<QueryStatisticsMetrics> EliminateWarmupIterations(IReadOnlyList<QueryStatisticsMetrics> noWarmupList, int roundTrips, int warmupIterations)
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
                List<QueryStatisticsMetrics> noWarmupList = this.EliminateWarmupIterations(queryStatisticsDatumVisitor.QueryMetricsList, roundTrips, warmupIterations);
                if (roundTrips > 1)
                {
                    noWarmupList = this.SumOfRoundTrips(roundTrips, noWarmupList);
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
                int roundTripCount = 1;
                int iterationCount = 1;
                textWriter.Write(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\"," +
                                        "\"{11}\",\"{12}\",\"{13}\",\"{14}\",\"{15}\",\"{16}\",\"{17}\"", "Iteration", "RoundTrip",
                                        "RetrievedDocumentCount", "RetrievedDocumentSize", "OutputDocumentCount", "OutputDocumentSize", "TotalQueryExecutionTime",
                                        "DocumentLoadTime", "DocumentWriteTime", "Created", "ChannelAcquisitionStarted", "Pipelined", "TransitTime", "Received",
                                        "Completed", "PocoTime", "GetCosmosElementResponseTime", "EndToEndTime"));
                textWriter.WriteLine();
                foreach (QueryStatisticsMetrics metrics in queryStatisticsDatumVisitor.QueryMetricsList)
                {
                    if (roundTripCount <= roundTrips)
                    {
                        textWriter.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\"," +
                                                "\"{11}\",\"{12}\",\"{13}\",\"{14}\",\"{15}\",\"{16}\",\"{17}\"", iterationCount, roundTripCount,
                                                metrics.RetrievedDocumentCount, metrics.RetrievedDocumentSize, metrics.OutputDocumentCount,
                                                metrics.OutputDocumentSize, metrics.TotalQueryExecutionTime, metrics.DocumentLoadTime, metrics.DocumentWriteTime,
                                                metrics.Created, metrics.ChannelAcquisitionStarted, metrics.Pipelined, metrics.TransitTime, metrics.Received,
                                                metrics.Completed, metrics.PocoTime, metrics.GetCosmosElementResponseTime, metrics.EndToEndTime));

                        roundTripCount++;
                    }
                    else
                    {
                        iterationCount++;
                        roundTripCount = 1;
                        textWriter.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\"," +
                                                "\"{11}\",\"{12}\",\"{13}\",\"{14}\",\"{15}\",\"{16}\",\"{17}\"", iterationCount, roundTripCount,
                                                metrics.RetrievedDocumentCount, metrics.RetrievedDocumentSize, metrics.OutputDocumentCount,
                                                metrics.OutputDocumentSize, metrics.TotalQueryExecutionTime, metrics.DocumentLoadTime, metrics.DocumentWriteTime,
                                                metrics.Created, metrics.ChannelAcquisitionStarted, metrics.Pipelined, metrics.TransitTime, metrics.Received,
                                                metrics.Completed, metrics.PocoTime, metrics.GetCosmosElementResponseTime, metrics.EndToEndTime));
                        roundTripCount++;
                    }
                }

                textWriter.WriteLine();
                textWriter.WriteLine(PrintBadRequests);
                foreach (QueryStatisticsMetrics metrics in queryStatisticsDatumVisitor.BadRequestMetricsList)
                {
                    textWriter.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", metrics.BadRequestCreated,
                        metrics.BadRequestChannelAcquisitionStarted, metrics.BadRequestPipelined, metrics.BadRequestTransitTime,
                        metrics.BadRequestReceived, metrics.BadRequestCompleted));
                    textWriter.WriteLine();
                }
            }
            textWriter.Flush();
        }
    }
}
