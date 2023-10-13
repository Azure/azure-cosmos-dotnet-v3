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
        private const string AllowOptimisticDirectExecution = "allowOptimisticDirectExecution";

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
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),
                
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

                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
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
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: null,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
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
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first7Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

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

                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

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
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT TOP 5 VALUE r.{NumberField} FROM r ORDER BY r.{NumberField}",
                    expectedResult: first5Integers,
                    partitionKey: partitionKeyValue,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.NonGroupByWithContinuationTokenPageSizeOptions,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

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
                
                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.numberField FROM r",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} ASC",
                    expectedResult: first400Integers,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

                //TODO: Change expectedPipelineType to OptimisticDirectExecution once emulator is updated to 0415
                CreateInput(
                    query: $"SELECT VALUE r.{NumberField} FROM r ORDER BY r.{NumberField} DESC",
                    expectedResult: first400IntegersReversed,
                    partitionKey: PartitionKey.None,
                    enableOptimisticDirectExecution: true,
                    pageSizeOptions: PageSizeOptions.PageSize100,
                    expectedPipelineType: TestInjections.PipelineType.Specialized),

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

        [TestMethod]
        public async Task TestAllowOdeFlagInCosmosClient()
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];

            CosmosClient client = new CosmosClient($"AccountEndpoint={endpoint};AccountKey={authKey}");
            AccountProperties properties = await client.ReadAccountAsync();

            Assert.IsTrue(properties.QueryEngineConfigurationString.Contains(AllowOptimisticDirectExecution));
            Assert.IsTrue(Convert.ToBoolean(properties.QueryEngineConfiguration[AllowOptimisticDirectExecution]));
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
                        EnableOptimisticDirectExecution = testCase.EnableOptimisticDirectExecution,
                        TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
                    };

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
            bool enableOptimisticDirectExecution,
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
            public bool EnableOptimisticDirectExecution { get; }
            public int[] PageSizeOptions { get; }
            public TestInjections.PipelineType ExpectedPipelineType { get; }

            public DirectExecutionTestCase(
                string query,
                IReadOnlyList<int> expectedResult,
                PartitionKey? partitionKey,
                bool enableOptimisticDirectExecution,
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
