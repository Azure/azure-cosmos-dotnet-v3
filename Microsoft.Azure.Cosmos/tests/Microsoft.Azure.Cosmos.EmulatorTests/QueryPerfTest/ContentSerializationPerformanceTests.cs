namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [Ignore]
    [TestClass]
    public class ContentSerializationPerformanceTests
    {
        private const string DiagnosticsDataFileName = "ContentSerializationPerformanceTestsDiagnosticsData.txt";

        private readonly Dictionary<SupportedSerializationFormats, QueryStatisticsDatumVisitor> queryStatisticsDatumVisitorMap;
        private readonly string endpoint;
        private readonly string authKey;
        private readonly string cosmosDatabaseId;
        private readonly string containerId;
        private readonly int insertDocumentCount;
        private readonly IReadOnlyList<string> queries;
        private readonly int iterationCount;
        private readonly int warmupIterationCount;
        private readonly int maxConcurrency;
        private readonly int maxItemCount;
        private readonly bool useStronglyTypedIterator;
        private readonly string outputPath;
        private readonly string outputDirectoryName;

        public ContentSerializationPerformanceTests()
        {
            this.queryStatisticsDatumVisitorMap = new Dictionary<SupportedSerializationFormats, QueryStatisticsDatumVisitor>
            {
                { SupportedSerializationFormats.JsonText, new QueryStatisticsDatumVisitor() },
                { SupportedSerializationFormats.CosmosBinary, new QueryStatisticsDatumVisitor() }
            };

            this.endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
            this.authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            this.cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.CosmosDatabaseId"];
            this.containerId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.ContainerId"];
            this.insertDocumentCount = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.InsertDocumentCount"]);
            this.queries = JsonSerializer.Deserialize<List<string>>(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.Queries"]);
            this.iterationCount = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.NumberOfIterations"]);
            this.warmupIterationCount = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.WarmupIterations"]);
            this.maxConcurrency = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.MaxConcurrency"]);
            this.maxItemCount = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.MaxItemCount"]);
            this.useStronglyTypedIterator = bool.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.UseStronglyTypedIterator"]);
            this.outputPath = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.OutputPath"];
            this.outputDirectoryName = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.OutputDirectoryName"];
        }

        [TestMethod]
        public async Task SetupBenchmark()
        {
            CosmosClient client = new CosmosClient(
                this.endpoint,
                this.authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                });

            Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync(this.cosmosDatabaseId);

            FeedIterator<ContainerProperties> iterator = database.GetContainerQueryIterator<ContainerProperties>();

            bool containerExists = false;
            while (iterator.HasMoreResults)
            {
                FeedResponse<ContainerProperties> containers = await iterator.ReadNextAsync().ConfigureAwait(false);
                if (containers.Any(c => c.Id == this.containerId))
                {
                    containerExists = true;
                    break;
                }
            }
            
            if (containerExists)
            {
                Container previousContainer = database.GetContainer(this.containerId);
                await previousContainer.DeleteContainerAsync();
            }

            Container container = await database.CreateContainerIfNotExistsAsync(
                id: this.containerId,
                partitionKeyPath: "/myPartitionKey",
                throughput: 400
            );

            await this.InsertDocuments(container);
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

        private async Task RunAsync(CosmosClient client)
        {
            Container container = client.GetContainer(this.cosmosDatabaseId, this.containerId);
            string outputPath = Path.GetFullPath(!string.IsNullOrWhiteSpace(this.outputPath) ? this.outputPath : Directory.GetCurrentDirectory());

            string fullOutputPath = Path.Combine(outputPath, this.outputDirectoryName);
            if (!Directory.Exists(fullOutputPath))
            {
                Directory.CreateDirectory(fullOutputPath);
            }

            foreach (string query in this.queries)
            {
                foreach (SupportedSerializationFormats serializationFormat in new[] { SupportedSerializationFormats.JsonText, SupportedSerializationFormats.CosmosBinary })
                {
                    for (int i = 0; i < this.warmupIterationCount; i++)
                    {
                        await this.RunQueryAsync(container, query, serializationFormat, isWarmup: true);
                    }

                    MetricsSerializer metricsSerializer = new();
                    for (int i = 0; i < this.iterationCount; i++)
                    {
                        await this.RunQueryAsync(container, query, serializationFormat, isWarmup: false);
                    }
                    metricsSerializer.Serialize(fullOutputPath, this.queryStatisticsDatumVisitorMap[serializationFormat], this.iterationCount, query, serializationFormat);
                }
            }

            Console.WriteLine($"Output file path: {fullOutputPath}");
            File.Delete(Path.GetFullPath(DiagnosticsDataFileName));
        }

        private async Task InsertDocuments(Container container)
        {
            string[] regions = { "Arizona", "California", "Florida", "Utah", "New York", "Oregon" };

            for (int i = 0; i < this.insertDocumentCount; i++)
            {
                States state = new States
                {
                    MyPartitionKey = $"partition-{i}",
                    Id = $"id-{i}",
                    Name = $"State-{i}",
                    City = $"City-{i % 1000}",
                    PostalCode = $"{(10000 + (i % 90000))}",
                    Region = regions[i % regions.Length],
                    UserDefinedID = i % 1000,
                    WordsArray = new List<string> { "alpha", "beta", "gamma", "delta" },
                    Tags = new Tags
                    {
                        Words = new List<string> { "sun", "moon", "stars", "comet" },
                        Numbers = $"{i % 100}-{(i / 2) % 100}-{(i / 3) % 100}"
                    },
                    RecipientList = new List<RecipientList>
                    {
                        new RecipientList
                        {
                            Name = $"Recipient-{i % 100}",
                            City = $"RecipientCity-{i % 1000}",
                            PostalCode = $"{(20000 + (i * 7) % 80000)}",
                            Region = regions[i % regions.Length],
                            GUID = $"guid-{i}",
                            Quantity = (i % 99) + 1
                        }
                    }
                };

                await container.CreateItemAsync(state, new Cosmos.PartitionKey(state.MyPartitionKey));

                if(i % 1000 == 0)
                {
                    Console.WriteLine($"Number of documents inserted: " + i);
                }
            }
        }

        private async Task RunQueryAsync(Container container, string query, SupportedSerializationFormats serializationFormat, bool isWarmup)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = this.maxConcurrency,
                MaxItemCount = this.maxItemCount,
                SupportedSerializationFormats = serializationFormat
            };

            if (isWarmup)
            {
                using (FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(
                    queryText: query,
                    requestOptions: requestOptions))
                {
                    while (iterator.HasMoreResults)
                    {
                        await iterator.ReadNextAsync();
                    }
                }
            }
            else
            {
                if (this.useStronglyTypedIterator)
                {
                    using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                        queryText: query,
                        requestOptions: requestOptions))
                    {
                        await this.GetIteratorResponse(iterator, serializationFormat);
                    }
                }
                else
                {
                    using (FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(
                            queryText: query,
                            requestOptions: requestOptions))
                    {
                        await this.GetIteratorResponse(iterator, serializationFormat);
                    }
                }
            }
        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator, SupportedSerializationFormats serializationFormat)
        {
            string diagnosticDataPath = Path.GetFullPath(DiagnosticsDataFileName);
            QueryStatisticsDatumVisitor visitor = this.queryStatisticsDatumVisitorMap[serializationFormat];

            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Documents.ValueStopwatch totalTime = new Documents.ValueStopwatch();
            Documents.ValueStopwatch accumulateMetricsTime = new Documents.ValueStopwatch();

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
                    metricsAccumulator.ReadFromTrace(response, visitor);
                }

                accumulateMetricsTime.Stop();
                totalTime.Stop();

                if (response.RequestCharge != 0)
                {
                    visitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - accumulateMetricsTime.ElapsedMilliseconds);
                    visitor.AddRequestCharge(response.RequestCharge);
                    visitor.PopulateMetrics();
                }

                totalTime.Reset();
                accumulateMetricsTime.Reset();
            }
        }
    }
}