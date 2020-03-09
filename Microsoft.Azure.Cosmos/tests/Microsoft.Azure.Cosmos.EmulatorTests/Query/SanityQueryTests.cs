

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class SanityQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestBasicCrossPartitionQueryAsync()
        {
            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
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

                            List<JToken> queryResults = await QueryTestsBase.RunQueryAsync<JToken>(
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

            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestNonDeterministicQueryResultsAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            async Task ImplementationAsync(Container container, IEnumerable<Document> inputDocuments)
            {
                foreach (int maxDegreeOfParallelism in new int[] { 1, 100 })
                {
                    foreach (int maxItemCount in new int[] { 10, 100 })
                    {
                        foreach (bool useOrderBy in new bool[] { false, true })
                        {
                            string query;
                            if (useOrderBy)
                            {
                                query = "SELECT c._ts, c.id FROM c ORDER BY c._ts";
                            }
                            else
                            {
                                query = "SELECT c.id FROM c";
                            }

                            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                            {
                                MaxBufferedItemCount = 7000,
                                MaxConcurrency = maxDegreeOfParallelism,
                                MaxItemCount = maxItemCount,
                                ReturnResultsInDeterministicOrder = false,
                            };

                            async Task ValidateNonDeterministicQuery(Func<Container, string, QueryRequestOptions, Task<List<JToken>>> queryFunc, bool hasOrderBy)
                            {
                                List<JToken> queryResults = await queryFunc(container, query, queryRequestOptions);
                                HashSet<string> expectedIds = new HashSet<string>(inputDocuments.Select(document => document.Id));
                                HashSet<string> actualIds = new HashSet<string>(queryResults.Select(queryResult => queryResult["id"].Value<string>()));
                                Assert.IsTrue(expectedIds.SetEquals(actualIds), $"query: {query} failed with {nameof(maxDegreeOfParallelism)}: {maxDegreeOfParallelism}, {nameof(maxItemCount)}: {maxItemCount}");

                                if (hasOrderBy)
                                {
                                    IEnumerable<long> timestamps = queryResults.Select(token => token["_ts"].Value<long>());
                                    IEnumerable<long> sortedTimestamps = timestamps.OrderBy(x => x);
                                    Assert.IsTrue(timestamps.SequenceEqual(sortedTimestamps), "Items were not sorted.");
                                }
                            }

                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithoutContinuationTokensAsync<JToken>, useOrderBy);
                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithContinuationTokensAsync<JToken>, useOrderBy);
                            await ValidateNonDeterministicQuery(QueryTestsBase.QueryWithCosmosElementContinuationTokenAsync<JToken>, useOrderBy);
                        }
                    }
                }
            }

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestExceptionlessFailuresAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
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

                        List<JToken> queryResults = await QueryTestsBase.RunQueryAsync<JToken>(
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestEmptyPagesAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
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

                        List<JToken> queryResults = await QueryTestsBase.RunQueryAsync<JToken>(
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestQueryPlanGatewayAndServiceInteropAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
            {
                ContainerCore containerCore = (ContainerInlineCore)container;

                foreach (bool isGatewayQueryPlan in new bool[] { true, false })
                {
                    MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                        containerCore.ClientContext,
                        containerCore,
                        isGatewayQueryPlan);

                    ContainerCore containerWithForcedPlan = new ContainerCore(
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

                            List<JToken> queryResults = await QueryTestsBase.RunQueryAsync<JToken>(
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestUnsupportedQueriesAsync()
        {
            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
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
                        await QueryTestsBase.RunQueryAsync<JToken>(
                            container,
                            unsupportedQuery,
                            queryRequestOptions: feedOptions);
                        Assert.Fail("Expected query to fail due it not being supported.");
                    }
                    catch (Exception e)
                    {
                        Assert.IsTrue(e.Message.Contains("Compositions of aggregates and other expressions are not allowed."),
                            e.Message);
                    }
                }
            }

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                NoDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestTryExecuteQuery()
        {
            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition,
                QueryTestsBase.NoDocuments,
                this.TestTryExecuteQueryHelper);
        }

        private async Task TestTryExecuteQueryHelper(
            Container container,
            IEnumerable<Document> documents)
        {
            ContainerCore conatinerCore = (ContainerInlineCore)container;
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
                            ((Exception exception, PartitionedQueryExecutionInfo partitionedQueryExecutionInfo), (bool canSupportActual, FeedIterator queryIterator)) = await conatinerCore.TryExecuteQueryAsync(
                                supportedQueryFeatures: queryFeatures,
                                queryDefinition: new QueryDefinition(query),
                                requestOptions: new QueryRequestOptions()
                                {
                                    MaxConcurrency = maxDegreeOfParallelism,
                                    MaxItemCount = maxItemCount,
                                },
                                continuationToken: continuationToken);

                            Assert.AreEqual(canSupportExpected, canSupportActual);
                            if (canSupportExpected)
                            {
                                ResponseMessage cosmosQueryResponse = await queryIterator.ReadNextAsync();
                                continuationToken = cosmosQueryResponse.ContinuationToken;
                            }

                            Assert.IsNotNull(partitionedQueryExecutionInfo);
                        } while (continuationToken != null);
                    }
                }
            }

            {
                // Test the syntax error case
                ((Exception exception, PartitionedQueryExecutionInfo partitionedQueryExecutionInfo), (bool canSupportActual, FeedIterator queryIterator)) = await conatinerCore.TryExecuteQueryAsync(
                                supportedQueryFeatures: QueryFeatures.None,
                                queryDefinition: new QueryDefinition("This is not a valid query."),
                                requestOptions: new QueryRequestOptions()
                                {
                                    MaxConcurrency = 1,
                                    MaxItemCount = 1,
                                },
                                continuationToken: null);

                Assert.IsNotNull(exception);
            }
        }

        [TestMethod]
        public async Task TestMalformedPipelinedContinuationToken()
        {
            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                NoDocuments,
                this.TestMalformedPipelinedContinuationTokenHelper);
        }

        private async Task TestMalformedPipelinedContinuationTokenHelper(
            Container container,
            IEnumerable<Document> documents)
        {
            string query = "SELECT * FROM c";

            // Malformed continuation token
            try
            {
                FeedIterator itemQuery = container.GetItemQueryStreamIterator(
                    queryText: query,
                    continuationToken: "is not the continuation token you are looking for");
                ResponseMessage cosmosQueryResponse = await itemQuery.ReadNextAsync();

                Assert.Fail("Expected bad request");
            }
            catch (Exception)
            {
            }
        }

        [TestMethod]
        public void ServiceInteropUsedByDefault()
        {
            // Test initialie does load CosmosClient
            Assert.IsFalse(CustomTypeExtensions.ByPassQueryParsing());
        }
    }
}
