namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Policy;
    using System.Text;
    using System.Threading.Tasks;
    using Castle.Components.DictionaryAdapter;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Query")]
    public sealed class TryExecuteQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestTryExecuteQueries()
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

            SinglePartitionWithContinuationsArgs args = new SinglePartitionWithContinuationsArgs()
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
            await TestPositiveTryExecuteOutput(container, args);
            await TestNegativeTryExecuteOutputHelper(container);
        }

        private static async Task TestPositiveTryExecuteOutput(
            Container container,
            SinglePartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string nullField = args.NullField;

            TryExecuteQueryTests tryExecuteQueryTests = new TryExecuteQueryTests();

            // feedOptions provide a partitionKey which ensures the TryExecute pipeline is utilized
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = tryExecuteQueryTests.GetTestInjections(false, false, true)
            };

            // check if pipeline returns empty continuation token
            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: feedOptions).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

            IDictionary<string, List<int>> queriesAndResults = new Dictionary<string, List<int>>
            {
                { $"SELECT TOP 5 r.numberField FROM r ORDER BY r.{partitionKey}", new List<int> { 0, 1, 2, 3, 4 } },
                { $"SELECT r.numberField FROM r", new List<int> { 0, 1, 2, 3, 4, 5, 6, 7} },
                { $"SELECT TOP 4 r.numberField FROM r ORDER BY r.{numberField}", new List<int> { 0, 1, 2, 3} },
                { $"SELECT TOP 3 r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC", new List<int> { 7, 6, 5}},
            };
            
            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };

            for (int i = 0; i < 5; i++)
            {
                foreach (KeyValuePair<string, List<int>> query in queriesAndResults)
                {
                    feedOptions.MaxItemCount = pageSizeOptions[i];

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            query.Key,
                            feedOptions);

                    int[] actual = items
                    .Select(doc => (doc as CosmosObject)["numberField"].ToString())
                    .ToArray().Select(int.Parse).ToArray();

                    bool areEqual = actual.SequenceEqual(query.Value);

                    Assert.IsTrue(areEqual);
                    Assert.AreEqual(TestInjections.PipelineType.TryExecute, feedOptions.TestSettings.Stats.PipelineType.Value);
                }
            }
            // test if pipeline is called if TestInjection.EnableTryExecute is false
            feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = tryExecuteQueryTests.GetTestInjections(false, false, false)
            };

            foreach (KeyValuePair<string, List<int>> query in queriesAndResults)
            {
                List<CosmosElement> items = await RunQueryAsync(
                        container,
                        query.Key,
                        feedOptions);

                int[] actual = items
                    .Select(doc => (doc as CosmosObject)["numberField"].ToString())
                    .ToArray().Select(int.Parse).ToArray();

                bool areEqual = actual.SequenceEqual(query.Value);

                Assert.IsTrue(areEqual);
                Assert.AreNotEqual(TestInjections.PipelineType.TryExecute, feedOptions.TestSettings.Stats.PipelineType.Value);
            }
        }

        private static async Task TestNegativeTryExecuteOutputHelper(
            Container container)
        {
            TryExecuteQueryTests tryExecuteQueryTests = new TryExecuteQueryTests();

            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = tryExecuteQueryTests.GetTestInjections(false, false, true)
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

        //Test is to confirm which queries use the TryExecute pipeline and which use the Specialized pipeline
        [TestMethod]
        public async Task TestCompareTryExecuteAndSpecializedQueries()
        {
            string[] inputDocs = new[]
            {
                @"{""id"":""documentId1"",""key"":""A"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId2"",""key"":""A"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId3"",""key"":""A"",""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId4"",""key"":5,""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId5"",""key"":5,""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId6"",""key"":5,""prop"":1,""shortArray"":[{""a"":7}]}",
                @"{""id"":""documentId10"",""prop"":3,""shortArray"":[{""a"":5}]}",
                @"{""id"":""documentId11"",""prop"":2,""shortArray"":[{""a"":6}]}",
                @"{""id"":""documentId12"",""prop"":1,""shortArray"":[{""a"":7}]}",
            };

            ConnectionModes connectionModes = ConnectionModes.Direct;
            await this.CreateIngestQueryDeleteAsync(
                connectionModes,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocs,
                ImplementationAsync,
                "/key");

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(5);
                TryExecuteQueryTests tryExecuteQueryTests = new TryExecuteQueryTests();

                QueryRequestOptions feedOptions = new QueryRequestOptions
                {
                    MaxBufferedItemCount = 7000,
                };

                async Task<List<CosmosElement>> AssertTryExecuteAsync(string query, Cosmos.PartitionKey? pk = null)
                {
                    feedOptions.TestSettings = tryExecuteQueryTests.GetTestInjections(false, false, true);
                    feedOptions.PartitionKey = pk;

                    List<CosmosElement> queryResults = await QueryTestsBase.RunQueryCombinationsAsync(
                        container,
                        query,
                        feedOptions,
                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                    Assert.IsTrue(feedOptions.TestSettings.Stats.PipelineType.HasValue);
                    Assert.AreEqual(TestInjections.PipelineType.TryExecute, feedOptions.TestSettings.Stats.PipelineType.Value);

                    return queryResults;
                }

                async Task<List<CosmosElement>> AssertSpecializedAsync(string query, Cosmos.PartitionKey? pk = null)
                {
                    feedOptions.TestSettings = tryExecuteQueryTests.GetTestInjections(false, false, true);
                    feedOptions.PartitionKey = pk;

                    List<CosmosElement> queryResults = await QueryTestsBase.RunQueryCombinationsAsync(
                        container,
                        query,
                        feedOptions,
                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                    Assert.IsTrue(feedOptions.TestSettings.Stats.PipelineType.HasValue);
                    Assert.AreEqual(TestInjections.PipelineType.Specialized, feedOptions.TestSettings.Stats.PipelineType.Value);

                    return queryResults;
                }

                await AssertSpecializedAsync("SELECT * FROM c ORDER BY c._ts");

                // Parallel and ORDER BY with partition key
                foreach (string query in new string[]
                {
                    "SELECT * FROM c WHERE c.key = 5",
                    "SELECT * FROM c WHERE c.key = 5 ORDER BY c._ts",
                })
                {
                    List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);
                    Assert.AreEqual(
                        3,
                        queryResults.Count,
                        $"query: {query} failed with ");
                }

                // TOP 
                {
                    // Top + Partition key => tryexecute
                    {
                        string query = "SELECT TOP 2 c.id FROM c WHERE c.key = 5";
                        List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);

                        Assert.AreEqual(
                            2,
                            queryResults.Count,
                            $"query: {query} failed with");
                    }

                    // Top without partition => specialized
                    {
                        string query = "SELECT TOP 2 c.id FROM c";
                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                    }
                }

                // OFFSET / LIMIT 
                {
                    // With Partition Key => tryexecute
                    {
                        string query = "SELECT c.id FROM c WHERE c.key = 5 OFFSET 1 LIMIT 1";
                        List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);

                        Assert.AreEqual(
                            1,
                            queryResults.Count,
                            $"query: {query} failed with");
                    }

                    // Without Partition Key => specialized
                    {
                        string query = "SELECT c.id FROM c OFFSET 1 LIMIT 1";
                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                    }
                }

                // AGGREGATES
                {
                    // With partition key => tryexecute
                    {
                        string query = "SELECT VALUE COUNT(1) FROM c WHERE c.key = 5";
                        List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);

                        Assert.AreEqual(
                            1,
                            queryResults.Count,
                            $"query: {query} failed with");

                        Assert.AreEqual(
                            3,
                            Number64.ToLong((queryResults.First() as CosmosNumber64).GetValue()),
                            $"query: {query} failed wit");
                    }

                    // Without partitoin key => specialized
                    {
                        string query = "SELECT VALUE COUNT(1) FROM c";
                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                    }
                }

                // GROUP BY 
                {
                    // With Partition Key => tryexecute
                    {
                        string query = "SELECT VALUE c.key FROM c WHERE c.key = 5 GROUP BY c.key";
                        List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);

                        Assert.AreEqual(
                            1,
                            queryResults.Count,
                            $"query: {query} failed with");
                    }

                    // Without Partition Key => Specialized
                    {
                        string query = "SELECT VALUE c.key FROM c GROUP BY c.key";
                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                    }
                }

                // DISTINCT 
                {
                    // With Partition Key => tryexecute
                    {
                        string query = "SELECT DISTINCT VALUE c.key FROM c WHERE c.key = 5";
                        List<CosmosElement> queryResults = await AssertTryExecuteAsync(query, partitionKey);

                        Assert.AreEqual(
                            1,
                            queryResults.Count,
                            $"query: {query} failed with");
                    }

                    // Without Partition Key => specialized
                    {
                        string query = "SELECT DISTINCT VALUE c.key FROM c";
                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                    }
                }
            }
        }

        private TestInjections GetTestInjections(bool simulate429s, bool simulateEmptyPages, bool enableTryExecute)
        {
            return new TestInjections(
                            simulate429s,
                            simulateEmptyPages,
                            enableTryExecute,
                            new TestInjections.ResponseStats());
        }

        private struct SinglePartitionWithContinuationsArgs
        {
            public int NumberOfDocuments;
            public string PartitionKey;
            public string NumberField;
            public string NullField;
        }

    }
}
