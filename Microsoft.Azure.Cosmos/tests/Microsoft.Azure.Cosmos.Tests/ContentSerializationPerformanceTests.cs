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
                //string authKey = "";
                //string endpoint = "";
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
            for (int i = 0; i < numberOfIterations; i++)
            {
                await this.RunQueryAsync(container, query);
            }

            using (TextWriter file = File.CreateText(filePath))
            {
                this.SerializeAsync(file, this.queryStatisticsAccumulator);
            }

            this.SerializeAsync(Console.Out, this.queryStatisticsAccumulator);
        }

        public async Task RunQueryAsync(Container container, string sql)
        {
            Stopwatch stopwatch = new Stopwatch();
            using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = -1,
                    CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                        contentSerializationFormat: contentSerialization,
                        createCustomNavigator: (content) => JsonNavigator.Create(content),
                        createCustomWriter: () => Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text))
                }))
            {
                while (iterator.HasMoreResults)
                {
                    stopwatch.Start();
                    FeedResponse<States> response = await iterator.ReadNextAsync();
                    {
                        stopwatch.Stop();
                        this.GetTrace(response);
                        stopwatch.Start();
                    }
                    stopwatch.Stop();
                    this.endToEndTimeList.Add(stopwatch.ElapsedMilliseconds);
                }
            }
        }

        private void GetTrace(FeedResponse<States> Response)
        {
            string backendKeyValue = "Query Metrics";
            string backendNodeValue = "[,FF) move next";
            ITrace trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            ContentSerializationPerformanceTests contentSerializationPerformanceTests = new();
            List<ITrace> backendMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, backendKeyValue, backendNodeValue);
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
            List<ITrace> transitMetrics = contentSerializationPerformanceTests.FindQueryMetrics(trace, transportKeyValue, transportNodeValue);
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

        private List<ITrace> FindQueryMetrics(ITrace trace, string keyName, string nodeName)
        {
            List<ITrace> queryMetricsNodes = new();
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);
            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Data.ContainsKey(keyName) && node.Name == nodeName)
                {
                    queryMetricsNodes.Add(node);
                }

                foreach (ITrace child in node.Children)
                    queue.Enqueue(child);
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
                    queue.Enqueue(child);
            }
            return null;
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

        private List<double> EliminateWarmupIterations(List<double> noWarmupList)
        {
            noWarmupList = noWarmupList.GetRange(warmupIterations, noWarmupList.Count - warmupIterations);
            return noWarmupList;
        }

        private void SerializeAsync(TextWriter textWriter, QueryStatisticsAccumulator queryStatisticsAccumulator)
        {
            if (textWriter == Console.Out)
            {
                using (TextWriter writer = Console.Out)
                {
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.endToEndTimeList)));
                    writer.WriteLine((double)queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList.Count / numberOfIterations);
                    if (queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList.Count > numberOfIterations)
                    {
                        List<double> DistinctList = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList.Distinct().ToList();
                        writer.WriteLine();
                        DistinctList.ForEach(writer.WriteLine);
                        writer.WriteLine();
                    }

                    else { writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList))); }
                    writer.WriteLine(this.CalculateAverage(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList));
                    if (queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Count > numberOfIterations)
                    {
                        List<double> DistinctList = queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList.Distinct().ToList();
                        writer.WriteLine();
                        DistinctList.ForEach(writer.WriteLine);
                        writer.WriteLine();
                    }

                    else { writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList))); }
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList)));
                    writer.WriteLine();
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList)));
                    writer.WriteLine();
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.pocoTimeList)));
                    writer.WriteLine(this.CalculateAverage(this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList)));
                    writer.WriteLine("\nMedian\n");
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.endToEndTimeList)));
                    writer.WriteLine((double)queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList.Count / numberOfIterations);
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentCountList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.RetrievedDocumentSizeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentCountList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.OutputDocumentSizeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TotalQueryExecutionTimeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentLoadTimeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.DocumentWriteTimeList)));
                    writer.WriteLine();
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CreatedList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ChannelAcquisitionStartedList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.PipelinedList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.TransitTimeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.ReceivedList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.CompletedList)));
                    writer.WriteLine();
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.pocoTimeList)));
                    writer.WriteLine(this.CalculateMedian(this.EliminateWarmupIterations(this.getCosmosElementResponseTimeList)));
                    writer.Flush();
                    writer.Close();
                }
            }
            else
            {
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
            public string[] Words { get; set; }
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
            public string[] WordsArray { get; set; }
            [JsonProperty(PropertyName = "tags")]
            public Tags Tags { get; set; }
            [JsonProperty(PropertyName = "recipientList")]
            public RecipientList[] RecipientList { get; set; }
            public static string PartitionKeyPath => "/myPartitionKey";
        }
    }
}
