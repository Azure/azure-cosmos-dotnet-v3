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
        private const string TransportKeyValue = "Client Side Request Stats";
        private const string TransportNodeName = "Microsoft.Azure.Documents.ServerStoreModel Transport Request";
        private readonly string RawDataPath = Path.GetFullPath(RawDataFileName);
        private readonly string AggregateDataPath = Path.GetFullPath(DiagnosticsDataFileName);

        private readonly QueryStatisticsDatumVisitor queryStatisticsDatumVisitor;
        private readonly string endpoint;
        private readonly string authKey;
        private readonly string cosmosDatabaseId;
        private readonly string containerId;
        private readonly PartitionKey partitionKeyValue;
        private readonly int numberOfIterations;
        private readonly int warmupIterations;
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;

        public OptimisticDirectExecutionPerformanceTests()
        {
            this.queryStatisticsDatumVisitor = new();
            this.endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
            this.authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            this.cosmosDatabaseId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.CosmosDatabaseId"];
            this.containerId = Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContainerId"];
            this.numberOfIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.NumberOfIterations"]);
            this.warmupIterations = int.Parse(Utils.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.WarmupIterations"]);
            this.partitionKeyValue = new PartitionKey("Andersen");
        }

        [TestMethod]
        public async Task PerformanceTestSetup()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
            };

            this.cosmosClient = new CosmosClient(this.endpoint, this.authKey, clientOptions);
            //await this.CreateDatabaseAsync();
            //await this.CreateContainerAsync();
            //await this.AddItemsToContainerAsync();
            await this.RunAsync();
        }

        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(this.cosmosDatabaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }

        private async Task CreateContainerAsync()
        {
            this.container = await this.database.CreateContainerIfNotExistsAsync(this.containerId, partitionKeyPath: "/name");
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }

        private async Task AddItemsToContainerAsync()
        {
            Random random = new Random();
            string[] cityOptions = new string[] { "Seattle", "Chicago", "NYC", "SF" };

            // Create a family object for the Andersen family
            foreach (int i in Enumerable.Range(0, 5000))
            {
                int numberOfRecipeints = random.Next(1, 4);
                List<RecipientList> recipientList = new List<RecipientList>();

                for (int j = 0; j < numberOfRecipeints; j++)
                {
                    RecipientList store = new RecipientList()
                    {
                        Name = "John",
                        City = cityOptions[random.Next(0, cityOptions.Length)],
                    };

                    recipientList.Add(store);
                }

                States andersenFamily = new States
                {
                    Id = i.ToString(),
                    Name = "Andersen",
                    City = cityOptions[random.Next(0, cityOptions.Length)],
                    PostalCode = random.Next(0, 1000).ToString(),
                    Region = "Northwest",
                    UserDefinedID = i % 10,
                    RecipientList = recipientList
                };

                try
                {
                    // Read the item to see if it exists.
                    ItemResponse<States> andersenFamilyResponse = await this.container.ReadItemAsync<States>(andersenFamily.Id, new PartitionKey("Andersen"));
                    Console.WriteLine("Item in database with id: {0} already exists\n", andersenFamilyResponse.Resource.Id);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                    ItemResponse<States> andersenFamilyResponse = await this.container.CreateItemAsync<States>(andersenFamily, new PartitionKey("Andersen"));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", andersenFamilyResponse.Resource.Id, andersenFamilyResponse.RequestCharge);
                }

            }
        }
        
        private async Task RunAsync()
        {
            MetricsSerializer metricsSerializer = new MetricsSerializer();
            this.database = this.cosmosClient.GetDatabase(this.cosmosDatabaseId);
            this.container = this.database.GetContainer(this.containerId);
            string highPrepTimeSumQuery = this.CreateHighPrepTimeSumQuery();
            string highPrepTimeConditionalQuery = this.CreateHighPrepTimeConditionalQuery();

            Console.WriteLine(this.RawDataPath);

            if (File.Exists(this.RawDataPath))
            {
                File.Delete(this.RawDataPath);
            } 
            
            if(File.Exists(this.AggregateDataPath)) 
            {
                File.Delete(this.AggregateDataPath);
            }

            List<DirectExecutionTestCase> odeTestCases = new List<DirectExecutionTestCase>()
            {
                //Simple Query
                CreateInput("SELECT * FROM c", this.partitionKeyValue, false, -1, 5000),
                CreateInput("SELECT * FROM c", this.partitionKeyValue, true, -1, 5000),

                //TOP
                CreateInput("SELECT TOP 1000 c.id FROM c", this.partitionKeyValue, false, -1, 1000),
                CreateInput("SELECT TOP 1000 c.id FROM c", this.partitionKeyValue, true, -1, 1000),
                
                //Filter
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", this.partitionKeyValue, false, -1, 2557),
                CreateInput("SELECT c.id FROM c WHERE c.city IN ('Seattle', 'NYC')", this.partitionKeyValue, true, -1, 2557),
                
                //DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", this.partitionKeyValue, false, -1, 3),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 OFFSET 1 LIMIT 3", this.partitionKeyValue, true, -1, 3),
                
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", this.partitionKeyValue, false, -1, 2),
                CreateInput("SELECT DISTINCT c.city FROM c WHERE STARTSWITH(c.city, 'S')", this.partitionKeyValue, true, -1, 2),

                //JOIN
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", this.partitionKeyValue, false, -1, 1),
                CreateInput("SELECT root.id " +
                "FROM root " +
                "JOIN root.id a " +
                "JOIN root.id b " +
                "JOIN root.id c " +
                "WHERE root.id = '1' OR a.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR b.id in (1,2,3,4,5,6,7,8,9,10) " +
                "OR c.id in (1,2,3,4,5,6,7,8,9,10)", this.partitionKeyValue, true, -1, 1),

                //High Prep Time
                CreateInput(highPrepTimeSumQuery, this.partitionKeyValue, false, -1, 5000),
                CreateInput(highPrepTimeSumQuery, this.partitionKeyValue, true, -1, 5000),

                CreateInput(highPrepTimeConditionalQuery, this.partitionKeyValue, false, -1, 3770),
                CreateInput(highPrepTimeConditionalQuery, this.partitionKeyValue, true, -1, 3770),

                //Order By
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", this.partitionKeyValue, false, -1, 5000),
                CreateInput("SELECT * FROM c ORDER BY c.userDefinedId DESC", this.partitionKeyValue, true, -1, 5000),

                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", this.partitionKeyValue, false, -1, 5000),
                CreateInput("SELECT c.id FROM c ORDER BY c.postalcode DESC", this.partitionKeyValue, true, -1, 5000),

                //Order By + TOP
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", this.partitionKeyValue, false, -1, 5),
                CreateInput("SELECT TOP 5 c.id FROM c ORDER BY c.userDefinedId", this.partitionKeyValue, true, -1, 5),

                //Order By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", this.partitionKeyValue, false, -1, 5000),
                CreateInput("SELECT DISTINCT c.id FROM c ORDER BY c.city DESC", this.partitionKeyValue, true, -1, 5000),

                //Order By + DISTINCT + Filter
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", this.partitionKeyValue, false, -1, 4),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId > 5 ORDER BY c.userDefinedId", this.partitionKeyValue, true, -1, 4),

                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", this.partitionKeyValue, false, -1, 6),
                CreateInput("SELECT DISTINCT c.userDefinedId FROM c WHERE c.userDefinedId BETWEEN 0 AND 5 ORDER BY c.id DESC", this.partitionKeyValue, true, -1, 6),

                //Group By
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", this.partitionKeyValue, false, -1, 996),
                CreateInput("SELECT c.postalcode FROM c GROUP BY c.postalcode", this.partitionKeyValue, true, -1, 996),

                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", this.partitionKeyValue, false, -1, 2),
                CreateInput("SELECT Count(1) AS count, Sum(ARRAY_LENGTH(c.recipientList)) AS sum FROM c WHERE c.city IN ('Seattle', 'SF') GROUP BY c.city", this.partitionKeyValue, true, -1, 2),

                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", this.partitionKeyValue, false, -1, 4),
                CreateInput("SELECT c.city, AVG(ARRAY_LENGTH(c.recipientList)) FROM c GROUP BY c.city", this.partitionKeyValue, true, -1, 4),

                //Group By + OFFSET
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", this.partitionKeyValue, false, -1, 3),
                CreateInput("SELECT c.id FROM c GROUP BY c.id OFFSET 5 LIMIT 3", this.partitionKeyValue, true, -1, 3),

                //Group By + TOP
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", this.partitionKeyValue, false, -1, 25),
                CreateInput("SELECT TOP 25 c.id FROM c GROUP BY c.id", this.partitionKeyValue, true, -1, 25),

                //Group By + DISTINCT
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", this.partitionKeyValue, false, -1, 5000),
                CreateInput("SELECT DISTINCT c.id FROM c GROUP BY c.id", this.partitionKeyValue, true, -1, 5000),

                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", this.partitionKeyValue, false, -1, 996),
                CreateInput("SELECT DISTINCT c.postalcode FROM c GROUP BY c.postalcode", this.partitionKeyValue, true, -1, 996),
            };

            foreach (DirectExecutionTestCase testCase in odeTestCases)
            {
                await this.RunQueryAsync(this.container, testCase);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(this.RawDataPath, FileMode.Append, FileAccess.Write)))
            {
                metricsSerializer.ODESerialization(writer, this.queryStatisticsDatumVisitor, this.numberOfIterations, true);
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(this.AggregateDataPath, FileMode.Append, FileAccess.Write)))
            {
                metricsSerializer.ODESerialization(writer, this.queryStatisticsDatumVisitor, this.numberOfIterations, false);
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

            for (int i = 0; i < this.numberOfIterations; i++)
            {
                bool isWarmUpIteration = false;
                if (i < this.warmupIterations)
                {
                    isWarmUpIteration = true;
                }
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                        queryText: queryInput.Query,
                        requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(iterator, queryInput, isWarmUpIteration);
                }
            }
        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator, DirectExecutionTestCase queryInput, bool isWarmUpIteration)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Documents.ValueStopwatch totalTime = new Documents.ValueStopwatch();
            Documents.ValueStopwatch getTraceTime = new Documents.ValueStopwatch();
            Guid correlatedActivityId = Guid.NewGuid();
            FeedResponse<T> response;
            int totalDocumentCount = 0;

            while (feedIterator.HasMoreResults)
            {
                if (!isWarmUpIteration)
                {
                    totalTime.Start();
                    response = await feedIterator.ReadNextAsync();
                    getTraceTime.Start();
                    if (response.RequestCharge != 0)
                    {
                        metricsAccumulator.ReadFromTrace(response, this.queryStatisticsDatumVisitor);
                    }

                    getTraceTime.Stop();
                    totalTime.Stop();
                    if (response.RequestCharge != 0)
                    {
                        this.queryStatisticsDatumVisitor.AddQuery(queryInput.Query);
                        this.queryStatisticsDatumVisitor.AddEnableOdeFlag(queryInput.EnableOptimisticDirectExecution);
                        this.queryStatisticsDatumVisitor.AddCorrelatedActivityId(correlatedActivityId);
                        this.queryStatisticsDatumVisitor.AddRuCharge(response.RequestCharge);
                        this.queryStatisticsDatumVisitor.AddEndToEndTime(totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds);
                        this.queryStatisticsDatumVisitor.PopulateMetrics();
                    }

                    totalTime.Reset();
                    getTraceTime.Reset();
                }
                else
                {
                    response = await feedIterator.ReadNextAsync();
                }

                totalDocumentCount += response.Count;
            }

            Assert.AreEqual(queryInput.ExpectedResultCount, totalDocumentCount);
        }

        private string CreateHighPrepTimeSumQuery()
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

        private string CreateHighPrepTimeConditionalQuery()
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

                sb.Append("(r.userDefinedId > " + nIdx + " AND r.city = " + "'" + cityOptions[nIdx % cityOptions.Length] + "')");
            }

            return sb.ToString();
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