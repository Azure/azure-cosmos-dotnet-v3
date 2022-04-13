namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using System.Diagnostics;
    using System.Linq;
    using System.IO;
    using Newtonsoft.Json;

    [TestClass]
    public class ContentSerializationPerformanceTests
    {
        private readonly QueryStatisticsAccumulator queryStatisticsAccumulator = new();
        private readonly List<double> endToEndTimeList = new();
        private readonly List<double> pocoTimeList = new();
        private readonly List<double> getCosmosElementResponseTimeList = new();
        private readonly string cosmosDatabaseId = System.Configuration.ConfigurationManager.AppSettings["CosmosDatabaseId"];
        private readonly string containerId = System.Configuration.ConfigurationManager.AppSettings["ContainerId"];
        private readonly string contentSerialization = System.Configuration.ConfigurationManager.AppSettings["ContentSerialization"];
        private readonly string query = System.Configuration.ConfigurationManager.AppSettings["Query"];
        private readonly int numberOfIterations = int.Parse(System.Configuration.ConfigurationManager.AppSettings["NumberOfIterations"]);
        private readonly int warmupIterations = int.Parse(System.Configuration.ConfigurationManager.AppSettings["WarmupIterations"]);

        [TestMethod]
        public async Task ConnectEndpoint()
        {
            try
            {
                string authKey = System.Configuration.ConfigurationManager.AppSettings["MasterKey"];
                string endpoint = System.Configuration.ConfigurationManager.AppSettings["GatewayEndpoint"];
                using (CosmosClient client = new CosmosClient(endpoint, authKey,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct
                    }))
                {
                    await this.RunAsync(client);
                }
            }
            catch (CosmosException cre)
            {
                Assert.Fail(cre.ToString());
            }
        }

        private async Task RunAsync(CosmosClient client)
        {
            Database cosmosDatabase = client.GetDatabase(this.cosmosDatabaseId);
            Container container = cosmosDatabase.GetContainer(this.containerId);
            string rawDataPath = "RawData.csv";
            SerializeData serializeData = new SerializeData();
            for (int i = 0; i < this.numberOfIterations; i++)
            {
                await this.RunQueryAsync(container, this.query);
            }
            using (TextWriter rawDataFile = new StreamWriter(rawDataPath))
            {
                serializeData.SerializeAsync(rawDataFile, this.queryStatisticsAccumulator, this.numberOfIterations, this.warmupIterations, rawData: true, this.endToEndTimeList, this.pocoTimeList, this.getCosmosElementResponseTimeList);
            }
            using (TextWriter writer = Console.Out)
            {
                serializeData.SerializeAsync(writer, this.queryStatisticsAccumulator, this.numberOfIterations, this.warmupIterations, rawData: false, this.endToEndTimeList, this.pocoTimeList, this.getCosmosElementResponseTimeList);
            }
        }

        private async Task RunQueryAsync(Container container, string sql)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = -1,
                MaxItemCount = -1,
                CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: this.contentSerialization,
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text))
            };
            if (sql.Contains("distinct", StringComparison.OrdinalIgnoreCase))
            {
                using (FeedIterator<string> distinctQueryIterator = container.GetItemQueryIterator<string>(
                    queryText: sql,
                    requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(distinctQueryIterator);
                }
            }
            else
            {
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                    queryText: sql,
                    requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(iterator);
                }
            }

        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator)
        {
            FetchMetrics fetchMetrics = new FetchMetrics();
            Stopwatch totalTime = new Stopwatch();
            Stopwatch getTraceTime = new Stopwatch();
            while (feedIterator.HasMoreResults)
            {
                totalTime.Start();
                FeedResponse<T> response = await feedIterator.ReadNextAsync();
                {
                    getTraceTime.Start();
                    if (response.RequestCharge != 0)
                    {
                        fetchMetrics.GetTrace(response, this.queryStatisticsAccumulator, this.pocoTimeList, this.getCosmosElementResponseTimeList);
                    }
                    getTraceTime.Stop();
                }
                totalTime.Stop();
            }
            this.endToEndTimeList.Add(totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds);
        }

        internal sealed class Tags
        {
            [JsonProperty(PropertyName = "words")]
            public List<string> Words { get; set; }
            [JsonProperty(PropertyName = "numbers")]
            public string Numbers { get; set; }
        }

        internal sealed class RecipientList
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "city")]
            public string City { get; set; }
            [JsonProperty(PropertyName = "postalcode")]
            public string PostalCode { get; set; }
            [JsonProperty(PropertyName = "region")]
            public string Region { get; set; }
            [JsonProperty(PropertyName = "guid")]
            public string GUID { get; set; }
            [JsonProperty(PropertyName = "quantity")]
            public int Quantity { get; set; }
        }

        internal sealed class States
        {
            [JsonProperty(PropertyName = "myPartitionKey")]
            public string MyPartitionKey { get; set; }
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "city")]
            public string City { get; set; }
            [JsonProperty(PropertyName = "postalcode")]
            public string PostalCode { get; set; }
            [JsonProperty(PropertyName = "region")]
            public string Region { get; set; }
            [JsonProperty(PropertyName = "userDefinedId")]
            public int UserDefinedID { get; set; }
            [JsonProperty(PropertyName = "wordsArray")]
            public List<string> WordsArray { get; set; }
            [JsonProperty(PropertyName = "tags")]
            public Tags Tags { get; set; }
            [JsonProperty(PropertyName = "recipientList")]
            public List<RecipientList> RecipientList { get; set; }
            public static string PartitionKeyPath => "/myPartitionKey";
        }
    }

    internal class FetchMetrics : ContentSerializationPerformanceTests
    {
        public void GetTrace<T>(FeedResponse<T> Response, QueryStatisticsAccumulator queryStatisticsAccumulator, List<double> pocoTimeList, List<double> getCosmosElementResponseTimeList)
        {
            string backendKeyValue = "Query Metrics";
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            List<ITrace> backendMetrics = this.FindQueryMetrics(trace: trace, keyName: backendKeyValue);
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
            List<ITrace> transitMetrics = this.FindQueryMetrics(trace, keyName: transportKeyValue);
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
            ITrace poco = this.FindQueryClientMetrics(trace: trace, nodeName: clientParseTimeNode);
            if (poco != null)
            {
                pocoTimeList.Add(poco.Duration.TotalMilliseconds);
            }
            string clientDeserializationTimeNode = "Get Cosmos Element Response";
            List<ITrace> getCosmosElementResponse = this.FindQueryMetrics(trace: trace, nodeName: clientDeserializationTimeNode);
            if (getCosmosElementResponse != null)
            {
                foreach (ITrace getCosmos in getCosmosElementResponse)
                {
                    getCosmosElementResponseTimeList.Add(getCosmos.Duration.TotalMilliseconds);
                }
            }
        }

        private List<ITrace> FindQueryMetrics(ITrace trace, string nodeName = null, string keyName = null)
        {
            List<ITrace> queryMetricsNodes = new();
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (keyName != null && node.Data.ContainsKey(keyName))
                {
                    queryMetricsNodes.Add(node);
                }
                else if (node.Name == nodeName)
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

        private ITrace FindQueryClientMetrics(ITrace trace, string nodeName)
        {
            Queue<ITrace> queue = new();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Name == nodeName)
                {
                    return node;
                }

                foreach (ITrace child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }
            throw new Exception(nodeName + " not found in Diagnostics");
        }
    }

    internal class SerializeData : ContentSerializationPerformanceTests
    {
        private List<double> CalculateAverage(List<QueryMetrics> getAverageList)
        {
            List<double> avgList = new();
            avgList.Add(getAverageList.Average(metric => metric.RetrievedDocumentCount));
            avgList.Add(getAverageList.Average(metric => metric.RetrievedDocumentSize));
            avgList.Add(getAverageList.Average(metric => metric.OutputDocumentCount));
            avgList.Add(getAverageList.Average(metric => metric.OutputDocumentSize));
            avgList.Add(getAverageList.Average(metric => metric.TotalQueryExecutionTime));
            avgList.Add(getAverageList.Average(metric => metric.DocumentLoadTime));
            avgList.Add(getAverageList.Average(metric => metric.DocumentWriteTime));
            avgList.Add(getAverageList.Average(metric => metric.Created));
            avgList.Add(getAverageList.Average(metric => metric.ChannelAcquisitionStarted));
            avgList.Add(getAverageList.Average(metric => metric.Pipelined));
            avgList.Add(getAverageList.Average(metric => metric.TransitTime));
            avgList.Add(getAverageList.Average(metric => metric.Received));
            avgList.Add(getAverageList.Average(metric => metric.Completed));
            return avgList;
        }

        /*private List<double> CalculateMedian(List<QueryMetrics> getMedianList)
        {
            List<double> medianList = new();
            int mid = getMedianList.Count() / 2;
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.RetrievedDocumentCount)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.RetrievedDocumentSize)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.OutputDocumentCount)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.OutputDocumentSize)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.TotalQueryExecutionTime)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.DocumentLoadTime)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.DocumentWriteTime)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.Created)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.ChannelAcquisitionStarted)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.Pipelined)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.TransitTime)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.Received)));
            medianList.Add(getMedian(getMedianList.OrderBy(metric => metric.Completed)));
            return medianList;
            double getMedian(IOrderedEnumerable<QueryMetrics> sortedNumbers)
            {
                double median = (getMedianList.Count() % 2) == 0
                    ? (Convert.ToDouble(sortedNumbers.ElementAt(mid)) + Convert.ToDouble(sortedNumbers.ElementAt(mid - 1))) / 2
                    : Convert.ToDouble(sortedNumbers.ElementAt(mid));
                return median;
            }
        }*/

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
                        Completed = metricsPerRoundTrip.Sum(metric => metric.Completed)
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

        public void SerializeAsync(TextWriter textWriter, QueryStatisticsAccumulator queryStatisticsAccumulator, int numberOfIterations, int warmupIterations, bool rawData, List<double> endToEndTimeList, List<double> pocoTimeList, List<double> getCosmosElementResponseTimeList)
        {
            int roundTrips = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList.Count / numberOfIterations;
            if (rawData == false)
            {
                textWriter.WriteLine((double)roundTrips);
                if (roundTrips > 1)
                {
                    this.CalculateAverage(
                    this.SumOfRoundTrips(roundTrips,
                            this.EliminateWarmupIterations(
                                queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList, roundTrips, warmupIterations))).ForEach(textWriter.WriteLine);
                    /*textWriter.WriteLine("Median");
                    this.CalculateMedian(
                    this.SumOfRoundTrips(roundTrips,
                            this.EliminateWarmupIterations(
                                queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList, roundTrips, warmupIterations))).ForEach(textWriter.WriteLine);
                    */
                }
                else
                {
                    this.CalculateAverage(
                        this.EliminateWarmupIterations(
                            queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList, roundTrips, warmupIterations)).ForEach(textWriter.WriteLine);
                    /*textWriter.WriteLine("Median");
                    this.CalculateMedian(
                    this.SumOfRoundTrips(roundTrips,
                            this.EliminateWarmupIterations(
                                queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList, roundTrips, warmupIterations))).ForEach(textWriter.WriteLine);
                    */
                }
                textWriter.Flush();
                textWriter.Close();
            }
            else
            {
                textWriter.WriteLine("EndToEndTime");
                foreach (double arr in endToEndTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("QueryMetrics");
                foreach (QueryMetrics metrics in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList)
                {
                    textWriter.WriteLine(metrics.RetrievedDocumentCount);
                    textWriter.WriteLine(metrics.RetrievedDocumentSize);
                    textWriter.WriteLine(metrics.OutputDocumentCount);
                    textWriter.WriteLine(metrics.OutputDocumentSize);
                    textWriter.WriteLine(metrics.TotalQueryExecutionTime);
                    textWriter.WriteLine(metrics.DocumentLoadTime);
                    textWriter.WriteLine(metrics.DocumentWriteTime);
                    textWriter.WriteLine(metrics.Created);
                    textWriter.WriteLine(metrics.ChannelAcquisitionStarted);
                    textWriter.WriteLine(metrics.Pipelined);
                    textWriter.WriteLine(metrics.TransitTime);
                    textWriter.WriteLine(metrics.Received);
                    textWriter.WriteLine(metrics.Completed);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine();
                textWriter.WriteLine("POCO");
                foreach (double arr in pocoTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("GetCosmosElementTime");
                foreach (double arr in getCosmosElementResponseTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequest");
                foreach (QueryMetrics metrics in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.QueryMetricsList)
                {
                    textWriter.WriteLine(metrics.BadRequestCreated);
                    textWriter.WriteLine(metrics.BadRequestChannelAcquisitionStarted);
                    textWriter.WriteLine(metrics.BadRequestPipelined);
                    textWriter.WriteLine(metrics.BadRequestTransitTime);
                    textWriter.WriteLine(metrics.BadRequestReceived);
                    textWriter.WriteLine(metrics.BadRequestCompleted);
                    textWriter.WriteLine();
                }
                textWriter.Close();
            }
        }
    }
}