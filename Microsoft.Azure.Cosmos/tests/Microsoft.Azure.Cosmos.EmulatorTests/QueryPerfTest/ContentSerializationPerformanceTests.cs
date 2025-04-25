namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    //[Ignore]
    [TestClass]
    public class ContentSerializationPerformanceTests
    {
        private const string DiagnosticsDataFileName = "ContentSerializationPerformanceTestsDiagnosticsData.txt";

        private readonly QueryStatisticsDatumVisitor queryStatisticsDatumVisitor;
        private readonly string endpoint;
        private readonly string authKey;
        private readonly string cosmosDatabaseId;
        private readonly string containerId;
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
            this.cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.CosmosDatabaseId"];
            this.containerId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.ContainerId"];
            this.query = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.Query"];
            this.numberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.NumberOfIterations"]);
            this.warmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.WarmupIterations"]);
            this.MaxConcurrency = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.MaxConcurrency"]);
            this.MaxItemCount = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.MaxItemCount"]);
            this.useStronglyTypedIterator = bool.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.UseStronglyTypedIterator"]);
        }

        [TestMethod]
        public async Task RunBenchmark()
        {
            using (CosmosClient client = new CosmosClient(
                this.endpoint,
                this.authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                }))
            {
                await this.RunAsync(client);
            }
        }

        [TestMethod]
        public async Task SetupBenchmark()
        {
            // TODO FIX THIS
            CosmosClient client = new CosmosClient(
                this.endpoint,
                this.authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                });

            Database database = await client.CreateDatabaseIfNotExistsAsync(this.cosmosDatabaseId);
            Container container = await database.CreateContainerIfNotExistsAsync(
                id: this.containerId,
                partitionKeyPath: "/myPartitionKey",
                throughput: 400
            );

            await this.InsertRandomDocuments(container);
        }

        private async Task RunAsync(CosmosClient client)
        {
            // TODO add warmup runs here instead?

            //TODO Get container directly from client
            Database database = await client.CreateDatabaseIfNotExistsAsync(this.cosmosDatabaseId);
            Container container = await database.CreateContainerIfNotExistsAsync(
                id: this.containerId,
                partitionKeyPath: "/myPartitionKey",
                throughput: 400
            );

            MetricsSerializer metricsSerializer = new MetricsSerializer();
            for (int i = 0; i < this.numberOfIterations; i++)
            {
                await this.RunQueryAsync(container);
            }

            string rawDataPath = Path.GetFullPath(Directory.GetCurrentDirectory()); // Mayapainter fix ths
            Console.WriteLine($"File path for raw data: {rawDataPath}");
            metricsSerializer.Serialize(rawDataPath, this.queryStatisticsDatumVisitor, this.numberOfIterations, this.warmupIterations);
        }

        // TODO: make this part of "Test Setup"
        private async Task InsertRandomDocuments(Container container)
        {
            Random random = new Random();

            // update this to be configurable
            for (int i = 0; i < 1000; i++)
            {
                States state = new States
                {
                    MyPartitionKey = Guid.NewGuid().ToString(),
                    Id = Guid.NewGuid().ToString(),
                    Name = $"State-{i}",
                    City = $"City-{random.Next(1000)}",
                    PostalCode = $"{random.Next(10000, 99999)}",
                    Region = $"Region-{random.Next(1, 10)}",
                    UserDefinedID = random.Next(1000),
                    WordsArray = new List<string> { "alpha", "beta", "gamma", "delta" },
                    Tags = new Tags
                    {
                        Words = new List<string> { "sun", "moon", "stars", "comet" },
                        Numbers = $"{random.Next(100)}-{random.Next(100)}-{random.Next(100)}"
                    },
                    RecipientList = new List<RecipientList>
                    {
                        new RecipientList
                        {
                            Name = $"Recipient-{random.Next(100)}",
                            City = $"RecipientCity-{random.Next(1000)}",
                            PostalCode = $"{random.Next(10000, 99999)}",
                            Region = $"Region-{random.Next(1, 10)}",
                            GUID = Guid.NewGuid().ToString(),
                            Quantity = random.Next(1, 100)
                        }
                    }
                };

                await container.CreateItemAsync(state, new PartitionKey(state.MyPartitionKey));
            }
        }

        private async Task RunQueryAsync(Container container)
        {
            // TODO test both serialization options
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = this.MaxConcurrency,
                MaxItemCount = this.MaxItemCount,
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
            Documents.ValueStopwatch totalTime = new Documents.ValueStopwatch();
            Documents.ValueStopwatch accumulateMetricsTime = new Documents.ValueStopwatch();
            string diagnosticDataPath = Path.GetFullPath(DiagnosticsDataFileName);
            while (feedIterator.HasMoreResults)
            {
                totalTime.Start();
                FeedResponse<T> response = await feedIterator.ReadNextAsync();
                accumulateMetricsTime.Start();
                if (response.RequestCharge != 0)
                {
                    using (StreamWriter outputFile = new StreamWriter(path: diagnosticDataPath, append: true))
                    {
                        outputFile.WriteLine(response.Diagnostics.ToString());
                    }
                    metricsAccumulator.ReadFromTrace(response, this.queryStatisticsDatumVisitor);
                }

                accumulateMetricsTime.Stop();
                totalTime.Stop();

                if (response.RequestCharge != 0) // mayapainter: what is this?
                {
                    this.queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - accumulateMetricsTime.ElapsedMilliseconds);
                    this.queryStatisticsDatumVisitor.PopulateMetrics();
                }

                totalTime.Reset();
                accumulateMetricsTime.Reset();
            }
        }
    }
}