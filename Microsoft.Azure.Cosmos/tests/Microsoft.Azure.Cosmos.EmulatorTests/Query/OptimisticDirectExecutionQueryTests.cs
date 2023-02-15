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
        [TestMethod]
        public async Task TestPassingOptimisticDirectExecutionQueries()
        {
            int numberOfDocuments = 8;
            string partitionKey = "key";
            string numberField = "numberField";
            string nullField = "nullField";

            List<string> documents = CreateDocuments(numberOfDocuments, partitionKey, numberField, nullField);

            List<DirectExecutionTestCase> queryAndResults = new List<DirectExecutionTestCase>()
            {
                // Tests for bool enableOptimisticDirectExecution
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: false, expectedPipelineType: TestInjections.PipelineType.Specialized),
                
                // Simple query
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r", expectedResult: new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.Passthrough),

                // DISTINCT with ORDER BY
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", expectedResult: new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.Specialized),
                
                // TOP with GROUP BY
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", expectedResult: new List<long> { 0, 1, 2, 3, 4 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.Specialized),
                
                // OFFSET LIMIT with WHERE and BETWEEN
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.SinglePartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: "/value", partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.OptimisticDirectExecution),
                CreateInput( query: $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", expectedResult: new List<long> { 1 }, partitionKey: null, partition: CollectionTypes.MultiPartition, enableOptimisticDirectExecution: true, expectedPipelineType: TestInjections.PipelineType.Specialized)
            };

            List<DirectExecutionTestCase> singlePartitionContainerTestCases = new List<DirectExecutionTestCase>();
            List<DirectExecutionTestCase> multiPartitionContainerTestCases = new List<DirectExecutionTestCase>();

            foreach (DirectExecutionTestCase queryAndResult in queryAndResults)
            {
                if (queryAndResult.Partition == CollectionTypes.SinglePartition)
                {
                    singlePartitionContainerTestCases.Add(queryAndResult);
                }
                else
                {
                    multiPartitionContainerTestCases.Add(queryAndResult);
                }
            }

            List<CollectionTypes> collectionTypes = new List<CollectionTypes>() { CollectionTypes.SinglePartition, CollectionTypes.MultiPartition};

            foreach (CollectionTypes collectionType in collectionTypes)
            {
                bool isSinglePartition = collectionType == CollectionTypes.SinglePartition;
                List<DirectExecutionTestCase> testCases = isSinglePartition ? singlePartitionContainerTestCases : multiPartitionContainerTestCases;
                
                await this.CreateIngestQueryDeleteAsync(
                    ConnectionModes.Direct | ConnectionModes.Gateway,
                    collectionType,
                    documents,
                    (container, documents) => RunPassingTests(testCases, container),
                    "/" + partitionKey);
            }
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

        private static async Task RunPassingTests(List<DirectExecutionTestCase> queryAndResults, Container container)
        {
            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };
            for (int i = 0; i < pageSizeOptions.Length; i++)
            {
                for (int j = 0; j < queryAndResults.Count; j++)
                {
                    // Added check because "Continuation token is not supported for queries with GROUP BY."
                    if (queryAndResults[j].Query.Contains("GROUP BY"))
                    {
                        if (queryAndResults[j].Partition == CollectionTypes.MultiPartition) continue;
                        if (pageSizeOptions[i] != -1) continue;
                    }

                    QueryRequestOptions feedOptions = new QueryRequestOptions
                    {
                        MaxItemCount = pageSizeOptions[i],
                        PartitionKey = queryAndResults[j].PartitionKey == null
                            ? null
                            : new Cosmos.PartitionKey(queryAndResults[j].PartitionKey),
                        EnableOptimisticDirectExecution = queryAndResults[j].EnableOptimisticDirectExecution,
                        TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
                    };

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            queryAndResults[j].Query,
                            feedOptions);

                    long[] actual = items.Cast<CosmosNumber>().Select(x => Number64.ToLong(x.Value)).ToArray();

                    Assert.IsTrue(queryAndResults[j].ExpectedResult.SequenceEqual(actual));

                    if (queryAndResults[j].EnableOptimisticDirectExecution)
                    {
                        Assert.AreEqual(queryAndResults[j].ExpectedPipelineType, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
                    else
                    {
                        // test if Ode is called if TestInjection.EnableOptimisticDirectExecution is false
                        Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
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
            TestInjections.PipelineType expectedPipelineType)
        {
            return new DirectExecutionTestCase(query, expectedResult, partitionKey, partition, enableOptimisticDirectExecution, expectedPipelineType);
        }

        internal readonly struct DirectExecutionTestCase
        {
            public string Query { get; }
            public List<long> ExpectedResult { get; }
            public string PartitionKey { get; }
            public CollectionTypes Partition { get; }
            public bool EnableOptimisticDirectExecution { get; }
            public TestInjections.PipelineType ExpectedPipelineType { get; }

            public DirectExecutionTestCase(
                string query,
                List<long> expectedResult,
                string partitionKey,
                CollectionTypes partition,
                bool enableOptimisticDirectExecution,
                TestInjections.PipelineType expectedPipelineType)
            {
                this.Query = query;
                this.ExpectedResult = expectedResult;
                this.PartitionKey = partitionKey;
                this.Partition = partition;
                this.EnableOptimisticDirectExecution = enableOptimisticDirectExecution;
                this.ExpectedPipelineType = expectedPipelineType;
            }
        }
    }
}
