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
        public async Task TestOptimisticDirectExecutionQueries()
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
            await TestPositiveOptimisticDirectExecutionOutput(container, args);
            await TestNegativeOptimisticDirectExecutionOutput(container);
        }

        private static async Task TestPositiveOptimisticDirectExecutionOutput(
            Container container,
            SinglePartitionWithContinuationsArgs args)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string nullField = args.NullField;

            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                TestSettings = GetTestInjections(simulate429s:false, simulateEmptyPages:false, enableOptimisticDirectExecution:true)
            };

            // check if pipeline returns empty continuation token
            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: feedOptions).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

            List<string> queries = new List<string>
            {
                $"SELECT TOP 5 VALUE r.numberField FROM r ORDER BY r.{partitionKey}",
                $"SELECT VALUE r.numberField FROM r",
                $"SELECT VALUE r.numberField FROM r",
                $"SELECT TOP 4 VALUE r.numberField FROM r ORDER BY r.{numberField}",
                $"SELECT TOP 3 VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC",
                $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1",
                $"SELECT DISTINCT VALUE r.{numberField} FROM r ORDER BY r.{numberField}",
                $"SELECT TOP 3 VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} ORDER BY r.{numberField} DESC",
                $"SELECT TOP 4 VALUE r.numberField FROM r ORDER BY r.{numberField}",
                $"SELECT VALUE r.numberField FROM r",
            };

            List<List<long>> results = new List<List<long>>
            {
                new List<long> { 0, 1, 2, 3, 4 },
                new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 },
                new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 },
                new List<long> { 0, 1, 2, 3},
                new List<long> { 7, 6, 5},
                new List<long> { 1},
                new List<long> { 0, 1, 2, 3, 4, 5, 6, 7},
                new List<long> { 7, 6, 5},
                new List<long> { 0, 1, 2, 3},
                new List<long> { 0, 1, 2, 3, 4, 5, 6, 7 },
            };

            List<string> partitionKeys = new List<string>
            {
                "/value",
                null,
                "/value",
                "/value",
                "/value",
                null,
                null,
                "/value",
                "/value",
                "/value",
            };

            List<TestInjections.PipelineType> expectedPipelineType = new List<TestInjections.PipelineType>
            {
                TestInjections.PipelineType.OptimisticDirectExecution,
                TestInjections.PipelineType.Passthrough,
                TestInjections.PipelineType.OptimisticDirectExecution,
                TestInjections.PipelineType.OptimisticDirectExecution,
                TestInjections.PipelineType.OptimisticDirectExecution,
                TestInjections.PipelineType.Specialized,
                TestInjections.PipelineType.Specialized,
                TestInjections.PipelineType.Specialized,
                TestInjections.PipelineType.Specialized,
                TestInjections.PipelineType.Passthrough,
            };

            List<bool> enabledOptimisticDirectExecution = new List<bool>
            {
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                false,
                false,
                false,
            };

            List<QueryResultsAndPipelineType> queryAndResults = new List<QueryResultsAndPipelineType>();

            for (int i = 0; i < queries.Count(); i++)
            {
                QueryResultsAndPipelineType queryAndResult = new QueryResultsAndPipelineType()
                {
                    Query = queries[i],
                    Result = results[i],
                    PartitionKey = partitionKeys[i],
                    ExpectedPipelineType = expectedPipelineType[i],
                    EnableOptimisticDirectExecution = enabledOptimisticDirectExecution[i],
                };

                queryAndResults.Add(queryAndResult);
            }

            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };

            for (int i = 0; i < pageSizeOptions.Length; i++)
            {
                for(int j = 0; j < queryAndResults.Count(); j++)
                {
                    feedOptions = new QueryRequestOptions
                    {
                        MaxItemCount = pageSizeOptions[i],
                        PartitionKey = queryAndResults[j].PartitionKey == null  
                           ? null
                           : new Cosmos.PartitionKey(queryAndResults[j].PartitionKey),
                        TestSettings = GetTestInjections(
                            simulate429s: false, 
                            simulateEmptyPages: false, 
                            enableOptimisticDirectExecution: queryAndResults[j].EnableOptimisticDirectExecution)
                    };

                    List<CosmosElement> items = await RunQueryAsync(
                            container,
                            queryAndResults[j].Query,
                            feedOptions);

                    long[] actual = items.Cast<CosmosNumber>().Select(x => Number64.ToLong(x.Value)).ToArray();

                    Assert.IsTrue(queryAndResults[j].Result.SequenceEqual(actual));

                    if (queryAndResults[j].EnableOptimisticDirectExecution)
                    {
                        Assert.AreEqual(queryAndResults[j].ExpectedPipelineType, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
                    else
                    {   // test if pipeline is called if TestInjection.EnableOptimisticDirectExecution is false
                        Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, feedOptions.TestSettings.Stats.PipelineType.Value);
                    } 
                }
            }
        }

        private static async Task TestNegativeOptimisticDirectExecutionOutput(
            Container container)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = GetTestInjections(simulate429s: false, simulateEmptyPages: false, enableOptimisticDirectExecution: true)
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

        private static TestInjections GetTestInjections(bool simulate429s, bool simulateEmptyPages, bool enableOptimisticDirectExecution)
        {
            return new TestInjections(
                            simulate429s,
                            simulateEmptyPages,
                            enableOptimisticDirectExecution,
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
            public string Query { get; set; }
            public List<long> Result { get; set; }
            public string PartitionKey { get; set; }
            public TestInjections.PipelineType ExpectedPipelineType { get; set; }
            public bool EnableOptimisticDirectExecution { get; set; }
        }
    }
}
