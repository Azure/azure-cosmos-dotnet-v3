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
    public sealed class SingleRoundtripOptimisticExecutionQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestSingleRoundtripOptimistExecQueries()
        {
            int numberOfDocuments = 8;
            string partitionKey = "key";
            string numberField = "numberField";
            string nullField = "nullField";

            List<string> documents = new List<string>(numberOfDocuments);
            for (int i = 0; i < numberOfDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(partitionKey, "/value");
                doc.SetPropertyValue(numberField, i % 8);
                doc.SetPropertyValue(nullField, null);
                documents.Add(doc.ToString());
            }

            SinglePartitionWithContinuationsArgs args = new SinglePartitionWithContinuationsArgs
            {
                NumberOfDocuments = numberOfDocuments,
                PartitionKey = partitionKey,
                NumberField = numberField,
                NullField = nullField,
            };

            await this.CreateIngestQueryDeleteAsync<SinglePartitionWithContinuationsArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                RunTests,
                args,
                "/" + partitionKey);
        }

        private static async Task RunTests(Container container, IReadOnlyList<CosmosObject> documents, SinglePartitionWithContinuationsArgs args)
        {
            await TestPositiveSingleRoundtripOptimistExecOutput(container, args);
            await TestNegativeSingleRoundtripOptimistExecOutput(container);
        }

        private static async Task TestPositiveSingleRoundtripOptimistExecOutput(
            Container container,
            SinglePartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string nullField = args.NullField;

            SingleRoundtripOptimisticExecutionQueryTests singleRoundtripOptimisticQueryTests = new SingleRoundtripOptimisticExecutionQueryTests();

            // feedOptions provide a partitionKey which ensures singleRoundtrip pipeline is utilized
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                TestSettings = GetTestInjections(false, false, true)
            };

            // check if pipeline returns empty continuation token
            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: feedOptions).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

            QueryResultsAndPipelineType queryAndResults = new QueryResultsAndPipelineType
            {
                Query = new List<string>
                {
                    $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}",
                    $"SELECT VALUE r.numberField FROM r",
                    $"SELECT VALUE r.numberField FROM r",
                    $"SELECT TOP 4 VALUE r.numberField FROM r ORDER BY r.{numberField}",
                    $"SELECT TOP 3 VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC",
                    $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1",
                    $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField}"
                },

                Results = new List<List<int>>
                {
                    new List<int> { 0, 1, 2, 3, 4 },
                    new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 },
                    new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 },
                    new List<int> { 0, 1, 2, 3},
                    new List<int> { 7, 6, 5},
                    new List<int> { 1},
                    new List<int> { 0, 1, 2, 3, 4, 5, 6, 7},
                },

                PartitionKeys = new List<string>
                {
                    "/value",
                    null,
                    "/value",
                    "/value",
                    "/value", 
                    null,
                    null,
                },

                ExpectedPipelineType = new List<TestInjections.PipelineType>
                {
                    TestInjections.PipelineType.SingleRoundtripOptimisticExecution,
                    TestInjections.PipelineType.Passthrough,
                    TestInjections.PipelineType.SingleRoundtripOptimisticExecution,
                    TestInjections.PipelineType.SingleRoundtripOptimisticExecution,
                    TestInjections.PipelineType.SingleRoundtripOptimisticExecution,
                    TestInjections.PipelineType.Specialized,
                    TestInjections.PipelineType.Specialized,
                },
            };

            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };

            for (int i = 0; i < pageSizeOptions.Length; i++)
            {
                for(int j = 0; j < queryAndResults.Query.Count(); j++)
                {
                    feedOptions.MaxItemCount = pageSizeOptions[i];
                    feedOptions.PartitionKey = queryAndResults.PartitionKeys[j] == null
                        ? null
                        : new Cosmos.PartitionKey(queryAndResults.PartitionKeys[j]);

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            queryAndResults.Query[j],
                            feedOptions);

                    int[] actual = items.Select(doc => doc.ToString()).Select(int.Parse).ToArray();

                    bool areEqual = actual.SequenceEqual(queryAndResults.Results[j]);

                    Assert.IsTrue(areEqual);
                    Assert.AreEqual(queryAndResults.ExpectedPipelineType[j], feedOptions.TestSettings.Stats.PipelineType.Value);
                }
            }

            // test if pipeline is called if TestInjection.EnableSingleRoundtripOptimisticQueryTests is false
            feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                TestSettings = GetTestInjections(false, false, false)
            };

            for (int j = 0; j < queryAndResults.Query.Count(); j++)
            {
                feedOptions.PartitionKey = queryAndResults.PartitionKeys[j] == null
                        ? null
                        : new Cosmos.PartitionKey(queryAndResults.PartitionKeys[j]);

                List<CosmosElement> items = await RunQueryAsync(
                        container,
                        queryAndResults.Query[j],
                        feedOptions);

                int[] actual = items.Select(doc => doc.ToString()).Select(int.Parse).ToArray();

                bool areEqual = actual.SequenceEqual(queryAndResults.Results[j]);

                Assert.IsTrue(areEqual);
                Assert.AreNotEqual(TestInjections.PipelineType.SingleRoundtripOptimisticExecution, feedOptions.TestSettings.Stats.PipelineType.Value);
            }
        }

        private static async Task TestNegativeSingleRoundtripOptimistExecOutput(
            Container container)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = GetTestInjections(false, false, true)
            };

            // check if bad continuation queries and syntax error queries are handled by pipeline
            IDictionary<string, string> invalidQueries = new Dictionary<string, string>
            {
                { "SELECT * FROM t", Guid.NewGuid().ToString() },
                { "SELECT TOP 10 * FOM r", null },
                { "this is not a valid query", null },
            };

            foreach (KeyValuePair<string, string> entry in invalidQueries)
            {
                try
                {
                    await container.GetItemQueryIterator<Document>(
                        queryDefinition: new QueryDefinition(entry.Key),
                        continuationToken: entry.Value,
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
        
        private static TestInjections GetTestInjections(bool simulate429s, bool simulateEmptyPages, bool enableSingleRoundtripOptimisticQueryTests)
        {
            return new TestInjections(
                            simulate429s,
                            simulateEmptyPages,
                            enableSingleRoundtripOptimisticQueryTests,
                            new TestInjections.ResponseStats());
        }

        private struct SinglePartitionWithContinuationsArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public string NumberField;
            public string NullField;
        }

        private struct QueryResultsAndPipelineType
        {
            public List<string> Query;
            public List<List<int>> Results;
            public List<string> PartitionKeys;
            public List<TestInjections.PipelineType> ExpectedPipelineType;
        }

    }
}
