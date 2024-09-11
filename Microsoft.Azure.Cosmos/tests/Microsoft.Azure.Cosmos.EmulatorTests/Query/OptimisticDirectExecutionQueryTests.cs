namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class OptimisticDirectExecutionQueryTests : QueryTestsBase
    {
        private const int NumberOfDocuments = 8;
        private const string PartitionKeyField = "key";
        private const string NumberField = "numberField";
        private const string NullField = "nullField";
        private const string ClientDisableOptimisticDirectExecution = "clientDisableOptimisticDirectExecution";

        private static class PageSizeOptions
        {
            public static readonly int[] NonGroupByAndNoContinuationTokenPageSizeOptions = { -1, 10 };
            public static readonly int[] NonGroupByWithContinuationTokenPageSizeOptions = { 1, 2 };
            public static readonly int[] GroupByPageSizeOptions = { -1 };
            public static readonly int[] PageSize100 = { 100 };
        }

        [TestMethod]
        public async Task TestPassingOptimisticDirectExecutionQueries()
        {
            IReadOnlyList<int> empty = new List<int>(0);
            IReadOnlyList<int> first5Integers = Enumerable.Range(0, 5).ToList();
            IReadOnlyList<int> first7Integers = Enumerable.Range(0, NumberOfDocuments).ToList();
            IReadOnlyList<int> first7IntegersReversed = Enumerable.Range(0, NumberOfDocuments).Reverse().ToList();

            PartitionKey partitionKeyValue = new PartitionKey("/value");
            List<DirectExecutionTestCase> singlePartitionContainerTestCases = new List<DirectExecutionTestCase>()
            {
                // Tests for bool enableOptimisticDirectExecution
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{PartitionKeyField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{PartitionKeyField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: false,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),

                // Simple query (requiresDist = false)
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                
                // DISTINCT with ORDER BY (requiresDist = true)
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
             
                // DISTINCT (requiresDist = true)
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                // TOP with GROUP BY (requiresDist = true)
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r GROUP BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r GROUP BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                // TOP (requiresDist = false)
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                // TOP with ORDER BY (requiresDist = false)
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                // OFFSET LIMIT with WHERE and BETWEEN (requiresDist = false)
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: partitionKeyValue,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    enableOptimisticDirectExecution: true,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: partitionKeyValue,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    enableOptimisticDirectExecution: true,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: null,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    enableOptimisticDirectExecution: true,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: null,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    enableOptimisticDirectExecution: true,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution)
            };

            List<DirectExecutionTestCase> multiPartitionContainerTestCases = new List<DirectExecutionTestCase>()
            {
                // Simple query (requiresDist = false)
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),

                // DISTINCT with ORDER BY (requiresDist = true)
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT DISTINCT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first7IntegersReversed,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                // TOP (requiresDist = false)
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                // TOP with ORDER BY (requiresDist = false)
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                // OFFSET LIMIT with WHERE and BETWEEN (requiresDist = false)
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized)
            };

            IReadOnlyList<string> documents = CreateDocuments(NumberOfDocuments, PartitionKeyField, NumberField, NullField);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition,
                documents,
                (container, documents) => RunTests(singlePartitionContainerTestCases, container),
                "/" + PartitionKeyField);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                documents,
                (container, documents) => RunTests(multiPartitionContainerTestCases, container),
                "/" + PartitionKeyField);
        }

        [TestMethod]
        public async Task TestQueriesWithPartitionKeyNone()
        {
            int documentCount = 400;
            IReadOnlyList<int> first400Integers = Enumerable.Range(0, documentCount).ToList();
            IReadOnlyList<int> first400IntegersReversed = Enumerable.Range(0, documentCount).Reverse().ToList();

            IReadOnlyList<DirectExecutionTestCase> testCases = new List<DirectExecutionTestCase>
            {
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: false,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} ASC",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: false,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first400IntegersReversed,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: false,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: false,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} ASC",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first400IntegersReversed,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r WHERE r.{NumberField} BETWEEN 0 AND {NumberOfDocuments} OFFSET 1 LIMIT 1",
                    expectedResult: new List<int> { 1 },
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
            };

            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                string document = $@"{{ {NumberField}: {i}, {NullField}: null }}";
                documents.Add(document);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.NonPartitioned | CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                (container, documents) => RunTests(testCases, container),
                "/undefinedPartitionKey");
        }

        [TestMethod]
        public async Task TestFailingOptimisticDirectExecutionOutput()
        {
            IReadOnlyList<string> documents = CreateDocuments(NumberOfDocuments, PartitionKeyField, NumberField, NullField);

            // check if bad continuation queries and syntax error queries are handled by pipeline
            IDictionary<string, string> invalidQueries = new Dictionary<string, string>
            {
                { "SELECT * FROM t", Guid.NewGuid().ToString() },
                { "SELECT TOP 10 * FOM r", null },
                { "this is not a valid query", null },
            };

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                (container, documents) => RunFailingTests(container, invalidQueries),
                "/" + PartitionKeyField);
        }

        //TODO: Remove Ignore flag once emulator is updated to 1101
        [Ignore]
        [TestMethod]
        public async Task TestClientDisableOdeDefaultValue()
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];

            CosmosClient client = new CosmosClient($"AccountEndpoint={endpoint};AccountKey={authKey}");
            AccountProperties properties = await client.ReadAccountAsync();

            bool success = bool.TryParse(properties.QueryEngineConfiguration[ClientDisableOptimisticDirectExecution].ToString(), out bool clientDisablOde);
            Assert.IsTrue(success, $"Parsing must succeed. Value supplied '{ClientDisableOptimisticDirectExecution}'");
            Assert.IsFalse(clientDisablOde);
        }

        [TestMethod]
        public async Task TestKnownIssues()
        {
            // This query is known to cause high level of nesting in distribution plan in the backend.
            // The level of nesting causes backend to fail with an exception.
            // Test should fail with ClientCompatibilityLevel = 1 against production account (until backend contains a fix for this case).
            // Test should pass with ClientCompatibilityLevel = 0.
            string query = @"SELECT DISTINCT VALUE p1.a1 FROM p1 WHERE
                (p1.a98=p1.b90 AND p1.a74=p1.b81 AND p1.a74=p1.b5 AND p1.a2=p1.b15 AND p1.a20=p1.b63 AND p1.a91=p1.b50 AND p1.a19=p1.b46 AND 
                p1.a8=p1.b84 AND p1.a57=p1.b26 AND p1.a1=p1.b94 AND p1.a16=p1.b3 AND p1.a78=p1.b1 AND p1.a75=p1.b64 AND p1.a68=p1.b90 AND 
                p1.a52=p1.b14 AND p1.a60=p1.b85 AND p1.a76=p1.b8 AND p1.a59=p1.b10 AND p1.a91=p1.b21 AND p1.a41=p1.b79 AND p1.a93=p1.b88 AND 
                p1.a49=p1.b20 AND p1.a75=p1.b12 AND p1.a19=p1.b39 AND p1.a17=p1.b48 AND p1.a70=p1.b16 AND p1.a2=p1.b55 AND p1.a82=p1.b96 AND 
                p1.a13=p1.b74 AND p1.a6=p1.b10 AND p1.a36=p1.b12 AND p1.a63=p1.b6 AND p1.a4=p1.b6 AND p1.a73=p1.b12 AND p1.a87=p1.b98 AND 
                p1.a92=p1.b36 AND p1.a84=p1.b21 AND p1.a1=p1.b27 AND p1.a53=p1.b59 AND p1.a25=p1.b64 AND p1.a45=p1.b30 AND p1.a73=p1.b5 AND 
                p1.a44=p1.b44 AND p1.a84=p1.b21 AND p1.a25=p1.b63 AND p1.a96=p1.b18 AND p1.a15=p1.b31 AND p1.a43=p1.b81 AND p1.a26=p1.b44 AND 
                p1.a16=p1.b70 AND p1.a38=p1.b7 AND p1.a51=p1.b18 AND p1.a55=p1.b34 AND p1.a31=p1.b80 AND p1.a54=p1.b55 AND p1.a43=p1.b54 AND 
                p1.a50=p1.b42 AND p1.a65=p1.b7 AND p1.a38=p1.b58 AND p1.a61=p1.b59 AND p1.a22=p1.b52 AND p1.a86=p1.b24 AND p1.a2=p1.b75 AND 
                p1.a22=p1.b54 AND p1.a77=p1.b20 AND p1.a2=p1.b10 AND p1.a43=p1.b54 AND p1.a27=p1.b39 AND p1.a78=p1.b56 AND p1.a49=p1.b11 AND 
                p1.a14=p1.b4 AND p1.a67=p1.b70 AND p1.a21=p1.b42 AND p1.a68=p1.b73 AND p1.a66=p1.b37 AND p1.a43=p1.b67 AND p1.a82=p1.b56 AND 
                p1.a48=p1.b85 AND p1.a20=p1.b28 AND p1.a16=p1.b79 AND p1.a13=p1.b76 AND p1.a3=p1.b34 AND p1.a54=p1.b34 AND p1.a12=p1.b95 AND 
                p1.a15=p1.b26 AND p1.a28=p1.b82 AND p1.a10=p1.b51 AND p1.a46=p1.b18 AND p1.a85=p1.b17 AND p1.a4=p1.b60 AND p1.a8=p1.b48 AND 
                p1.a88=p1.b40 AND p1.a76=p1.b34 AND p1.a27=p1.b86 AND p1.a7=p1.b41 AND p1.a19=p1.b51 AND p1.a40=p1.b70 AND p1.a97=p1.b37 AND 
                p1.a2=p1.b33 AND p1.a16=p1.b86 AND p1.a31=p1.b73 AND p1.a58=p1.b40 AND p1.a10=p1.b61 AND p1.a58=p1.b31 AND p1.a11=p1.b31 AND 
                p1.a1=p1.b3 AND p1.a25=p1.b56 AND p1.a72=p1.b64 AND p1.a88=p1.b62 AND p1.a58=p1.b21 AND p1.a7=p1.b25 AND p1.a89=p1.b74 AND 
                p1.a8=p1.b76 AND p1.a42=p1.b39 AND p1.a54=p1.b9 AND p1.a17=p1.b52 AND p1.a2=p1.b17 AND p1.a29=p1.b35 AND p1.a90=p1.b49 AND 
                p1.a31=p1.b16) ORDER BY p1.a1";

            int documentCount = 400;
            IReadOnlyList<int> first400Integers = Enumerable.Range(0, documentCount).ToList();

            IReadOnlyList<DirectExecutionTestCase> testCases = new List<DirectExecutionTestCase>
            {
                CreateInput(
                    query,
                    expectedResult: new List<int>(),
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution)
            };

            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                string document = $@"{{ {NumberField}: {i}, {NullField}: null }}";
                documents.Add(document);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition,
                documents,
                (container, documents) => RunTests(testCases, container),
                "/undefinedPartitionKey");
        }

        [TestMethod]
        public async Task TestOdeEnvironmentVariable()
        {
            bool defaultValue = false;
            QueryRequestOptions options = new QueryRequestOptions();
            Assert.AreEqual(defaultValue, options.EnableOptimisticDirectExecution);

            foreach ((string name, string value, bool expectedValue) in new[]
                {
                    // Environment variables are case insensitive in windows
                    ("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", "true", true),
                    ("AZURE_COSMOS_optimistic_direct_execution_enabled", "True", true),
                    ("azure_cosmos_optimistic_direct_execution_enabled", "TRUE", true),
                    ("Azure_Cosmos_Optimistic_Direct_Execution_Enabled", "truE", true),
                    ("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", "false", false),
                    ("AZURE_COSMOS_optimistic_direct_execution_enabled", "False", false),
                    ("azure_cosmos_optimistic_direct_execution_enabled", "FALSE", false),
                    ("Azure_Cosmos_Optimistic_Direct_Execution_Enabled", "false", false),
                    ("Azure_Cosmos_Optimistic_Direct_Execution_Enabled", string.Empty, defaultValue),
                    (nameof(QueryRequestOptions.EnableOptimisticDirectExecution), "false", defaultValue),
                    (nameof(QueryRequestOptions.EnableOptimisticDirectExecution), null, defaultValue),
                    ("enableode", "false", defaultValue)
                })
            {
                try
                {
                    // Test new value
                    Environment.SetEnvironmentVariable(name, value);
                    QueryRequestOptions options2 = new QueryRequestOptions();
                    bool areEqual = expectedValue == options2.EnableOptimisticDirectExecution;
                    Assert.IsTrue(areEqual, $"EnvironmentVariable:'{name}', value:'{value}', expected:'{expectedValue}', actual:'{options2.EnableOptimisticDirectExecution}'");
                }
                finally
                {
                    // Remove side effects.
                    Environment.SetEnvironmentVariable(name, null);
                }
            }

            foreach (string value in new[]
                {
                    "'",
                    "-",
                    "asdf",
                    "'true'",
                    "'false'"
                })
            {
                bool receivedException = false;
                try
                {
                    // Test new value
                    Environment.SetEnvironmentVariable("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", value);
                    QueryRequestOptions options2 = new QueryRequestOptions();
                }
                catch(FormatException fe)
                {
                    Assert.IsTrue(fe.ToString().Contains($@"String '{value}' was not recognized as a valid Boolean."));
                    receivedException = true;
                }
                finally
                {
                    // Remove side effects.
                    Environment.SetEnvironmentVariable("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", null);
                }

                Assert.IsTrue(receivedException, $"Expected exception was not received for value '{value}'");
            }

            await this.TestQueryExecutionUsingODEEnvironmentVariable(
                environmentVariableValue: "false",
                expectODEPipeline: false);

            await this.TestQueryExecutionUsingODEEnvironmentVariable(
                environmentVariableValue: "true",
                expectODEPipeline: true);
        }

        private async Task TestQueryExecutionUsingODEEnvironmentVariable(string environmentVariableValue, bool expectODEPipeline)
        {
            IReadOnlyList<int> empty = new List<int>(0);
            IReadOnlyList<int> first5Integers = Enumerable.Range(0, 5).ToList();
            IReadOnlyList<int> first7Integers = Enumerable.Range(0, NumberOfDocuments).ToList();
            IReadOnlyList<int> first7IntegersReversed = Enumerable.Range(0, NumberOfDocuments).Reverse().ToList();

            try
            {
                // Test query execution using environment variable
                Environment.SetEnvironmentVariable("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", environmentVariableValue);
                PartitionKey partitionKeyValue = new PartitionKey("/value");
                List<DirectExecutionTestCase> singlePartitionContainerTestCases = new List<DirectExecutionTestCase>()
                    {
                        CreateInput(
                            query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{PartitionKeyField}",
                            expectedResult: first5Integers,
                            partitionKey: partitionKeyValue,
                            enableOptimisticDirectExecution: null,  // Uses environment variable
                            pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                            expectedPipelineType: expectODEPipeline ? TestInjections.PipelineType.OptimisticDirectExecution : TestInjections.PipelineType.Passthrough),
                        CreateInput(
                            query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{PartitionKeyField}",
                            expectedResult: first5Integers,
                            partitionKey: partitionKeyValue,
                            enableOptimisticDirectExecution: false,  // Overrides environment variable
                            pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                            expectedPipelineType: TestInjections.PipelineType.Passthrough),
                        CreateInput(
                            query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{PartitionKeyField}",
                            expectedResult: first5Integers,
                            partitionKey: partitionKeyValue,
                            enableOptimisticDirectExecution: true,  // Overrides environment variable
                            pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                            expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                    };

                IReadOnlyList<string> documents = CreateDocuments(NumberOfDocuments, PartitionKeyField, NumberField, NullField);

                await this.CreateIngestQueryDeleteAsync(
                    ConnectionModes.Direct | ConnectionModes.Gateway,
                    CollectionTypes.SinglePartition,
                    documents,
                    (container, documents) => RunTests(singlePartitionContainerTestCases, container),
                    "/" + PartitionKeyField);
            }
            finally
            {
                // Attempt to protect other ODE tests from side-effects in case of test failure.
                Environment.SetEnvironmentVariable("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", null);
            }
        }

        private static async Task RunTests(IEnumerable<DirectExecutionTestCase> testCases, Container container)
        {
            foreach (DirectExecutionTestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizeOptions)
                {
                    QueryRequestOptions feedOptions = new QueryRequestOptions
                    {
                        MaxItemCount = pageSize,
                        PartitionKey = testCase.PartitionKey,
                        TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
                    };

                    if(testCase.EnableOptimisticDirectExecution.HasValue)
                    {
                        feedOptions.EnableOptimisticDirectExecution = testCase.EnableOptimisticDirectExecution.Value;
                    }

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            testCase.Query,
                            feedOptions);

                    int[] actual = items.Cast<CosmosNumber>().Select(x => (int)Number64.ToLong(x.Value)).ToArray();

                    Assert.IsTrue(testCase.ExpectedResult.SequenceEqual(actual));
                    Assert.AreEqual(testCase.ExpectedPipelineType, feedOptions.TestSettings.Stats.PipelineType.Value);
                }
            }
        }

        private static async Task RunFailingTests(Container container, IDictionary<string, string> invalidQueries)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                EnableOptimisticDirectExecution = true,
                TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
            };

            foreach (KeyValuePair<string, string> queryAndResult in invalidQueries)
            {
                try
                {
                    await container.GetItemQueryIterator<string>(
                        queryDefinition: new QueryDefinition(queryAndResult.Key),
                        continuationToken: queryAndResult.Value,
                        requestOptions: feedOptions).ReadNextAsync();

                    Assert.Fail("Expect exception");
                }
                catch (CosmosException dce)
                {
                    Assert.IsTrue(dce.StatusCode == HttpStatusCode.BadRequest);
                }
                catch (AggregateException aggrEx)
                {
                    Assert.Fail(aggrEx.ToString());
                }
            }
        }

        private static IReadOnlyList<string> CreateDocuments(int documentCount, string partitionKey, string numberField, string nullField)
        {
            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                string document = $@"{{ {partitionKey}: ""/value"", {numberField}: {i}, {nullField}: null }}";
                documents.Add(document);
            }

            return documents;
        }

        private static DirectExecutionTestCase CreateInput(
            string query,
            IReadOnlyList<int> expectedResult,
            PartitionKey? partitionKey,
            bool? enableOptimisticDirectExecution,
            int[] pageSizeOptions,
            TestInjections.PipelineType expectedPipelineType)
        {
            return new DirectExecutionTestCase(query, expectedResult, partitionKey, enableOptimisticDirectExecution, pageSizeOptions, expectedPipelineType);
        }

        private readonly struct DirectExecutionTestCase
        {
            public string Query { get; }
            public IReadOnlyList<int> ExpectedResult { get; }
            public PartitionKey? PartitionKey { get; }
            public bool? EnableOptimisticDirectExecution { get; }
            public int[] PageSizeOptions { get; }
            public TestInjections.PipelineType ExpectedPipelineType { get; }

            public DirectExecutionTestCase(
                string query,
                IReadOnlyList<int> expectedResult,
                PartitionKey? partitionKey,
                bool? enableOptimisticDirectExecution,
                int[] pageSizeOptions,
                TestInjections.PipelineType expectedPipelineType)
            {
                this.Query = query;
                this.ExpectedResult = expectedResult;
                this.PartitionKey = partitionKey;
                this.EnableOptimisticDirectExecution = enableOptimisticDirectExecution;
                this.PageSizeOptions = pageSizeOptions;
                this.ExpectedPipelineType = expectedPipelineType;
            }
        }
    }
}
