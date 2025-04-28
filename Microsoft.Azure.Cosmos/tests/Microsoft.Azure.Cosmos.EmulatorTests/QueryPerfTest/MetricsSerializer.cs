namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class MetricsSerializer
    {
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

        public void Serialize(string outputPath, QueryStatisticsDatumVisitor visitor, int numberOfIterations, int warmupIterations, Microsoft.Azure.Documents.SupportedSerializationFormats serializationFormat)
        {
            int roundTrips = visitor.QueryMetricsList.Count / numberOfIterations;
            List<QueryStatisticsMetrics> metricsList = this.EliminateWarmupIterations(visitor.QueryMetricsList, roundTrips, warmupIterations);
            if (roundTrips > 1)
            {
                metricsList = this.SumOfRoundTrips(roundTrips, metricsList);
            }

            string[] headers = {
                "RetrievedDocumentCount", "RetrievedDocumentSize", "OutputDocumentCount", "OutputDocumentSize",
                "TotalQueryExecutionTime", "DocumentLoadTime", "DocumentWriteTime", "Created", "ChannelAcquisitionStarted",
                "Pipelined", "TransitTime", "Received", "Completed", "PocoTime", "GetCosmosElementResponseTime", "EndToEndTime"
            };

            // Append to averages.csv
            string averagesPath = Path.Combine(outputPath, "averages.csv");
            List<double> averageData = this.CalculateAverage(metricsList);
            using (StreamWriter writer = new StreamWriter(averagesPath))
            {
                writer.WriteLine(string.Join(",", headers));
                writer.WriteLine(string.Join(",", averageData));
            }

            // Append to medians.csv
            string mediansPath = Path.Combine(outputPath, "medians.csv");
            List<double> medianData = this.CalculateMedian(metricsList);
            using (StreamWriter writer = new StreamWriter(mediansPath))
            {
                writer.WriteLine(string.Join(",", headers));
                writer.WriteLine(string.Join(",", medianData));
            }

            // Create raw_data.csv
            string metricsPath = Path.Combine(outputPath, "raw_data.csv");
            using (StreamWriter writer = new StreamWriter(metricsPath))
            {
                string[] fullHeaders = new string[] { "Iteration", "RoundTrip" }.Concat(headers).ToArray();
                writer.WriteLine(string.Join(",", fullHeaders));

                int iteration = 1;
                int roundTrip = 1;
                foreach (QueryStatisticsMetrics metrics in visitor.QueryMetricsList)
                {
                    object[] values = new object[]
                    {
                        iteration, roundTrip,
                        metrics.RetrievedDocumentCount, metrics.RetrievedDocumentSize, metrics.OutputDocumentCount,
                        metrics.OutputDocumentSize, metrics.TotalQueryExecutionTime, metrics.DocumentLoadTime,
                        metrics.DocumentWriteTime, metrics.Created, metrics.ChannelAcquisitionStarted, metrics.Pipelined,
                        metrics.TransitTime, metrics.Received, metrics.Completed, metrics.PocoTime,
                        metrics.GetCosmosElementResponseTime, metrics.EndToEndTime
                    };

                    writer.WriteLine(string.Join(",", values));
                    iteration++;
                    if (iteration > numberOfIterations)
                    {
                        iteration = 1;
                        roundTrip++;
                    }
                }
            }

            // Write bad_requests.csv
            string badRequestsPath = Path.Combine(outputPath, "bad_requests.csv");
            using (StreamWriter writer = new StreamWriter(badRequestsPath))
            {
                string[] badRequestHeaders = {
                    "BadRequestCreated", "BadRequestChannelAcquisitionStarted", "BadRequestPipelined",
                    "BadRequestTransitTime", "BadRequestReceived", "BadRequestCompleted"
                };
                writer.WriteLine(string.Join(",", badRequestHeaders));

                foreach (QueryStatisticsMetrics metrics in visitor.BadRequestMetricsList)
                {
                    double[] values = new double[]
                    {
                        metrics.BadRequestCreated, metrics.BadRequestChannelAcquisitionStarted,
                        metrics.BadRequestPipelined, metrics.BadRequestTransitTime,
                        metrics.BadRequestReceived, metrics.BadRequestCompleted
                    };
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }

    }
}
