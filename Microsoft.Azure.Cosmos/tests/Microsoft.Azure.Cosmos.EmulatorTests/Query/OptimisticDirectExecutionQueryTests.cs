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
            
            List<QueryResultsAndPipelineType> queryAndResults = new List<QueryResultsAndPipelineType>()
            {
                // Tests for bool enableOptimisticDirectExecution
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = false, ExpectedPipelineType = TestInjections.PipelineType.Specialized},
                
                // Simple Query
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r", Result = new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r", Result = new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, PartitionKey = null, Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r", Result = new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, PartitionKey = "/value", Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r", Result = new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 }, PartitionKey = null, Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.Passthrough},

                // DISTINCT with ORDER BY
                new QueryResultsAndPipelineType { Query = $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", Result = new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", Result = new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, PartitionKey = null, Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", Result = new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, PartitionKey = "/value", Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField} DESC", Result = new List<long> { 7, 6, 5, 4, 3, 2, 1, 0 }, PartitionKey = null, Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.Specialized},
                
                // TOP with GROUP BY
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = null, Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = "/value", Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT TOP 5 VALUE r.{numberField} FROM r GROUP BY r.{numberField}", Result = new List<long> { 0, 1, 2, 3, 4 }, PartitionKey = null, Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.Specialized},
                
                // OFFSET LIMIT with WHERE and BETWEEN
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = null, Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = "/value", Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {numberOfDocuments} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = null, Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.Specialized},
            };

            Container singlePartitionContainer = await CreateContainer(this, CollectionTypes.SinglePartition, documents, partitionKey);
            Container multiPartitionContainer = await CreateContainer(this, CollectionTypes.MultiPartition, documents, partitionKey);
            foreach (QueryResultsAndPipelineType queryAndResult in queryAndResults)
            {
                bool isSinglePartition = queryAndResult.Partition == CollectionTypes.SinglePartition;
                Container container = isSinglePartition ? singlePartitionContainer : multiPartitionContainer;

                await this.CreateIngestQueryDeleteAsync(
                    ConnectionModes.Direct | ConnectionModes.Gateway,
                    isSinglePartition ? CollectionTypes.SinglePartition : CollectionTypes.MultiPartition,
                    documents,
                    (container, documents) => RunPassingTests(container, queryAndResult, isSinglePartition),
                    "/" + partitionKey);
            }
        }

        private static async Task RunPassingTests(Container container, QueryResultsAndPipelineType queryAndResult, bool isSinglePartition)
        {
            await HelperFunctionForPassingTests(queryAndResult, container, isSinglePartition);
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

            foreach (KeyValuePair<string, string> entry in invalidQueries)
            {
                await this.CreateIngestQueryDeleteAsync(
                    ConnectionModes.Direct | ConnectionModes.Gateway,
                    CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                    documents,
                    (container, documents, args) => RunFailingTests(container, entry),
                    "/" + partitionKey);
            }
        }

        private static async Task RunFailingTests(Container container, KeyValuePair<string, string> queryAndResult)
        {
            await HelperFunctionForFailingTests(container, queryAndResult);
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

        private static async Task<Container> CreateContainer(QueryTestsBase queryTestsBase, CollectionTypes collectionType, List<string> documents, string partitionKey)
        {
            await queryTestsBase.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                collectionType,
                documents,
                (container, documents) => Task.FromResult(container),
                "/" + partitionKey);

            return null;
        }

        private static async Task HelperFunctionForPassingTests(QueryResultsAndPipelineType queryAndResults, Container container, bool isSinglePartition)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                EnableOptimisticDirectExecution = true,
                TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
            };

            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };
            for (int i = 0; i < pageSizeOptions.Length; i++)
            {
                // Added check because "Continuation token is not supported for queries with GROUP BY."
                if (queryAndResults.Query.Contains("GROUP BY"))
                {
                    if (!isSinglePartition) continue;
                    if (pageSizeOptions[i] != -1) continue;
                }

                feedOptions = new QueryRequestOptions
                {
                    MaxItemCount = pageSizeOptions[i],
                    PartitionKey = queryAndResults.PartitionKey == null
                        ? null
                        : new Cosmos.PartitionKey(queryAndResults.PartitionKey),
                    EnableOptimisticDirectExecution = queryAndResults.EnableOptimisticDirectExecution,
                    TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
                };

                List<CosmosElement> items = await RunQueryAsync(
                        container,
                        queryAndResults.Query,
                        feedOptions);

                long[] actual = items.Cast<CosmosNumber>().Select(x => Number64.ToLong(x.Value)).ToArray();

                Assert.IsTrue(queryAndResults.Result.SequenceEqual(actual));

                if (queryAndResults.EnableOptimisticDirectExecution)
                {
                    Assert.AreEqual(queryAndResults.ExpectedPipelineType, feedOptions.TestSettings.Stats.PipelineType.Value);
                }
                else
                {
                    // test if Ode is called if TestInjection.EnableOptimisticDirectExecution is false
                    Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, feedOptions.TestSettings.Stats.PipelineType.Value);
                }
            }
        }

        private static async Task HelperFunctionForFailingTests(Container container, KeyValuePair<string, string> queryAndResult)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                EnableOptimisticDirectExecution = true,
                TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
            };

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

        private struct QueryResultsAndPipelineType
        {
            public string Query { get; set; }
            public List<long> Result { get; set; }
            public string PartitionKey { get; set; }
            public CollectionTypes Partition { get; set; }
            public bool EnableOptimisticDirectExecution { get; set; }
            public TestInjections.PipelineType ExpectedPipelineType { get; set; }
        }
    }
}
