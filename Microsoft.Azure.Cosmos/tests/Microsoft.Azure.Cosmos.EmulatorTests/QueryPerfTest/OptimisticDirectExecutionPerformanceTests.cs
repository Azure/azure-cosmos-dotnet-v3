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
        private const string RawDataFileName = "OptimisticDirectExecutionPerformanceTestsRawData.csv";
        private const string DiagnosticsDataFileName = "OptimisticDirectExecutionPerformanceTestsAggregatedData.csv";
        private readonly string RawDataPath = Path.GetFullPath(RawDataFileName);
        private readonly string AggregateDataPath = Path.GetFullPath(DiagnosticsDataFileName);
        private static readonly QueryStatisticsDatumVisitor queryStatisticsDatumVisitor = new QueryStatisticsDatumVisitor();
        private static readonly string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
        private static readonly string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
        private static readonly string cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.CosmosDatabaseId"];
        private static readonly string containerId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContainerId"];
        private static readonly PartitionKey partitionKeyValue = new PartitionKey("Andersen");
        private static readonly int numberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.NumberOfIterations"]);
        private static readonly int warmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.WarmupIterations"]);

        [TestMethod]
        public async Task OptimisticDirectExecutionPerformanceTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            CosmosClient cosmosClient = new CosmosClient(endpoint, authKey, clientOptions);
            (Database database, Container container) = await this.TestInitialize(cosmosClient);
            await this.RunAsync(container);
            await this.TestCleanup(database);
        }

        private async Task<(Database, Container)> TestInitialize(CosmosClient cosmosClient)
        {
            Database database = await this.CreateDatabaseAsync(cosmosClient);
            Container container = await this.CreateContainerAsync(database);
            await this.AddItemsToContainerAsync(container);

            if (File.Exists(this.RawDataPath))
            {
                File.Delete(this.RawDataPath);
            }

            if (File.Exists(this.AggregateDataPath))
            {
                File.Delete(this.AggregateDataPath);
            }

            return (database, container);
        }

        private async Task<Database> CreateDatabaseAsync(CosmosClient cosmosClient)
        {
            // Create a new database
            return await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabaseId);
        }

        private async Task<Container> CreateContainerAsync(Database database)
        {
            return await database.CreateContainerIfNotExistsAsync(containerId, partitionKeyPath: "/name");
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

                try
                {
                    // Read the item to see if it exists.
                    ItemResponse<States> andersenFamilyResponse = await container.ReadItemAsync<States>(andersenFamily.Id, new PartitionKey(andersenFamily.Name));
                    Console.WriteLine("Item in database with id: {0} already exists\n", andersenFamilyResponse.Resource.Id);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                    ItemResponse<States> andersenFamilyResponse = await container.CreateItemAsync<States>(andersenFamily, new PartitionKey(andersenFamily.Name));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", andersenFamilyResponse.Resource.Id, andersenFamilyResponse.RequestCharge);
                }
            }
        }
        
        private async Task RunAsync(Container container)
        {
            MetricsSerializer metricsSerializer = new MetricsSerializer();
            string highPrepTimeSumQuery = CreateHighPrepTimeSumQuery();
            string highPrepTimeConditionalQuery = CreateHighPrepTimeConditionalQuery();

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
                await this.RunQueryAsync(container, testCase);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(this.RawDataPath, FileMode.Append, FileAccess.Write)))
            {
                metricsSerializer.OdeSerialization(writer, queryStatisticsDatumVisitor, numberOfIterations, rawData: true);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(this.AggregateDataPath, FileMode.Append, FileAccess.Write)))
            {
                metricsSerializer.OdeSerialization(writer, queryStatisticsDatumVisitor, numberOfIterations, rawData: false);
            }
        }

        private async Task RunQueryAsync(Container container, DirectExecutionTestCase queryInput)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = queryInput.PageSizeOption,
                EnableOptimisticDirectExecution = queryInput.EnableOptimisticDirectExecution,
                PartitionKey = queryInput.PartitionKey,
            };

            for (int i = 0; i < numberOfIterations + warmupIterations; i++)
            {
                bool isWarmUpIteration = i < warmupIterations;
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
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
                        await this.GetIteratorResponse(iterator, queryInput);
                    }
                    
                }
            }
        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator, DirectExecutionTestCase queryInput)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Documents.ValueStopwatch totalTime = new Documents.ValueStopwatch();
            Documents.ValueStopwatch getTraceTime = new Documents.ValueStopwatch();
            Guid correlatedActivityId = Guid.NewGuid();
            FeedResponse<T> response;
            int totalDocumentCount = 0;

            while (feedIterator.HasMoreResults)
            {
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
                    queryStatisticsDatumVisitor.AddQuery(queryInput.Query);
                    queryStatisticsDatumVisitor.AddEnableOdeFlag(queryInput.EnableOptimisticDirectExecution);
                    queryStatisticsDatumVisitor.AddCorrelatedActivityId(correlatedActivityId);
                    queryStatisticsDatumVisitor.AddRuCharge(response.RequestCharge);
                    queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds);
                    queryStatisticsDatumVisitor.PopulateMetrics();
                }

                totalTime.Reset();
                getTraceTime.Reset();

                totalDocumentCount += response.Count;
            }

            Assert.AreEqual(queryInput.ExpectedResultCount, totalDocumentCount);
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

        private async Task TestCleanup(Database database)
        {
            await database.DeleteAsync();
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