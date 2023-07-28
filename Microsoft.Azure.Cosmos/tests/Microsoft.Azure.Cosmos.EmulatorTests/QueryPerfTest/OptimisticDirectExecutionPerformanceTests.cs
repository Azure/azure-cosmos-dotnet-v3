namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OptimisticDirectExecutionPerformanceTests
    {
        private Container Container;
        private const string RawDataFileName = "OptimisticDirectExecutionPerformanceTestsRawData.csv";
        private const string DiagnosticsDataFileName = "OptimisticDirectExecutionPerformanceTestsAggregatedData.csv";
        private const string PrintQueryMetrics = "QueryMetrics";
        private static readonly string RawDataPath = Path.GetFullPath(RawDataFileName);
        private static readonly string AggregateDataPath = Path.GetFullPath(DiagnosticsDataFileName);
        private static readonly string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
        private static readonly string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
        private static readonly string cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.CosmosDatabaseId"];
        private static readonly string containerId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContainerId"];
        private static readonly PartitionKey partitionKeyValue = new PartitionKey("Andersen");
        private static readonly int numberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.NumberOfIterations"]);
        private static readonly int warmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.WarmupIterations"]);

        [TestInitialize]
        public async Task InitializeTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            CosmosClient cosmosClient = new CosmosClient(endpoint, authKey, clientOptions);
            Database database = await cosmosClient.CreateDatabaseAsync(cosmosDatabaseId);
            this.Container = await database.CreateContainerAsync(containerId, partitionKeyPath: "/name");
            await this.AddItemsToContainerAsync(this.Container);

            if (File.Exists(RawDataPath))
            {
                File.Delete(RawDataPath);
            }

            if (File.Exists(AggregateDataPath))
            {
                File.Delete(AggregateDataPath);
            }
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            if (this.Container != null)
            {
                await this.Container.Database.DeleteAsync();
                this.Container = null;
            }
        }

        private async Task AddItemsToContainerAsync(Container container)
        {
            int totalItems = 5000;
            string[] cityOptions = new string[] { "Seattle", "Chicago", "NYC", "SF" };

            // Create a family object for the Andersen family
            foreach (int i in Enumerable.Range(0, totalItems))
            {
                int numberOfRecipeints = cityOptions.Length;
                List<RecipientList> recipientList = new List<RecipientList>(numberOfRecipeints);

                for (int j = 0; j < numberOfRecipeints; j++)
                {
                    RecipientList recipient = new RecipientList()
                    {
                        Name = "John",
                        City = cityOptions[j],
                    };

                    recipientList.Add(recipient);
                }

                States andersenFamily = new States
                {
                    Id = i.ToString(),
                    Name = i < (totalItems/2) ? "Andersen" : "Smith",
                    City = cityOptions[i%cityOptions.Length],
                    PostalCode = (i * 10).ToString(),
                    Region = "Northwest",
                    UserDefinedID = i % 10,
                    RecipientList = recipientList
                };
          
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<States> andersenFamilyResponse = await container.CreateItemAsync<States>(andersenFamily, new PartitionKey(andersenFamily.Name));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", andersenFamilyResponse.Resource.Id, andersenFamilyResponse.RequestCharge);
            }
        }

        [TestMethod]
        [Owner("akotalwar")]
        public async Task RunAsync()
        {
            string highPrepTimeSumQuery = CreateHighPrepTimeSumQuery();
            string highPrepTimeConditionalQuery = CreateHighPrepTimeConditionalQuery();
            List<CustomOdeStats> globalCustomOdeStatisticsList = new List<CustomOdeStats>();

            List<DirectExecutionTestCase> odeTestCases = new List<DirectExecutionTestCase>()
            {
                //Simple Query
                CreateInput("SELECT * FROM c", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT * FROM c", partitionKeyValue, true, -1, 2500),

                //TOP
                CreateInput("SELECT TOP 1000 c.id FROM c", partitionKeyValue, false, -1, 1000),
                CreateInput("SELECT TOP 1000 c.id FROM c", partitionKeyValue, true, -1, 1000),
                
                //Filter
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", partitionKeyValue, false, -1, 1250),
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", partitionKeyValue, true, -1, 1250),
                
                //DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", partitionKeyValue, false, -1, 3),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", partitionKeyValue, true, -1, 3),
                
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", partitionKeyValue, false, -1, 2),
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", partitionKeyValue, true, -1, 2),

                //JOIN
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", partitionKeyValue, false, -1, 1),
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", partitionKeyValue, true, -1, 1),

                //High Prep Time
                CreateInput(highPrepTimeSumQuery, partitionKeyValue, false, -1, 2500),
                CreateInput(highPrepTimeSumQuery, partitionKeyValue, true, -1, 2500),

                CreateInput(highPrepTimeConditionalQuery, partitionKeyValue, false, -1, 1750),
                CreateInput(highPrepTimeConditionalQuery, partitionKeyValue, true, -1, 1750),

                //Order By
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", partitionKeyValue, true, -1, 2500),

                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", partitionKeyValue, true, -1, 2500),

                //Order By + TOP
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", partitionKeyValue, false, -1, 5),
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", partitionKeyValue, true, -1, 5),

                //Order By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", partitionKeyValue, true, -1, 2500),

                //Order By + DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", partitionKeyValue, false, -1, 4),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", partitionKeyValue, true, -1, 4),

                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", partitionKeyValue, false, -1, 6),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", partitionKeyValue, true, -1, 6),

                //Group By
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", partitionKeyValue, true, -1, 2500),

                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", partitionKeyValue, false, -1, 2),
                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", partitionKeyValue, true, -1, 2),

                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", partitionKeyValue, false, -1, 4),
                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", partitionKeyValue, true, -1, 4),

                //Group By + OFFSET
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", partitionKeyValue, false, -1, 3),
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", partitionKeyValue, true, -1, 3),

                //Group By + TOP
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", partitionKeyValue, false, -1, 25),
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", partitionKeyValue, true, -1, 25),

                //Group By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", partitionKeyValue, true, -1, 2500),

                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", partitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", partitionKeyValue, true, -1, 2500),
            };

            foreach (DirectExecutionTestCase testCase in odeTestCases)
            {
                List<CustomOdeStats> customOdeStats = await this.RunQueryAsync(testCase);
                globalCustomOdeStatisticsList.AddRange(customOdeStats);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(RawDataPath, FileMode.Append, FileAccess.Write)))
            {
                SerializeODEQueryMetrics(writer, globalCustomOdeStatisticsList, numberOfIterations, rawData: true);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(AggregateDataPath, FileMode.Append, FileAccess.Write)))
            {
                SerializeODEQueryMetrics(writer, globalCustomOdeStatisticsList, numberOfIterations, rawData: false);
            }
        }

        private async Task<List<CustomOdeStats>> RunQueryAsync(DirectExecutionTestCase queryInput)
        {
            List<CustomOdeStats> customOdeStats = new List<CustomOdeStats>();
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = queryInput.PageSizeOption,
                EnableOptimisticDirectExecution = queryInput.EnableOptimisticDirectExecution,
                PartitionKey = queryInput.PartitionKey,
            };

            for (int i = 0; i < numberOfIterations + warmupIterations; i++)
            {
                bool isWarmUpIteration = i < warmupIterations;
                using (FeedIterator<States> iterator = this.Container.GetItemQueryIterator<States>(
                        queryText: queryInput.Query,
                        requestOptions: requestOptions))
                {
                    if (isWarmUpIteration)
                    {
                        while (iterator.HasMoreResults)
                        {
                            await iterator.ReadNextAsync();
                        }
                    }
                    else
                    {
                        customOdeStats = await this.GetIteratorStatistics(iterator, queryInput);
                    }
                    
                }
            }

            return customOdeStats;
        }

        private async Task<List<CustomOdeStats>> GetIteratorStatistics<T>(FeedIterator<T> feedIterator, DirectExecutionTestCase queryInput)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Documents.ValueStopwatch totalTime = new Documents.ValueStopwatch();
            Documents.ValueStopwatch getTraceTime = new Documents.ValueStopwatch();
            Guid correlatedActivityId = Guid.NewGuid();
            FeedResponse<T> response;
            int totalDocumentCount = 0;
            string query;
            bool enableOde;
            List<CustomOdeStats> customOdeStats = new List<CustomOdeStats>();

            while (feedIterator.HasMoreResults)
            {
                QueryStatisticsDatumVisitor queryStatisticsDatumVisitor = new QueryStatisticsDatumVisitor();
                totalTime.Start();
                response = await feedIterator.ReadNextAsync();
                getTraceTime.Start();
                if (response.RequestCharge != 0)
                {
                    metricsAccumulator.ReadFromTrace(response, queryStatisticsDatumVisitor);
                }

                getTraceTime.Stop();
                totalTime.Stop();
                if (response.RequestCharge != 0)
                {
                    query = queryInput.Query;
                    enableOde = queryInput.EnableOptimisticDirectExecution;
                    queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds);
                    queryStatisticsDatumVisitor.PopulateMetrics();

                    QueryStatisticsMetrics queryStatistics = queryStatisticsDatumVisitor.QueryMetricsList[0];
                    queryStatistics.RUCharge = response.RequestCharge;
                    queryStatistics.CorrelatedActivityId = correlatedActivityId;

                    customOdeStats.Add(new CustomOdeStats
                    {
                        Query = query,
                        EnableOde = enableOde,
                        QueryStatisticsMetrics = queryStatistics
                    });
                }

                totalTime.Reset();
                getTraceTime.Reset();

                totalDocumentCount += response.Count;
            }

            Assert.AreEqual(queryInput.ExpectedResultCount, totalDocumentCount);

            return customOdeStats;
        }

        private static string CreateHighPrepTimeSumQuery()
        {
            int exprCount = 9999;
            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT r.id FROM root r WHERE ");
            for (int i = 0; i < exprCount; i++)
            {
                sb.Append(i == 0 ? "1" : "+1");
            }

            return sb.Append(" = " + exprCount + " ORDER BY r.id ASC").ToString();
        }

        private static string CreateHighPrepTimeConditionalQuery()
        {
            int exprCount = 999;
            StringBuilder sb = new StringBuilder();
            string[] cityOptions = new string[] { "Seattle", "Chicago", "NYC", "SF" };

            sb.Append("SELECT * FROM root r WHERE ");
            for (int nIdx = 0; nIdx < exprCount; nIdx++)
            {
                if (nIdx > 0)
                {
                    sb.Append(" OR ");
                }

                sb.Append($"r.userDefinedId > {nIdx} AND r.city = '{cityOptions[nIdx % cityOptions.Length]}'");
            }

            return sb.ToString();
        }

        private static void SerializeODEQueryMetrics(TextWriter textWriter, List<CustomOdeStats> customOdeStatisticsList, int numberOfIterations, bool rawData)
        {
            if (rawData)
            {
                SerializeODERawDataQueryMetrics(textWriter, customOdeStatisticsList);
            }
            else
            {
                SerializeODEProcessedDataQueryMetrics(textWriter, customOdeStatisticsList, numberOfIterations);
            }
        }

        private static void SerializeODERawDataQueryMetrics(TextWriter textWriter, List<OptimisticDirectExecutionPerformanceTests.CustomOdeStats> customOdeStatsList)
        {
            textWriter.WriteLine();
            textWriter.WriteLine(PrintQueryMetrics);
            textWriter.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\"", "Query", "ODE", "RUCharge", "BackendTime", "TransitTime", "ClientTime", "EndToEndTime");
            textWriter.WriteLine();
            double totalClientTime = 0;
            double totalBackendTime = 0;
            double totalEndToEndTime = 0;
            double totalTransitTime = 0;
            double totalRU = 0;
            string prevQuery = "";
            bool prevOde = default;
            Guid prevCorrelatedActivityId = customOdeStatsList[0].QueryStatisticsMetrics.CorrelatedActivityId;

            foreach (OptimisticDirectExecutionPerformanceTests.CustomOdeStats customOdeStats in customOdeStatsList)
            {
                QueryStatisticsMetrics metrics = customOdeStats.QueryStatisticsMetrics;
                double transitTime = metrics.Created + metrics.ChannelAcquisitionStarted + metrics.Pipelined + metrics.Received + metrics.Completed;
                double backendTime = metrics.TotalQueryExecutionTime;

                if (metrics.CorrelatedActivityId == prevCorrelatedActivityId)
                {
                    totalClientTime += metrics.EndToEndTime - (backendTime + transitTime);
                    totalBackendTime += backendTime;
                    totalEndToEndTime += metrics.EndToEndTime;
                    totalTransitTime += transitTime;
                    totalRU += metrics.RUCharge;
                    prevQuery = customOdeStats.Query;
                    prevOde = customOdeStats.EnableOde;
                    prevCorrelatedActivityId = metrics.CorrelatedActivityId;

                }
                else
                {
                    textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU},{totalBackendTime},{totalTransitTime},{totalClientTime},{totalEndToEndTime}");
                    totalClientTime = metrics.EndToEndTime - (backendTime + transitTime);
                    totalBackendTime = backendTime;
                    totalEndToEndTime = metrics.EndToEndTime;
                    totalTransitTime = transitTime;
                    totalRU = metrics.RUCharge;
                    prevCorrelatedActivityId = metrics.CorrelatedActivityId;
                    prevQuery = customOdeStats.Query;
                    prevOde = customOdeStats.EnableOde;
                }
            }

            textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU},{totalBackendTime},{totalTransitTime},{totalClientTime},{totalEndToEndTime}");
        }

        private static void SerializeODEProcessedDataQueryMetrics(TextWriter textWriter, List<OptimisticDirectExecutionPerformanceTests.CustomOdeStats> customOdeStatsList, int numberOfIterations)
        {
            textWriter.WriteLine();
            textWriter.WriteLine(PrintQueryMetrics);
            textWriter.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\"", "Query", "ODE", "RUCharge", "EndToEndTime");
            textWriter.WriteLine();

            string prevQuery = customOdeStatsList[0].Query;
            bool prevOde = customOdeStatsList[0].EnableOde;
            double totalEndToEndTime = 0;
            double totalRU = 0;

            foreach (OptimisticDirectExecutionPerformanceTests.CustomOdeStats customOdeStats in customOdeStatsList)
            {
                QueryStatisticsMetrics metrics = customOdeStats.QueryStatisticsMetrics;
                if (customOdeStats.Query == prevQuery && customOdeStats.EnableOde == prevOde)
                {
                    totalEndToEndTime += metrics.EndToEndTime;
                    totalRU += metrics.RUCharge;
                }
                else
                {
                    textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU / numberOfIterations},{totalEndToEndTime / numberOfIterations}");
                    totalEndToEndTime = metrics.EndToEndTime;
                    totalRU = metrics.RUCharge;
                    prevQuery = customOdeStats.Query;
                    prevOde = customOdeStats.EnableOde;
                }
            }

            textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU / numberOfIterations},{totalEndToEndTime / numberOfIterations}");
        }

        private class CustomOdeStats
        {
            public string Query { get; set; }
            public bool EnableOde { get; set; }
            public QueryStatisticsMetrics QueryStatisticsMetrics { get; set; }
        }

        private static DirectExecutionTestCase CreateInput(
            string query,
            PartitionKey? partitionKey,
            bool enableOptimisticDirectExecution,
            int pageSizeOption,
            int expectedResultCount)
        {
            return new DirectExecutionTestCase(query, partitionKey, enableOptimisticDirectExecution, pageSizeOption, expectedResultCount);
        }

        private readonly struct DirectExecutionTestCase
        {
            public string Query { get; }
            public PartitionKey? PartitionKey { get; }
            public bool EnableOptimisticDirectExecution { get; }
            public int PageSizeOption { get; }
            public int ExpectedResultCount { get; }

            public DirectExecutionTestCase(
                string query,
                PartitionKey? partitionKey,
                bool enableOptimisticDirectExecution,
                int pageSizeOption,
                int expectedResultCount)
            {
                this.Query = query;
                this.PartitionKey = partitionKey;
                this.EnableOptimisticDirectExecution = enableOptimisticDirectExecution;
                this.PageSizeOption = pageSizeOption;
                this.ExpectedResultCount = expectedResultCount;
            }
        }
    }
}