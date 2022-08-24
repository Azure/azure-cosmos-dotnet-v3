namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Policy;
    using System.Text;
    using System.Threading.Tasks;
    using Castle.Components.DictionaryAdapter;
    using HdrHistogram.Utilities;
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
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Moq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SingleRoundtripOptimisticExecutionQuery;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SingleRoundtripOptimisticExecutionQueryTests : QueryTestsBase
    {
        private static readonly string singleRoundtripOptimisticExecution = "SingleRoundtripOptimisticExecution";
        private static readonly string specialized = "Specialized";
        private static readonly string passThrough = "PassThrough";

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
               // PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = singleRoundtripOptimisticQueryTests.GetTestInjections(false, false, true)
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

                ExpectedPipelineType = new List<string>
                {
                    singleRoundtripOptimisticExecution,
                    passThrough,
                    singleRoundtripOptimisticExecution,
                    singleRoundtripOptimisticExecution,
                    singleRoundtripOptimisticExecution,
                    specialized,
                    specialized,
                },
            };

            int[] pageSizeOptions = new[] { -1, 1, 2, 10, 100 };

            for (int i = 0; i < 5; i++)
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

                    if (queryAndResults.ExpectedPipelineType[j] == singleRoundtripOptimisticExecution)
                    {
                        Assert.AreEqual(TestInjections.PipelineType.SingleRoundtripOptimisticExecution, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
                    else if (queryAndResults.ExpectedPipelineType[j] == specialized)
                    {
                        Assert.AreEqual(TestInjections.PipelineType.Specialized, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
                    else {
                        Assert.AreEqual(TestInjections.PipelineType.Passthrough, feedOptions.TestSettings.Stats.PipelineType.Value);
                    }
                }
            }

            // test if pipeline is called if TestInjection.EnableSingleRoundtripOptimisticQueryTests is false
            feedOptions = new QueryRequestOptions
            {
                MaxItemCount = -1,
                TestSettings = singleRoundtripOptimisticQueryTests.GetTestInjections(false, false, false)
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
            SingleRoundtripOptimisticExecutionQueryTests queryTests = new SingleRoundtripOptimisticExecutionQueryTests();

            QueryRequestOptions feedOptions = new QueryRequestOptions
            {
                PartitionKey = new Cosmos.PartitionKey("/value"),
                TestSettings = queryTests.GetTestInjections(false, false, true)
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
        
        private TestInjections GetTestInjections(bool simulate429s, bool simulateEmptyPages, bool enableSingleRoundtripOptimisticQueryTests)
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
            public List<string> ExpectedPipelineType;
        }

    }
}
