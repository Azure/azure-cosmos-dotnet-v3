namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [Ignore]
    [TestClass]
    public class ContentSerializationPerformanceTests
    {
        private const string RawDataFileName = "ContentSerializationPerformanceTestsRawData.csv";
        private const string DiagnosticsDataFileName = "ContentSerializationPerformanceTestsDiagnosticsData.txt";

        private readonly QueryStatisticsDatumVisitor queryStatisticsDatumVisitor;
        private readonly string endpoint;
        private readonly string authKey;
        private readonly string cosmosDatabaseId;
        private readonly string containerId;
        private readonly string contentSerialization;
        private readonly string query;
        private readonly int numberOfIterations;
        private readonly int warmupIterations;
        private readonly int MaxConcurrency;
        private readonly int MaxItemCount;
        private readonly bool useStronglyTypedIterator;

        public ContentSerializationPerformanceTests()
        {
            this.queryStatisticsDatumVisitor = new();
            this.endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
            this.authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            this.cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.CosmosDatabaseId"];
            this.containerId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContainerId"];
            this.contentSerialization = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContentSerialization"];
            this.query = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.Query"];
            this.numberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.NumberOfIterations"]);
            this.warmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.WarmupIterations"]);
            this.MaxConcurrency = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.MaxConcurrency"]);
            this.MaxItemCount = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.MaxItemCount"]);
            this.useStronglyTypedIterator = bool.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.UseStronglyTypedIterator"]);
        }

        [TestMethod]
        public async Task ConnectEndpoint()
        {
            using (CosmosClient client = new CosmosClient(this.endpoint, this.authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                }))
            {
                await this.RunAsync(client);
            }
        }

        private async Task RunAsync(CosmosClient client)
        {
            Database cosmosDatabase = client.GetDatabase(this.cosmosDatabaseId);
            Container container = cosmosDatabase.GetContainer(this.containerId);
            string rawDataPath = Path.GetFullPath(RawDataFileName);
            MetricsSerializer metricsSerializer = new MetricsSerializer();
            for (int i = 0; i < this.numberOfIterations; i++)
            {
                await this.RunQueryAsync(container);
            }

            Console.WriteLine("File path for raw data: ", rawDataPath);
            using (StreamWriter writer = new StreamWriter(new FileStream(rawDataPath, FileMode.Create, FileAccess.Write)))
            {
                metricsSerializer.Serialize(writer, this.queryStatisticsDatumVisitor, this.numberOfIterations, this.warmupIterations, rawData: true);
            }

            metricsSerializer.Serialize(Console.Out, this.queryStatisticsDatumVisitor, this.numberOfIterations, this.warmupIterations, rawData: false);
        }

        private async Task RunQueryAsync(Container container)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = this.MaxConcurrency,
                MaxItemCount = this.MaxItemCount,
                CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: this.contentSerialization,
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => JsonWriter.Create(this.contentSerialization == "JsonText" ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary))
            };

            if (this.useStronglyTypedIterator)
            {
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                    queryText: this.query,
                    requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(iterator);
                }
            }
            else
            {
                using (FeedIterator<dynamic> distinctQueryIterator = container.GetItemQueryIterator<dynamic>(
                        queryText: this.query,
                        requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(distinctQueryIterator);
                }
            }
        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Stopwatch totalTime = new Stopwatch();
            Stopwatch getTraceTime = new Stopwatch();
            string diagnosticDataPath = Path.GetFullPath(DiagnosticsDataFileName);
            while (feedIterator.HasMoreResults)
            {
                totalTime.Start();
                FeedResponse<T> response = await feedIterator.ReadNextAsync();
                getTraceTime.Start();
                if (response.RequestCharge != 0)
                {
                    using (StreamWriter outputFile = new StreamWriter(path: diagnosticDataPath, append: true))
                    {
                        outputFile.WriteLine(response.Diagnostics.ToString());
                    }
                    metricsAccumulator.ReadFromTrace(response, this.queryStatisticsDatumVisitor);
                }

                getTraceTime.Stop();
                totalTime.Stop();
                if (response.RequestCharge != 0)
                {
                    this.queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds);
                    this.queryStatisticsDatumVisitor.PopulateMetrics();
                }
                totalTime.Reset();
                getTraceTime.Reset();
            }
        }
    }
}