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
            metric => metric.RUCharge,
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
                        RUCharge = metricsPerRoundTrip.Sum(metric => metric.RUCharge),
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

        public void Serialize(string outputPath, QueryStatisticsDatumVisitor visitor, int numberOfIterations, string query, Microsoft.Azure.Documents.SupportedSerializationFormats serializationFormat)
        {
            int roundTrips = visitor.QueryMetricsList.Count / numberOfIterations;
            string formattedQuery =  $"\"{query}\"";
            List<QueryStatisticsMetrics> metricsList = visitor.QueryMetricsList.ToList();
            if (roundTrips > 1)
            {
                metricsList = this.SumOfRoundTrips(roundTrips, metricsList);
            }

            string[] headers = {
                "Query",
                "TransportSerializationFormat",
                "RetrievedDocumentCount", 
                "RetrievedDocumentSize", 
                "OutputDocumentCount",
                "OutputDocumentSize",
                "RUCharge",
                "TotalQueryExecutionTime", 
                "DocumentLoadTime",
                "DocumentWriteTime",
                "Created", 
                "ChannelAcquisitionStarted",
                "Pipelined",
                "TransitTime", 
                "Received", 
                "Completed", 
                "PocoTime", 
                "GetCosmosElementResponseTime",
                "EndToEndTime"
            };

            string averagesPath = Path.Combine(outputPath, "averages.csv");
            bool fileExists = File.Exists(averagesPath);

            List<double> averageData = this.CalculateAverage(metricsList);
            using (StreamWriter writer = new StreamWriter(averagesPath, append: true))
            {
                if (!fileExists)
                {
                    writer.WriteLine(string.Join(",", headers));
                }

                writer.WriteLine(formattedQuery + "," + serializationFormat + "," + string.Join(",", averageData));
            }

            string mediansPath = Path.Combine(outputPath, "medians.csv");
            fileExists = File.Exists(mediansPath);

            List<double> medianData = this.CalculateMedian(metricsList);
            using (StreamWriter writer = new StreamWriter(mediansPath, append: true))
            {
                if (!fileExists)
                {
                    writer.WriteLine(string.Join(",", headers));
                }

                writer.WriteLine(formattedQuery + "," + serializationFormat + "," + string.Join(",", medianData));
            }

            string metricsPath = Path.Combine(outputPath, "raw_data.csv");
            fileExists = File.Exists(metricsPath);

            using (StreamWriter writer = new StreamWriter(metricsPath, append: true))
            {
                if (!fileExists)
                {
                    string[] fullHeaders = new string[] { "Iteration", "RoundTrip" }.Concat(headers).ToArray();
                    writer.WriteLine(string.Join(",", fullHeaders));
                }

                int iteration = 1;
                int roundTrip = 1;
                foreach (QueryStatisticsMetrics metrics in visitor.QueryMetricsList)
                {
                    object[] values = new object[]
                    {
                        formattedQuery,
                        serializationFormat,
                        iteration,
                        roundTrip,
                        metrics.RetrievedDocumentCount,
                        metrics.RetrievedDocumentSize,
                        metrics.OutputDocumentCount,
                        metrics.OutputDocumentSize,
                        metrics.RUCharge,
                        metrics.TotalQueryExecutionTime,
                        metrics.DocumentLoadTime,
                        metrics.DocumentWriteTime, 
                        metrics.Created,
                        metrics.ChannelAcquisitionStarted,
                        metrics.Pipelined,
                        metrics.TransitTime,
                        metrics.Received,
                        metrics.Completed,
                        metrics.PocoTime,
                        metrics.GetCosmosElementResponseTime,
                        metrics.EndToEndTime
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
                    "BadRequestCreated",
                    "BadRequestChannelAcquisitionStarted",
                    "BadRequestPipelined",
                    "BadRequestTransitTime",
                    "BadRequestReceived", 
                    "BadRequestCompleted"
                };
                writer.WriteLine(string.Join(",", badRequestHeaders));

                foreach (QueryStatisticsMetrics metrics in visitor.BadRequestMetricsList)
                {
                    double[] values = new double[]
                    {
                        metrics.BadRequestCreated,
                        metrics.BadRequestChannelAcquisitionStarted,
                        metrics.BadRequestPipelined,
                        metrics.BadRequestTransitTime,
                        metrics.BadRequestReceived,
                        metrics.BadRequestCompleted
                    };
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }
    }
}
