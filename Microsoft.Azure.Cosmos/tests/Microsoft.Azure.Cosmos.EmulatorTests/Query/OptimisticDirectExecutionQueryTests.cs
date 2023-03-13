namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class OptimisticDirectExecutionQueryTests : QueryTestsBase
    {
        private static class PageSizeOptions
        {
            public static readonly int[] NonGroupByAndNoContinuationTokenPageSizeOptions = { -1, 10, 100 };
            public static readonly int[] NonGroupWithContinuationTokenPageSizeOptions = { 1, 2, 5 };
            public static readonly int[] GroupByPageSizeOptions = { -1 };
        }

        [TestMethod]
        public async Task TestPassingOptimisticDirectExecutionQueries()
        {
            int numberOfDocuments = 8;
            string partitionKey = "key";
            string numberField = "numberField";
            string nullField = "nullField";

            List<string> documents = CreateDocuments(numberOfDocuments, partitionKey, numberField, nullField);

            List<DirectExecutionTestCase> singlePartitionContainerTestCases = new List<DirectExecutionTestCase>()
            {
                // Tests for bool enableOptimisticDirectExecution
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: false, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: false, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Passthrough),

                // Simple query (requiresDist = false)
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                
                // DISTINCT with ORDER BY (requiresDist = true)
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),

                // TOP with GROUP BY (requiresDist = true)
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.GroupByPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),

                // OFFSET LIMIT with WHERE and BETWEEN (requiresDist = false)
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.SinglePartition, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.SinglePartition, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution)
            };
            
            List<DirectExecutionTestCase> multiPartitionContainerTestCases = new List<DirectExecutionTestCase>()
            {
                // Simple query (requiresDist = false)
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Passthrough),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Passthrough),

                // DISTINCT with ORDER BY (requiresDist = true)
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),

                // OFFSET LIMIT with WHERE and BETWEEN (requiresDist = false)
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupByAndNoContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, pageSizeOptions: PageSizeOptions.NonGroupWithContinuationTokenPageSizeOptions, expectedPipelineType: TestInjections.PipelineType.Specialized)
            };
            
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition,
                documents,
                (container, documents) => RunPassingTests(singlePartitionContainerTestCases, container),
                "/" + partitionKey);
            
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                documents,
                (container, documents) => RunPassingTests(multiPartitionContainerTestCases, container),
                "/" + partitionKey);
        }

        [TestMethod]
        public async Task TestFailingOptimisticDirectExecutionOutput()
        {
            int numberOfDocuments = 8;
            string partitionKey = "key";
            string numberField = "numberField";
            string nullField = "nullField";

            List<string> documents = CreateDocuments(numberOfDocuments, partitionKey, numberField, nullField);

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
                "/" + partitionKey);
        }

        private static async Task RunPassingTests(IEnumerable<DirectExecutionTestCase> testCases, Container container)
        {
            foreach (DirectExecutionTestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizeOptions)
                {
                    QueryRequestOptions feedOptions = new QueryRequestOptions
                    {
                        MaxItemCount = pageSize,
                        PartitionKey = testCase.PartitionKey == null
                            ? null
                            : new Cosmos.PartitionKey(testCase.PartitionKey),
                        EnableOptimisticDirectExecution = testCase.EnableOptimisticDirectExecution,
                        TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
                    };

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            testCase.Query,
                            feedOptions);

                    long[] actual = items.Cast<CosmosNumber>().Select(x => Number64.ToLong(x.Value)).ToArray();

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
                    await container.GetItemQueryIterator<Document>(
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

        private static List<string> CreateDocuments(int documentCount, string partitionKey, string numberField, string nullField)
        {
            List<string> documents = new List<string>(documentCount);
            for (int i = 0; i < documentCount; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, "/value");
                doc.SetPropertyValue(numberField, i % documentCount);
                doc.SetPropertyValue(nullField, null);
                documents.Add(doc.ToString());
            }

            return documents;
        }

        private static DirectExecutionTestCase CreateInput(
            string query,
            List<long> expectedResult,
            string partitionKey,
            CollectionTypes partition,
            bool enableOptimisticDirectExecution,
            int[] pageSizeOptions,
            TestInjections.PipelineType expectedPipelineType)
        {
            return new DirectExecutionTestCase(query, expectedResult, partitionKey, partition, enableOptimisticDirectExecution, pageSizeOptions, expectedPipelineType);
        }

        internal readonly struct DirectExecutionTestCase
        {
            public string Query { get; }
            public List<long> ExpectedResult { get; }
            public string PartitionKey { get; }
            public CollectionTypes Partition { get; }
            public bool EnableOptimisticDirectExecution { get; }
            public int[] PageSizeOptions { get; }
            public TestInjections.PipelineType ExpectedPipelineType { get; }

            public DirectExecutionTestCase(
                string query,
                List<long> expectedResult,
                string partitionKey,
                CollectionTypes partition,
                bool enableOptimisticDirectExecution,
                int[] pageSizeOptions,
                TestInjections.PipelineType expectedPipelineType)
            {
                this.Query = query;
                this.ExpectedResult = expectedResult;
                this.PartitionKey = partitionKey;
                this.Partition = partition;
                this.EnableOptimisticDirectExecution = enableOptimisticDirectExecution;
                this.PageSizeOptions = pageSizeOptions;
                this.ExpectedPipelineType = expectedPipelineType;
            }
        }
    }
}
