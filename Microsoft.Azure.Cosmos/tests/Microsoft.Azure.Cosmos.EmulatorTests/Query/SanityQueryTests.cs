

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class SanityQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task Sanity()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                    container,
                    "SELECT * FROM c");

                Assert.AreEqual(
                    documents.Count(),
                    queryResults.Count);
            }
        }

        [TestMethod]
        public async Task ResponseHeadersAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                FeedIterator<JToken> itemQuery = container.GetItemQueryIterator<JToken>(
                    queryText: "SELECT * FROM c",
                    requestOptions: new QueryRequestOptions());
                while (itemQuery.HasMoreResults)
                {
                    FeedResponse<JToken> page = await itemQuery.ReadNextAsync();
                    Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                }
            }
        }

        [TestMethod]
        public async Task TestBasicCrossPartitionQueryAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
                {
                    foreach (int maxItemCount in new int[] { 10, 100 })
                    {
                        foreach (string query in new string[] { "SELECT c.id FROM c", "SELECT c._ts, c.id FROM c ORDER BY c._ts" })
                        {
                            QueryRequestOptions feedOptions = new QueryRequestOptions
                            {
                                MaxBufferedItemCount = 7000,
                                MaxConcurrency = maxDegreeOfParallelism,
                                MaxItemCount = maxItemCount,
                                ReturnResultsInDeterministicOrder = true,
                            };

                            List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                                container,
                                query,
                                feedOptions);

                            Assert.AreEqual(
                                documents.Count(),
                                queryResults.Count,
                                $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task MemoryLeak()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                List<WeakReference> weakReferences = await CreateWeakReferenceToFeedIterator(container);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                foreach (WeakReference weakReference in weakReferences)
                {
                    Assert.IsFalse(weakReference.IsAlive);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<List<WeakReference>> CreateWeakReferenceToFeedIterator(
            Container container)
        {
            List<WeakReference> weakReferences = new List<WeakReference>();

            // Test draining typed iterator
            using (FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>(
                queryDefinition: null,
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 1000,
                }))
            {
                weakReferences.Add(new WeakReference(feedIterator, true));
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<JObject> response = await feedIterator.ReadNextAsync();
                    foreach (JObject jObject in response)
                    {
                        Assert.IsNotNull(jObject);
                    }
                }
            }

            // Test draining stream iterator
            using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryDefinition: null,
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 1000,
                }))
            {
                weakReferences.Add(new WeakReference(feedIterator, true));
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        Assert.IsNotNull(response.Content);
                    }
                }
            }

            // Test single page typed iterator
            using (FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>(
                queryText: "SELECT * FROM c",
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 10,
                }))
            {
                weakReferences.Add(new WeakReference(feedIterator, true));
                FeedResponse<JObject> response = await feedIterator.ReadNextAsync();
                foreach (JObject jObject in response)
                {
                    Assert.IsNotNull(jObject);
                }
            }

            // Test single page stream iterator
            using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: "SELECT * FROM c",
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 10,
                }))
            {
                weakReferences.Add(new WeakReference(feedIterator, true));
                using (ResponseMessage response = await feedIterator.ReadNextAsync())
                {
                    Assert.IsNotNull(response.Content);
                }
            }

            // Dummy Operation
            using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: "SELECT * FROM c",
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 10,
                }))
            {
                using (ResponseMessage response = await feedIterator.ReadNextAsync())
                {
                    Assert.IsNotNull(response.Content);
                }
            }

            return weakReferences;
        }

        [TestMethod]
        public async Task StoreResponseStatisticsMemoryLeak()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                    queryText: "SELECT * FROM c",
                    continuationToken: null,
                    requestOptions: new QueryRequestOptions
                    {
                        MaxItemCount = 10,
                    }))
                {
                    WeakReference weakReference = await CreateWeakReferenceToResponseContent(feedIterator);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await Task.Delay(500 /*ms*/);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    Assert.IsFalse(weakReference.IsAlive);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<WeakReference> CreateWeakReferenceToResponseContent(
            FeedIterator feedIterator)
        {
            WeakReference weakResponseContent;
            using (ResponseMessage response = await feedIterator.ReadNextAsync())
            {
                Assert.IsNotNull(response.Content);
                weakResponseContent = new WeakReference(response.Content, true);
            }

            return weakResponseContent;
        }

        [TestMethod]
        public async Task TestNonDeterministicQueryResultsAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync);

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> inputDocuments)
            {
                foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
                {
                    foreach (int maxItemCount in new int[] { 10, 100 })
                    {
                        foreach (bool useOrderBy in new bool[] { false, true })
                        {
                            string query = useOrderBy ? "SELECT c._ts, c.id FROM c ORDER BY c._ts" : "SELECT c.id FROM c";
                            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                            {
                                MaxBufferedItemCount = 7000,
                                MaxConcurrency = maxDegreeOfParallelism,
                                MaxItemCount = maxItemCount,
                                ReturnResultsInDeterministicOrder = false,
                            };

                            async Task ValidateNonDeterministicQuery(
                                Func<Container, string, QueryRequestOptions, Task<List<CosmosObject>>> queryFunc,
                                bool hasOrderBy)
                            {
                                List<CosmosObject> queryResults = await queryFunc(container, query, queryRequestOptions);
                                HashSet<UtfAnyString> expectedIds = new HashSet<UtfAnyString>(inputDocuments
                                    .Select(document => ((CosmosString)document["id"]).Value));
                                HashSet<UtfAnyString> actualIds = new HashSet<UtfAnyString>(queryResults
                                    .Select(queryResult => ((CosmosString)queryResult["id"]).Value));
                                Assert.IsTrue(
                                    expectedIds.SetEquals(actualIds),
                                    $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");

                                if (hasOrderBy)
                                {
                                    IEnumerable<long> timestamps = queryResults.Select(token => (long)token["_ts"].ToDouble());
                                    IEnumerable<long> sortedTimestamps = timestamps.OrderBy(x => x);
                                    Assert.IsTrue(timestamps.SequenceEqual(sortedTimestamps), "Items were not sorted.");
                                }
                            }

                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithoutContinuationTokensAsync<CosmosObject>, useOrderBy);
                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithContinuationTokensAsync<CosmosObject>, useOrderBy);
                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithCosmosElementContinuationTokenAsync<CosmosObject>, useOrderBy);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestExceptionlessFailuresAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (int maxItemCount in new int[] { 10, 100 })
                {
                    foreach (string query in new string[] { "SELECT c.id FROM c", "SELECT c._ts, c.id FROM c ORDER BY c._ts" })
                    {
                        QueryRequestOptions feedOptions = new QueryRequestOptions
                        {
                            MaxBufferedItemCount = 7000,
                            MaxConcurrency = 2,
                            MaxItemCount = maxItemCount,
                            TestSettings = new TestInjections(simulate429s: true, simulateEmptyPages: false)
                        };

                        List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                            container,
                            query,
                            feedOptions);

                        Assert.AreEqual(
                            documents.Count(),
                            queryResults.Count,
                            $"query: {query} failed with {nameof(maxItemCount)}: {maxItemCount}");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestEmptyPagesAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (int maxItemCount in new int[] { 10, 100 })
                {
                    foreach (string query in new string[] { "SELECT c.id FROM c", "SELECT c._ts, c.id FROM c ORDER BY c._ts" })
                    {
                        QueryRequestOptions feedOptions = new QueryRequestOptions
                        {
                            MaxBufferedItemCount = 7000,
                            MaxConcurrency = 2,
                            MaxItemCount = maxItemCount,
                            TestSettings = new TestInjections(simulate429s: false, simulateEmptyPages: true)
                        };

                        List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                            container,
                            query,
                            feedOptions);

                        Assert.AreEqual(
                            documents.Count(),
                            queryResults.Count,
                            $"query: {query} failed with {nameof(maxItemCount)}: {maxItemCount}");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestQueryPlanGatewayAndServiceInteropAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                ContainerInternal containerCore = (ContainerInlineCore)container;

                foreach (bool isGatewayQueryPlan in new bool[] { true, false })
                {
                    MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                        containerCore.ClientContext,
                        containerCore,
                        isGatewayQueryPlan);

                    ContainerInternal containerWithForcedPlan = new ContainerInlineCore(
                        containerCore.ClientContext,
                        (DatabaseCore)containerCore.Database,
                        containerCore.Id,
                        cosmosQueryClientCore);

                    int numOfQueries = 0;
                    foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
                    {
                        foreach (int maxItemCount in new int[] { 10, 100 })
                        {
                            numOfQueries++;
                            QueryRequestOptions feedOptions = new QueryRequestOptions
                            {
                                MaxBufferedItemCount = 7000,
                                MaxConcurrency = maxDegreeOfParallelism,
                                MaxItemCount = maxItemCount,
                            };

                            List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                                containerWithForcedPlan,
                                "SELECT * FROM c ORDER BY c._ts",
                                feedOptions);

                            Assert.AreEqual(documents.Count(), queryResults.Count);
                        }
                    }

                    if (isGatewayQueryPlan)
                    {
                        Assert.IsTrue(cosmosQueryClientCore.QueryPlanCalls > numOfQueries);
                    }
                    else
                    {
                        Assert.AreEqual(0, cosmosQueryClientCore.QueryPlanCalls, "ServiceInterop mode should not be calling gateway plan retriever");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestUnsupportedQueriesAsync()
        {
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                NoDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                QueryRequestOptions feedOptions = new QueryRequestOptions
                {
                    MaxBufferedItemCount = 7000,
                    MaxConcurrency = 10,
                    MaxItemCount = 10,
                };

                string compositeAggregate = "SELECT COUNT(1) + 5 FROM c";

                string[] unsupportedQueries = new string[]
                {
                compositeAggregate,
                };

                foreach (string unsupportedQuery in unsupportedQueries)
                {
                    try
                    {
                        await QueryTestsBase.RunQueryAsync(
                            container,
                            unsupportedQuery,
                            queryRequestOptions: feedOptions);
                        Assert.Fail("Expected query to fail due it not being supported.");
                    }
                    catch (CosmosException e)
                    {
                        Assert.IsTrue(e.Message.Contains("Compositions of aggregates and other expressions are not allowed."),
                            e.Message);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestTryExecuteQuery()
        {
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition,
                QueryTestsBase.NoDocuments,
                this.TestTryExecuteQueryHelper);
        }

        private async Task TestTryExecuteQueryHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            ContainerInternal conatinerCore = (ContainerInlineCore)container;
            foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
            {
                foreach (int maxItemCount in new int[] { 10, 100 })
                {
                    foreach ((string query, QueryFeatures queryFeatures, bool canSupportExpected) in new Tuple<string, QueryFeatures, bool>[]
                    {
                        new Tuple<string, QueryFeatures, bool>("SELECT * FROM c", QueryFeatures.None, true),
                        new Tuple<string, QueryFeatures, bool>("SELECT * FROM c ORDER BY c._ts", QueryFeatures.None, false),
                        new Tuple<string, QueryFeatures, bool>("SELECT * FROM c ORDER BY c._ts", QueryFeatures.OrderBy, true),
                    })
                    {
                        string continuationToken = null;
                        do
                        {
                            ContainerInternal.TryExecuteQueryResult tryExecuteQueryResult = await conatinerCore.TryExecuteQueryAsync(
                                supportedQueryFeatures: queryFeatures,
                                queryDefinition: new QueryDefinition(query),
                                requestOptions: new QueryRequestOptions()
                                {
                                    MaxConcurrency = maxDegreeOfParallelism,
                                    MaxItemCount = maxItemCount,
                                },
                                feedRangeInternal: null,
                                continuationToken: continuationToken);

                            if (canSupportExpected)
                            {
                                Assert.IsTrue(tryExecuteQueryResult is ContainerInternal.QueryPlanIsSupportedResult);
                            }
                            else
                            {
                                Assert.IsTrue(tryExecuteQueryResult is ContainerInternal.QueryPlanNotSupportedResult);
                            }

                            if (canSupportExpected)
                            {
                                ContainerInternal.QueryPlanIsSupportedResult queryPlanIsSupportedResult = (ContainerInternal.QueryPlanIsSupportedResult)tryExecuteQueryResult;
                                ResponseMessage cosmosQueryResponse = await queryPlanIsSupportedResult.QueryIterator.ReadNextAsync();
                                continuationToken = cosmosQueryResponse.ContinuationToken;
                            }
                        } while (continuationToken != null);
                    }
                }
            }

            {
                // Test the syntax error case
                ContainerInternal.TryExecuteQueryResult tryExecuteQueryResult = await conatinerCore.TryExecuteQueryAsync(
                    supportedQueryFeatures: QueryFeatures.None,
                    queryDefinition: new QueryDefinition("This is not a valid query."),
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = 1,
                        MaxItemCount = 1,
                    },
                    feedRangeInternal: null,
                    continuationToken: null);

                Assert.IsTrue(tryExecuteQueryResult is ContainerInternal.FailedToGetQueryPlanResult);
            }

            {
                // Test that the force passthrough mechanism works
                ContainerInternal.TryExecuteQueryResult tryExecuteQueryResult = await conatinerCore.TryExecuteQueryAsync(
                    supportedQueryFeatures: QueryFeatures.None, // Not supporting any features
                    queryDefinition: new QueryDefinition("SELECT VALUE [{\"item\": {\"sum\": SUM(c.blah), \"count\": COUNT(c.blah)}}] FROM c"), // Query has aggregates
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = 1,
                        MaxItemCount = 1,
                    },
                    feedRangeInternal: new FeedRangePartitionKeyRange("0"), // filtering on a PkRangeId.
                    continuationToken: null);

                Assert.IsTrue(tryExecuteQueryResult is ContainerInternal.QueryPlanIsSupportedResult);
                ContainerInternal.QueryPlanIsSupportedResult queryPlanIsSupportedResult = (ContainerInternal.QueryPlanIsSupportedResult)tryExecuteQueryResult;
                ResponseMessage response = await queryPlanIsSupportedResult.QueryIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode, response.ErrorMessage);
            }
        }

        [TestMethod]
        public async Task TestMalformedPipelinedContinuationToken()
        {
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                NoDocuments,
                this.TestMalformedPipelinedContinuationTokenHelper);
        }

        private async Task TestMalformedPipelinedContinuationTokenHelper(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            string notJsonContinuationToken = "is not the continuation token you are looking for";
            await this.TestMalformedPipelinedContinuationTokenRunner(
                container: container,
                queryText: "SELECT * FROM c",
                continuationToken: notJsonContinuationToken);

            string validJsonInvalidFormatContinuationToken = @"{""range"":{""min"":""05C189CD6732"",""max"":""05C18F5D153C""}}";
            await this.TestMalformedPipelinedContinuationTokenRunner(
                container: container,
                queryText: "SELECT * FROM c",
                continuationToken: validJsonInvalidFormatContinuationToken);
        }

        private async Task TestMalformedPipelinedContinuationTokenRunner(
            Container container,
            string queryText,
            string continuationToken)
        {
            {
                // Malformed continuation token
                FeedIterator itemStreamQuery = container.GetItemQueryStreamIterator(
                queryText: queryText,
                continuationToken: continuationToken);
                ResponseMessage cosmosQueryResponse = await itemStreamQuery.ReadNextAsync();
                Assert.AreEqual(HttpStatusCode.BadRequest, cosmosQueryResponse.StatusCode);
                string errorMessage = cosmosQueryResponse.ErrorMessage;
                Assert.IsTrue(errorMessage.Contains(continuationToken));
            }

            // Malformed continuation token
            try
            {
                using (FeedIterator<dynamic> itemQuery = container.GetItemQueryIterator<dynamic>(
                    queryText: queryText,
                    continuationToken: continuationToken))
                {
                    await itemQuery.ReadNextAsync();
                }
                Assert.Fail("Expected bad request");
            }
            catch (CosmosException ce)
            {
                Assert.IsNotNull(ce);
                string message = ce.ToString();
                Assert.IsNotNull(message);
                Assert.IsTrue(message.Contains(continuationToken));
                string diagnostics = ce.Diagnostics.ToString();
                Assert.IsNotNull(diagnostics);
            }
        }

        [TestMethod]
        public void ServiceInteropUsedByDefault()
        {
            // Test initialie does load CosmosClient
            Assert.IsFalse(CustomTypeExtensions.ByPassQueryParsing());
        }

        [TestMethod]
        public async Task TestPassthroughQueryAsync()
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

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocs,
                ImplementationAsync,
                "/key");

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
                {
                    foreach (int maxItemCount in new int[] { 10, 100 })
                    {
                        ContainerInternal containerCore = (ContainerInlineCore)container;

                        foreach (bool isGatewayQueryPlan in new bool[] { true, false })
                        {
                            foreach (Cosmos.PartitionKey? partitionKey in new Cosmos.PartitionKey?[] { new Cosmos.PartitionKey(5), default })
                            {
                                QueryRequestOptions feedOptions = new QueryRequestOptions
                                {
                                    MaxBufferedItemCount = 7000,
                                    MaxConcurrency = maxDegreeOfParallelism,
                                    MaxItemCount = maxItemCount,
                                };

                                async Task<List<CosmosElement>> AssertPassthroughAsync(string query, Cosmos.PartitionKey? pk = default)
                                {
                                    MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                                        containerCore.ClientContext,
                                        containerCore,
                                        isGatewayQueryPlan);

                                    ContainerInternal containerWithForcedPlan = new ContainerInlineCore(
                                        containerCore.ClientContext,
                                        (DatabaseCore)containerCore.Database,
                                        containerCore.Id,
                                        cosmosQueryClientCore);

                                    feedOptions.TestSettings = new TestInjections(
                                        simulate429s: false,
                                        simulateEmptyPages: false,
                                        responseStats: new TestInjections.ResponseStats());
                                    feedOptions.PartitionKey = pk;

                                    List<CosmosElement> queryResults = await QueryTestsBase.RunQueryCombinationsAsync(
                                        containerWithForcedPlan,
                                        query,
                                        feedOptions,
                                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                                    Assert.IsTrue(feedOptions.TestSettings.Stats.PipelineType.HasValue);
                                    Assert.AreEqual(TestInjections.PipelineType.Passthrough, feedOptions.TestSettings.Stats.PipelineType.Value);

                                    if (pk.HasValue)
                                    {
                                        Assert.AreEqual(0, cosmosQueryClientCore.QueryPlanCalls);
                                    }

                                    return queryResults;
                                }

                                async Task<List<CosmosElement>> AssertSpecializedAsync(string query, Cosmos.PartitionKey? pk = default)
                                {
                                    MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                                        containerCore.ClientContext,
                                        containerCore,
                                        isGatewayQueryPlan);

                                    ContainerInternal containerWithForcedPlan = new ContainerInlineCore(
                                        containerCore.ClientContext,
                                        (DatabaseCore)containerCore.Database,
                                        containerCore.Id,
                                        cosmosQueryClientCore);

                                    feedOptions.TestSettings = new TestInjections(
                                        simulate429s: false,
                                        simulateEmptyPages: false,
                                        responseStats: new TestInjections.ResponseStats());
                                    feedOptions.PartitionKey = pk;

                                    List<CosmosElement> queryResults = await QueryTestsBase.RunQueryCombinationsAsync(
                                        containerWithForcedPlan,
                                        query,
                                        feedOptions,
                                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                                    Assert.IsTrue(feedOptions.TestSettings.Stats.PipelineType.HasValue);
                                    Assert.AreEqual(TestInjections.PipelineType.Specialized, feedOptions.TestSettings.Stats.PipelineType.Value);

                                    return queryResults;
                                }

                                await AssertPassthroughAsync("SELECT * FROM c", partitionKey);

                                await AssertSpecializedAsync("SELECT * FROM c ORDER BY c._ts");

                                // Parallel and ORDER BY with partition key
                                foreach (string query in new string[]
                                {
                                    "SELECT * FROM c WHERE c.key = 5",
                                    "SELECT * FROM c WHERE c.key = 5 ORDER BY c._ts",
                                })
                                {
                                    List<CosmosElement> queryResults = await AssertPassthroughAsync(query, partitionKey);
                                    Assert.AreEqual(
                                        3,
                                        queryResults.Count,
                                        $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                }

                                // TOP 
                                {
                                    // Top + Partition key => passthrough
                                    {
                                        string query = "SELECT TOP 2 c.id FROM c WHERE c.key = 5";
                                        List<CosmosElement> queryResults = await AssertPassthroughAsync(query, partitionKey);

                                        Assert.AreEqual(
                                            2,
                                            queryResults.Count,
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                    }

                                    // Top without partition => !passthrough
                                    {
                                        string query = "SELECT TOP 2 c.id FROM c";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                                    }
                                }

                                // OFFSET / LIMIT 
                                {
                                    // With Partition Key => passthrough
                                    {
                                        string query = "SELECT c.id FROM c WHERE c.key = 5 OFFSET 1 LIMIT 1";
                                        List<CosmosElement> queryResults = await AssertPassthroughAsync(query, partitionKey);

                                        Assert.AreEqual(
                                            1,
                                            queryResults.Count,
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                    }

                                    // Without Partition Key => specialized
                                    {
                                        string query = "SELECT c.id FROM c OFFSET 1 LIMIT 1";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                                    }
                                }

                                // AGGREGATES
                                {
                                    // With partition key => specialized
                                    {
                                        string query = "SELECT VALUE COUNT(1) FROM c WHERE c.key = 5";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);

                                        Assert.AreEqual(
                                            1,
                                            queryResults.Count,
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");

                                        Assert.AreEqual(
                                            3,
                                            Number64.ToLong((queryResults.First() as CosmosNumber64).GetValue()),
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                    }

                                    // Without partitoin key => specialized
                                    {
                                        string query = "SELECT VALUE COUNT(1) FROM c";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query);
                                    }
                                }

                                // GROUP BY 
                                {
                                    // With Partition Key => Specialized
                                    {
                                        string query = "SELECT VALUE c.key FROM c WHERE c.key = 5 GROUP BY c.key";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);

                                        Assert.AreEqual(
                                            1,
                                            queryResults.Count,
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                    }

                                    // Without Partition Key => Specialized
                                    {
                                        string query = "SELECT VALUE c.key FROM c GROUP BY c.key";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);
                                    }
                                }

                                // DISTINCT 
                                {
                                    // With Partition Key => specialized
                                    {
                                        string query = "SELECT DISTINCT VALUE c.key FROM c WHERE c.key = 5";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);

                                        Assert.AreEqual(
                                            1,
                                            queryResults.Count,
                                            $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");
                                    }

                                    // Without Partition Key => specialized
                                    {
                                        string query = "SELECT DISTINCT VALUE c.key FROM c";
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);
                                    }
                                }

                                // Syntax Error
                                {
                                    string query = "this is not a valid query";
                                    try
                                    {
                                        List<CosmosElement> queryResults = await AssertSpecializedAsync(query, partitionKey);

                                        Assert.Fail("Expected an exception.");
                                    }
                                    catch (CosmosException e) when (e.StatusCode == HttpStatusCode.BadRequest)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestTracingAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (string query in new string[] { "SELECT c.id FROM c", "SELECT c._ts, c.id FROM c ORDER BY c._ts" })
                {
                    QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                    {
                        MaxBufferedItemCount = 7000,
                        MaxConcurrency = 10,
                        MaxItemCount = 10,
                        ReturnResultsInDeterministicOrder = true,
                    };

                    FeedIteratorInternal<CosmosElement> feedIterator = (FeedIteratorInternal<CosmosElement>)container.GetItemQueryIterator<CosmosElement>(
                        queryText: query,
                        requestOptions: queryRequestOptions);
                    ITrace trace;
                    int numChildren = 1; // +1 for create query pipeline
                    using (trace = Trace.GetRootTrace("Cross Partition Query"))
                    {
                        while (feedIterator.HasMoreResults)
                        {
                            await feedIterator.ReadNextAsync(trace, default);
                            numChildren++;
                        }
                    }

                    string traceString = TraceWriter.TraceToText(trace);

                    Console.WriteLine(traceString);

                    //Assert.AreEqual(numChildren, trace.Children.Count);
                }
            }
        }

        [TestMethod]
        public async Task TestCancellationTokenAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                foreach (string query in new string[] { "SELECT c.id FROM c", "SELECT c._ts, c.id FROM c ORDER BY c._ts" })
                {
                    QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                    {
                        MaxBufferedItemCount = 7000,
                        MaxConcurrency = 10,
                        MaxItemCount = 10,
                        ReturnResultsInDeterministicOrder = true,
                    };

                    // See if cancellation token is honored for first request
                    try
                    {
                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        cancellationTokenSource.Cancel();
                        FeedIteratorInternal<CosmosElement> feedIterator = (FeedIteratorInternal<CosmosElement>)container.GetItemQueryIterator<CosmosElement>(
                            queryText: query,
                            requestOptions: queryRequestOptions);
                        await feedIterator.ReadNextAsync(cancellationTokenSource.Token);

                        Assert.Fail("Expected exception.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    // See if cancellation token is honored for second request
                    try
                    {
                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        cancellationTokenSource.Cancel();
                        FeedIteratorInternal<CosmosElement> feedIterator = (FeedIteratorInternal<CosmosElement>)container.GetItemQueryIterator<CosmosElement>(
                            queryText: query,
                            requestOptions: queryRequestOptions);
                        await feedIterator.ReadNextAsync(default);
                        await feedIterator.ReadNextAsync(cancellationTokenSource.Token);

                        Assert.Fail("Expected exception.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    // See if cancellation token is honored mid draining
                    try
                    {
                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        FeedIteratorInternal<CosmosElement> feedIterator = (FeedIteratorInternal<CosmosElement>)container.GetItemQueryIterator<CosmosElement>(
                            queryText: query,
                            requestOptions: queryRequestOptions);
                        await feedIterator.ReadNextAsync(cancellationTokenSource.Token);
                        cancellationTokenSource.Cancel();
                        await feedIterator.ReadNextAsync(cancellationTokenSource.Token);

                        Assert.Fail("Expected exception.");
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        }
    }
}
