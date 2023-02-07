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

            CollectionTypes[] pipelineTypes = new CollectionTypes[] { CollectionTypes.SinglePartition, CollectionTypes.MultiPartition };
            foreach (CollectionTypes type in pipelineTypes)
            {
                await this.CreateIngestQueryDeleteAsync<SinglePartitionWithContinuationsArgs>(
                     ConnectionModes.Direct | ConnectionModes.Gateway,
                     type,
                     documents,
                     (container, documents, args) => RunTests(container, documents, args, type),
                     args,
                     "/" + partitionKey);
            }
        }

        private static async Task RunTests(Container container, IReadOnlyList<CosmosObject> documents, SinglePartitionWithContinuationsArgs args, CollectionTypes pipelineType)
        {
            await TestPositiveOptimisticDirectExecutionOutput(container, args, pipelineType);
            await TestNegativeOptimisticDirectExecutionOutput(container);
        }

        private static async Task TestPositiveOptimisticDirectExecutionOutput(
            Container container,
            SinglePartitionWithContinuationsArgs args,
            CollectionTypes pipelineType)
        {
            int documentCount = args.NumberOfDocuments;
            string partitionKey = args.PartitionKey;
            string numberField = args.NumberField;
            string nullField = args.NullField;
            
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                EnableOptimisticDirectExecution = true,
                TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
            };

            // check if pipeline returns empty continuation token
            FeedResponse<Document> responseWithEmptyContinuationExpected = await container.GetItemQueryIterator<Document>(
                $"SELECT TOP 0 * FROM r",
                requestOptions: feedOptions).ReadNextAsync();

            Assert.AreEqual(null, responseWithEmptyContinuationExpected.ContinuationToken);

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
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = "/value", Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = null, Partition = CollectionTypes.SinglePartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = "/value", Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.OptimisticDirectExecution},
                new QueryResultsAndPipelineType { Query = $"SELECT VALUE r.numberField FROM r WHERE r.{numberField} BETWEEN 0 AND {documentCount} OFFSET 1 LIMIT 1", Result = new List<long> { 1 }, PartitionKey = null, Partition = CollectionTypes.MultiPartition, EnableOptimisticDirectExecution = true, ExpectedPipelineType = TestInjections.PipelineType.Specialized},
            };

            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };
            for (int i = 0; i < pageSizeOptions.Length; i++)
            {
                for(int j = 0; j < queryAndResults.Count(); j++)
                {
                    if (pipelineType != queryAndResults[j].Partition)
                    {
                        continue;
                    }

                    // Added check because "Continuation token is not supported for queries with GROUP BY."
                    if (queryAndResults[j].Query.Contains("GROUP BY"))
                    {
                        if (pipelineType == CollectionTypes.MultiPartition) continue;
                        if (pageSizeOptions[i] != -1) continue;
                    }

                    feedOptions = new QueryRequestOptions
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
                    
                    Assert.IsTrue(queryAndResults[j].Result.SequenceEqual(actual));

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

        private static async Task TestNegativeOptimisticDirectExecutionOutput(
            Container container)
        {
            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                EnableOptimisticDirectExecution = true,
                TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: false, new TestInjections.ResponseStats())
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
            public CollectionTypes Partition { get; set; }
            public bool EnableOptimisticDirectExecution { get; set; }
            public TestInjections.PipelineType ExpectedPipelineType { get; set; }
        }
    }
}
