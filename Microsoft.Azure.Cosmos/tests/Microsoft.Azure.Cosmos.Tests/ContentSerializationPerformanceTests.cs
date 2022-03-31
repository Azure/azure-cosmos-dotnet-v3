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
        private static Database cosmosDatabase;
        readonly QueryStatisticsAccumulator queryStatisticsAccumulator = new();
        private readonly List<double> endToEndTimeList = new();
        private readonly List<double> pocoTimeList = new();
        private readonly List<double> getCosmosElementResponseTimeList = new();
        private const string cosmosDatabaseId = "";
        private const string containerId = "";
        private const string contentSerialization = "";
        private const string query = "";
        private const int numberOfIterations = 30;
        private const int warmupIterations = 10;

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

        public async Task RunAsync(CosmosClient client)
        {
            cosmosDatabase = client.GetDatabase(cosmosDatabaseId);
            Container container = cosmosDatabase.GetContainer(containerId);
            string filePath = "";
            string rawDataPath = "";
            for (int i = 0; i < numberOfIterations; i++)
            {
                await this.RunQueryAsync(container, query);
            }
            using (TextWriter rawDataFile = new StreamWriter(rawDataPath))
            {
                this.SerializeAsync(rawDataFile, this.queryStatisticsAccumulator, true);
            }
            using (TextWriter file = new StreamWriter(filePath, true))
            {
                this.SerializeAsync(file, this.queryStatisticsAccumulator, false);
            }
            using (TextWriter writer = Console.Out)
            {
                this.SerializeAsync(writer, this.queryStatisticsAccumulator, false);
            }
        }

        public async Task RunQueryAsync(Container container, string sql)
        {
            if (sql.ToLower().Contains("distinct"))
            {
                Stopwatch distinctQueryStopwatch = new Stopwatch();
                using (FeedIterator<string> distinctQueryIterator = container.GetItemQueryIterator<string>(
                    queryText: sql,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = -1,
                        MaxItemCount = -1,
                        CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: contentSerialization,
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text))
                    }))
                {
                    while (distinctQueryIterator.HasMoreResults)
                    {
                        distinctQueryStopwatch.Start();
                        FeedResponse<string> distinctQueryResponse = await distinctQueryIterator.ReadNextAsync();
                        {
                            distinctQueryStopwatch.Stop();
                            if (distinctQueryResponse.RequestCharge != 0)
                            {
                                this.GetDistinctQueryTrace(distinctQueryResponse);
                            }
                            distinctQueryStopwatch.Start();
                        }
                        distinctQueryStopwatch.Stop();
                        this.endToEndTimeList.Add(distinctQueryStopwatch.ElapsedMilliseconds);
                    }
                }
            }
            else
            {
                Stopwatch stopwatch = new Stopwatch();
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                    queryText: sql,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = -1,
                        MaxItemCount = -1,
                        CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: contentSerialization,
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary))
                    }))
                {
                    while (iterator.HasMoreResults)
                    {
                        stopwatch.Start();
                        FeedResponse<States> response = await iterator.ReadNextAsync();
                        {
                            stopwatch.Stop();
                            if (response.RequestCharge != 0)
                            {
                                this.GetTrace(response);
                            }
                            stopwatch.Start();
                        }
                        stopwatch.Stop();
                        this.endToEndTimeList.Add(stopwatch.ElapsedMilliseconds);
                    }
                }
            }
        }

        private void GetTrace(FeedResponse<States> Response)
        {
            string backendKeyValue = "Query Metrics";
            string backendNodeValue = "[,FF) move next";
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            ContentSerializationPerformanceTests contentSerializationPerformanceTests = new();
            List<ITrace> backendMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, backendNodeValue, backendKeyValue);
            if (backendMetrics != null)
            {
                foreach (ITrace node in backendMetrics)
                {
                    foreach (KeyValuePair<string, object> kvp in node.Data)
                    {
                        this.queryStatisticsAccumulator.Visit((QueryMetricsTraceDatum)kvp.Value);
                    }
                }
            }
            string transportKeyValue = "Client Side Request Stats";
            string transportNodeValue = "Microsoft.Azure.Documents.ServerStoreModel Transport Request";
            List<ITrace> transitMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, transportNodeValue, transportKeyValue);
            if (transitMetrics != null)
            {
                foreach (ITrace node in transitMetrics)
                {
                    foreach (KeyValuePair<string, object> kvp in node.Data)
                    {
                        this.queryStatisticsAccumulator.Visit((ClientSideRequestStatisticsTraceDatum)kvp.Value);
                    }
                }
            }
            string clientParseTimeNode = "POCO Materialization";
            ITrace poco = contentSerializationPerformanceTests.FindQueryClientMetrics(trace, clientParseTimeNode);
            if (poco != null)
            {
                this.pocoTimeList.Add(poco.Duration.TotalMilliseconds);
            }
            string clientDeserializationTimeNode = "Get Cosmos Element Response";
            List<ITrace> getCosmosElementResponse = contentSerializationPerformanceTests.FindQueryMetrics(trace, clientDeserializationTimeNode);
            if (getCosmosElementResponse != null)
            {
                foreach (ITrace getCosmos in getCosmosElementResponse)
                {
                    this.getCosmosElementResponseTimeList.Add(getCosmos.Duration.TotalMilliseconds);
                }
            }
        }

        private void GetDistinctQueryTrace(FeedResponse<string> Response)
        {
            string backendKeyValue = "Query Metrics";
            string backendNodeValue = "[,FF) move next";
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            ContentSerializationPerformanceTests contentSerializationPerformanceTests = new();
            List<ITrace> backendMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, backendNodeValue, backendKeyValue);
            if (backendMetrics != null)
            {
                foreach (ITrace node in backendMetrics)
                {
                    foreach (KeyValuePair<string, object> kvp in node.Data)
                    {
                        this.queryStatisticsAccumulator.Visit((QueryMetricsTraceDatum)kvp.Value);
                    }
                }
            }
            string transportKeyValue = "Client Side Request Stats";
            string transportNodeValue = "Microsoft.Azure.Documents.ServerStoreModel Transport Request";
            List<ITrace> transitMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, transportNodeValue, transportKeyValue);
            if (transitMetrics != null)
            {
                foreach (ITrace node in transitMetrics)
                {
                    foreach (KeyValuePair<string, object> kvp in node.Data)
                    {
                        this.queryStatisticsAccumulator.Visit((ClientSideRequestStatisticsTraceDatum)kvp.Value);
                    }
                }
            }
            string clientParseTimeNode = "POCO Materialization";
            ITrace poco = contentSerializationPerformanceTests.FindQueryClientMetrics(trace, clientParseTimeNode);
            if (poco != null)
            {
                this.pocoTimeList.Add(poco.Duration.TotalMilliseconds);
            }
            string clientDeserializationTimeNode = "Get Cosmos Element Response";
            ITrace getCosmosElementResponse = contentSerializationPerformanceTests.FindQueryClientMetrics(trace, clientDeserializationTimeNode);
            if (getCosmosElementResponse != null)
            {
                this.getCosmosElementResponseTimeList.Add(getCosmosElementResponse.Duration.TotalMilliseconds);
            }
        }
        private List<ITrace> FindQueryMetrics(ITrace trace, string nodeName, string keyName = null)
        {
            List<ITrace> queryMetricsNodes = new();
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (keyName != null && node.Data.ContainsKey(keyName) && node.Name == nodeName)
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

        private ITrace FindQueryClientMetrics(ITrace trace, string name)
        {
            Queue<ITrace> queue = new();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Name == name)
                {
                    return node;
                }

                foreach (ITrace child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }
            throw new Exception(name + " not found in Diagnostics");
        }

        private double CalculateAverage(List<double> avgList)
        {
            return Queryable.Average(avgList.AsQueryable());
        }

        private double CalculateMedian(List<double> medianList)
        {
            int listCount = medianList.Count();
            int mid = medianList.Count() / 2;
            IOrderedEnumerable<double> sortedNumbers = medianList.OrderBy(n => n);
            double median = (listCount % 2) == 0
                ? (sortedNumbers.ElementAt(mid) + sortedNumbers.ElementAt(mid - 1)) / 2
                : sortedNumbers.ElementAt(mid);
            return median;
        }

        private List<double> SumOfRoundTrips(int roundTrips, List<double> originalList)
        {
            List<double> sumOfRoundTripsList = new();
            int i = 0;
            while (i < originalList.Count)
            {
                double sumOfIterations = 0;
                for (int j = 0; j < roundTrips; j++)
                {
                    sumOfIterations += originalList[j];
                }
                sumOfRoundTripsList.Add(sumOfIterations);
                i += roundTrips;
            }
            return sumOfRoundTripsList;
        }

        private List<double> EliminateWarmupIterations(List<double> noWarmupList, int roundTrips)
        {
            int iterationsToEliminate = warmupIterations * roundTrips;
            noWarmupList = noWarmupList.GetRange(iterationsToEliminate, noWarmupList.Count - iterationsToEliminate);
            return noWarmupList;
        }

        private void SerializeAsync(TextWriter textWriter, QueryStatisticsAccumulator queryStatisticsAccumulator, bool rawData)
        {
            int roundTrips = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList.Count / numberOfIterations;
            if (rawData == false)
            {
                if (textWriter == Console.Out)
                {
                    textWriter.WriteLine((double)roundTrips);
                    if (roundTrips > 1)
                    {
                        List<double> distinctRetrievedDocumentCountList = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList.Distinct().ToList();
                        distinctRetrievedDocumentCountList = distinctRetrievedDocumentCountList.GetRange(0, roundTrips);
                        textWriter.WriteLine(distinctRetrievedDocumentCountList.Sum());
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList, roundTrips))));
                        if (queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Count > numberOfIterations)
                        {
                            List<double> distinctOutputDocumentCountList = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Distinct().ToList();
                            textWriter.WriteLine(distinctOutputDocumentCountList.Sum());
                            textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips)));
                        }
                        else
                        {
                            textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList, roundTrips))));
                            textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips))));
                        }
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList, roundTrips))));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList, roundTrips))));
                        textWriter.WriteLine();
                        if (this.pocoTimeList.Count > numberOfIterations)
                        {
                            textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.pocoTimeList, roundTrips))));
                            textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList, roundTrips))));
                            textWriter.WriteLine(this.CalculateAverage(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.endToEndTimeList, roundTrips))));
                        }
                        else
                        {
                            textWriter.WriteLine(this.CalculateAverage(this.pocoTimeList));
                            textWriter.WriteLine(this.CalculateAverage(this.getCosmosElementResponseTimeList));
                            textWriter.WriteLine(this.CalculateAverage(this.endToEndTimeList));
                        }
                        textWriter.WriteLine("\nMedian\n");
                        textWriter.WriteLine(roundTrips);
                        textWriter.WriteLine(distinctRetrievedDocumentCountList.Sum());
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList, roundTrips))));
                        if (queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Count > numberOfIterations)
                        {
                            List<double> distinctOutputDocumentCountList = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Distinct().ToList();
                            textWriter.WriteLine(distinctOutputDocumentCountList.Sum());
                            textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips)));
                        }
                        else
                        {
                            textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList, roundTrips))));
                            textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips))));
                        }
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList, roundTrips))));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList, roundTrips))));
                        textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList, roundTrips))));
                        textWriter.WriteLine();
                        if (this.pocoTimeList.Count > numberOfIterations)
                        {
                            textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.pocoTimeList, roundTrips))));
                            textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList, roundTrips))));
                            textWriter.WriteLine(this.CalculateMedian(this.SumOfRoundTrips(roundTrips, this.EliminateWarmupIterations(this.endToEndTimeList, roundTrips))));
                        }
                        else
                        {
                            textWriter.WriteLine(this.CalculateMedian(this.pocoTimeList));
                            textWriter.WriteLine(this.CalculateMedian(this.getCosmosElementResponseTimeList));
                            textWriter.WriteLine(this.CalculateMedian(this.endToEndTimeList));
                        }
                    }
                    else
                    {
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.pocoTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.endToEndTimeList, roundTrips)));
                        textWriter.WriteLine("\nMedian\n");
                        textWriter.WriteLine(roundTrips);
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.pocoTimeList, roundTrips)));
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList, roundTrips)));
                        textWriter.WriteLine();
                        textWriter.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.endToEndTimeList, roundTrips)));
                    }
                    textWriter.Flush();
                    textWriter.Close();
                }
                else
                {

                }
            }
            else
            {
                textWriter.WriteLine("EndToEndTime");
                foreach (double arr in this.endToEndTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Retrieved Document Count");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Retrieved Document Size");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Output Document Count");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Output Document Size");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("TotalQueryExecutionTime");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("DocumentLoadTime");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("DocumentWriteTime");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Created");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("ChannelAcquisitionStarted");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Pipelined");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("TransitTime");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Received");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("Completed");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("POCO");
                foreach (double arr in this.pocoTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("GetCosmosElementTime");
                foreach (double arr in this.getCosmosElementResponseTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestCreated");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestCreatedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestChannelAcquisitionStarted");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestChannelAcquisitionStartedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestPipelined");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestPipelinedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestTransitTime");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestTransitTimeList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestReceived");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestReceivedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.WriteLine();
                textWriter.WriteLine("BadRequestCompleted");
                foreach (double arr in queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.BadRequestCompletedList)
                {
                    textWriter.WriteLine(arr);
                }
                textWriter.Close();
            }
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
}
