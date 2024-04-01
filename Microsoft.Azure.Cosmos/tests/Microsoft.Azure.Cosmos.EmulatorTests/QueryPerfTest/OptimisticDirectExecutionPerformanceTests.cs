namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OptimisticDirectExecutionPerformanceTests
    {
        private Container Container;
        private const string RawDataFileName = "OptimisticDirectExecutionPerformanceTestsRawData.csv";
        private const string AggregateDataFileName = "OptimisticDirectExecutionPerformanceTestsAggregatedData.csv";
        private const string PrintQueryMetrics = "QueryMetrics";
        private static readonly string RawDataPath = Path.GetFullPath(RawDataFileName);
        private static readonly string AggregateDataPath = Path.GetFullPath(AggregateDataFileName);
        private static readonly string Endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
        private static readonly string AuthKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
        private static readonly string CosmosDatabaseId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.CosmosDatabaseId"];
        private static readonly string ContainerId = Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.ContainerId"];
        private static readonly PartitionKey PartitionKeyValue = new PartitionKey("Andersen");
        private static readonly int NumberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.NumberOfIterations"]);
        private static readonly int WarmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["QueryPerformanceTests.WarmupIterations"]);

        [TestInitialize]
        public async Task InitializeTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            CosmosClient cosmosClient = new CosmosClient(Endpoint, AuthKey, clientOptions);
            Database database = await cosmosClient.CreateDatabaseAsync(CosmosDatabaseId);
            this.Container = await database.CreateContainerAsync(ContainerId, partitionKeyPath: "/name");
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

            // Create a family object for the Andersen family
            foreach (int i in Enumerable.Range(0, totalItems))
            {
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
          
                ItemResponse<States> andersenFamilyResponse = await container.CreateItemAsync<States>(andersenFamily, new PartitionKey(andersenFamily.Name));
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", andersenFamilyResponse.Resource.Id, andersenFamilyResponse.RequestCharge);
            }
        }

        //Set Ode perf tests to ignore so that they dont run on every loop build. 
        //Ignore flag can be removed when checking for Ode performance.
        [Ignore]
        [TestMethod]
        [Owner("akotalwar")]
        public async Task RunAsync()
        {
            string highPrepTimeSumQuery = CreateHighPrepTimeSumQuery();
            string highPrepTimeConditionalQuery = CreateHighPrepTimeConditionalQuery();
            List<List<OdeQueryStatistics>> globalCustomOdeStatisticsList = new List<List<OdeQueryStatistics>>();

            List<DirectExecutionTestCase> odeTestCases = new List<DirectExecutionTestCase>()
            {
                //Simple Query
                CreateInput("SELECT * FROM c", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT * FROM c", PartitionKeyValue, true, -1, 2500),

                //TOP
                CreateInput("SELECT TOP 1000 c.id FROM c", PartitionKeyValue, false, -1, 1000),
                CreateInput("SELECT TOP 1000 c.id FROM c", PartitionKeyValue, true, -1, 1000),
                
                //Filter
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", PartitionKeyValue, false, -1, 1250),
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", PartitionKeyValue, true, -1, 1250),
                
                //DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", PartitionKeyValue, false, -1, 3),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", PartitionKeyValue, true, -1, 3),
                
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", PartitionKeyValue, false, -1, 2),
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", PartitionKeyValue, true, -1, 2),

                //JOIN
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", PartitionKeyValue, false, -1, 1),
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", PartitionKeyValue, true, -1, 1),

                //High Prep Time
                CreateInput(highPrepTimeSumQuery, PartitionKeyValue, false, -1, 2500),
                CreateInput(highPrepTimeSumQuery, PartitionKeyValue, true, -1, 2500),

                CreateInput(highPrepTimeConditionalQuery, PartitionKeyValue, false, -1, 1750),
                CreateInput(highPrepTimeConditionalQuery, PartitionKeyValue, true, -1, 1750),

                //Order By
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", PartitionKeyValue, true, -1, 2500),

                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", PartitionKeyValue, true, -1, 2500),

                //Order By + TOP
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", PartitionKeyValue, false, -1, 5),
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", PartitionKeyValue, true, -1, 5),

                //Order By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", PartitionKeyValue, true, -1, 2500),

                //Order By + DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", PartitionKeyValue, false, -1, 4),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", PartitionKeyValue, true, -1, 4),

                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", PartitionKeyValue, false, -1, 6),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", PartitionKeyValue, true, -1, 6),

                //Group By
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", PartitionKeyValue, true, -1, 2500),

                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", PartitionKeyValue, false, -1, 2),
                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", PartitionKeyValue, true, -1, 2),

                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", PartitionKeyValue, false, -1, 4),
                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", PartitionKeyValue, true, -1, 4),

                //Group By + OFFSET
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", PartitionKeyValue, false, -1, 3),
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", PartitionKeyValue, true, -1, 3),

                //Group By + TOP
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", PartitionKeyValue, false, -1, 25),
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", PartitionKeyValue, true, -1, 25),

                //Group By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", PartitionKeyValue, true, -1, 2500),

                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", PartitionKeyValue, false, -1, 2500),
                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", PartitionKeyValue, true, -1, 2500),
            };

            foreach (DirectExecutionTestCase testCase in odeTestCases)
            {
                globalCustomOdeStatisticsList.AddRange(await this.RunQueryAsync(testCase));
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(RawDataPath, FileMode.Append, FileAccess.Write)))
            {
                SerializeODEQueryMetrics(writer, globalCustomOdeStatisticsList, NumberOfIterations, rawData: true);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(AggregateDataPath, FileMode.Append, FileAccess.Write)))
            {
                SerializeODEQueryMetrics(writer, globalCustomOdeStatisticsList, NumberOfIterations, rawData: false);
            }
        }

        private async Task<List<List<OdeQueryStatistics>>> RunQueryAsync(DirectExecutionTestCase queryInput)
        {
            List<List<OdeQueryStatistics>> odeQueryStatisticsList = new List<List<OdeQueryStatistics>>();
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = queryInput.PageSizeOption,
                EnableOptimisticDirectExecution = queryInput.EnableOptimisticDirectExecution,
                PartitionKey = queryInput.PartitionKey,
            };

            for (int i = 0; i < NumberOfIterations + WarmupIterations; i++)
            {
                bool isWarmUpIteration = i < WarmupIterations;
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
                        odeQueryStatisticsList.Add(await this.GetIteratorStatistics(iterator, queryInput));
                    }
                }
            }

            return odeQueryStatisticsList;
        }

        private async Task<List<OdeQueryStatistics>> GetIteratorStatistics<T>(FeedIterator<T> feedIterator, DirectExecutionTestCase queryInput)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Guid correlatedActivityId = Guid.NewGuid();
            FeedResponse<T> response;
            int totalDocumentCount = 0;
            string query;
            bool enableOde;
            List<OdeQueryStatistics> odeQueryStatisticsList = new List<OdeQueryStatistics>();

            while (feedIterator.HasMoreResults)
            {
                QueryStatisticsDatumVisitor queryStatisticsDatumVisitor = new QueryStatisticsDatumVisitor();
                System.Diagnostics.Stopwatch totalTime = new System.Diagnostics.Stopwatch();
                System.Diagnostics.Stopwatch traceTime = new System.Diagnostics.Stopwatch();

                totalTime.Start();
                response = await feedIterator.ReadNextAsync();
                traceTime.Start();
                if (response.RequestCharge != 0)
                {
                    metricsAccumulator.ReadFromTrace(response, queryStatisticsDatumVisitor);
                }

                traceTime.Stop();
                totalTime.Stop();

                query = queryInput.Query;
                enableOde = queryInput.EnableOptimisticDirectExecution;
                queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - traceTime.ElapsedMilliseconds);
                queryStatisticsDatumVisitor.PopulateMetrics();

                QueryStatisticsMetrics queryStatistics = queryStatisticsDatumVisitor.QueryMetricsList[0];
                queryStatistics.RUCharge = response.RequestCharge;
                queryStatistics.CorrelatedActivityId = correlatedActivityId;

                // Each roundtrip is a new item in the list
                odeQueryStatisticsList.Add(new OdeQueryStatistics
                {
                    Query = query,
                    EnableOde = enableOde,
                    QueryStatisticsMetrics = queryStatistics
                });

                totalDocumentCount += response.Count;
            }

            Assert.AreEqual(queryInput.ExpectedResultCount, totalDocumentCount);

            return odeQueryStatisticsList;
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

        private static void SerializeODEQueryMetrics(TextWriter textWriter, List<List<OdeQueryStatistics>> customOdeStatisticsList, int numberOfIterations, bool rawData)
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

        private static void SerializeODERawDataQueryMetrics(TextWriter textWriter, List<List<OdeQueryStatistics>> globalOdeQueryStatisticsList)
        {
            textWriter.WriteLine();
            textWriter.WriteLine(PrintQueryMetrics);
            textWriter.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\"", "Query", "ODE", "RUCharge", "BackendTime", "TransitTime", "ClientTime", "EndToEndTime");
            textWriter.WriteLine();

            foreach (List<OdeQueryStatistics> queryStatisticsList in globalOdeQueryStatisticsList)
            {
                double totalClientTime = 0;
                double totalBackendTime = 0;
                double totalEndToEndTime = 0;
                double totalTransitTime = 0;
                double totalRU = 0;
                string query = "";
                bool ode = false;

                foreach (OdeQueryStatistics queryStatistics in queryStatisticsList)
                {
                    QueryStatisticsMetrics metrics = queryStatistics.QueryStatisticsMetrics;
                    double transitTime = metrics.Created + metrics.ChannelAcquisitionStarted + metrics.Pipelined + metrics.Received + metrics.Completed;
                    double backendTime = metrics.TotalQueryExecutionTime;

                    totalClientTime += metrics.EndToEndTime - (backendTime + transitTime);
                    totalBackendTime += backendTime;
                    totalEndToEndTime += metrics.EndToEndTime;
                    totalTransitTime += transitTime;
                    totalRU += metrics.RUCharge;
                    query = queryStatistics.Query;
                    ode = queryStatistics.EnableOde;
                }

                textWriter.WriteLine($"{query},{ode},{totalRU},{totalBackendTime},{totalTransitTime},{totalClientTime},{totalEndToEndTime}");
            }
        }

        private static void SerializeODEProcessedDataQueryMetrics(TextWriter textWriter, List<List<OdeQueryStatistics>> globalOdeQueryStatisticsList, int numberOfIterations)
        {
            textWriter.WriteLine();
            textWriter.WriteLine(PrintQueryMetrics);
            textWriter.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\"", "Query", "ODE", "RUCharge", "EndToEndTime");
            textWriter.WriteLine();

            string prevQuery = globalOdeQueryStatisticsList[0][0].Query;
            bool prevOde = globalOdeQueryStatisticsList[0][0].EnableOde;
            double totalEndToEndTime = 0;
            double totalRU = 0;

            foreach (List<OdeQueryStatistics> odeQueryStatisticsList in globalOdeQueryStatisticsList)
            {
                if (odeQueryStatisticsList[0].Query == prevQuery && odeQueryStatisticsList[0].EnableOde == prevOde)
                {
                    foreach (OdeQueryStatistics odeQueryStatistics in odeQueryStatisticsList)
                    {
                        QueryStatisticsMetrics metrics = odeQueryStatistics.QueryStatisticsMetrics;
                        totalEndToEndTime += metrics.EndToEndTime;
                        totalRU += metrics.RUCharge;
                    }
                }
                else
                {
                    textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU / numberOfIterations},{totalEndToEndTime / numberOfIterations}");

                    foreach (OdeQueryStatistics odeQueryStatistics in odeQueryStatisticsList)
                    {
                        QueryStatisticsMetrics metrics = odeQueryStatistics.QueryStatisticsMetrics;
                        totalEndToEndTime = metrics.EndToEndTime;
                        totalRU = metrics.RUCharge;
                        prevQuery = odeQueryStatistics.Query;
                        prevOde = odeQueryStatistics.EnableOde;
                    }
                }
            }

            textWriter.WriteLine($"{prevQuery},{prevOde},{totalRU / numberOfIterations},{totalEndToEndTime / numberOfIterations}");
        }

        private class OdeQueryStatistics
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