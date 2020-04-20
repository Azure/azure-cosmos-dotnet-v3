

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class SanityQueryTests : QueryTestsBase
    {
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

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
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

                            async Task ValidateNonDeterministicQuery(
                                Func<Container, string, QueryRequestOptions, Task<List<CosmosObject>>> queryFunc,
                                bool hasOrderBy)
                            {
                                List<CosmosObject> queryResults = await queryFunc(container, query, queryRequestOptions);
                                HashSet<string> expectedIds = new HashSet<string>(inputDocuments
                                    .Select(document => ((CosmosString)document["id"]).Value));
                                HashSet<string> actualIds = new HashSet<string>(queryResults
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

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
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

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
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

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
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

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
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
                            ((Exception exception, PartitionedQueryExecutionInfo partitionedQueryExecutionInfo), (bool canSupportActual, FeedIterator queryIterator)) = await conatinerCore.TryExecuteQueryAsync(
                                supportedQueryFeatures: queryFeatures,
                                queryDefinition: new QueryDefinition(query),
                                requestOptions: new QueryRequestOptions()
                                {
                                    MaxConcurrency = maxDegreeOfParallelism,
                                    MaxItemCount = maxItemCount,
                                },
                                continuationToken: continuationToken,
                                cancellationToken: default(CancellationToken));

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
                                continuationToken: null,
                                requestOptions: new QueryRequestOptions()
                                {
                                    MaxConcurrency = 1,
                                    MaxItemCount = 1,
                                },
                                cancellationToken: default(CancellationToken));

                Assert.IsNotNull(exception);
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
                continuationToken: notJsonContinuationToken,
                expectedResponseMessageError: $"Response status code does not indicate success: BadRequest (400); Substatus: 0; ActivityId: ; Reason: (Malformed Continuation Token: {notJsonContinuationToken});");

            string validJsonInvalidFormatContinuationToken = @"{""range"":{""min"":""05C189CD6732"",""max"":""05C18F5D153C""}";
            await this.TestMalformedPipelinedContinuationTokenRunner(
                container: container,
                queryText: "SELECT * FROM c",
                continuationToken: validJsonInvalidFormatContinuationToken,
                expectedResponseMessageError: $"Response status code does not indicate success: BadRequest (400); Substatus: 0; ActivityId: ; Reason: (Malformed Continuation Token: {validJsonInvalidFormatContinuationToken});");
        }

        private async Task TestMalformedPipelinedContinuationTokenRunner(
            Container container,
            string queryText,
            string continuationToken,
            string expectedResponseMessageError)
        {
            {
                // Malformed continuation token
                FeedIterator itemStreamQuery = container.GetItemQueryStreamIterator(
                queryText: queryText,
                continuationToken: continuationToken);
                ResponseMessage cosmosQueryResponse = await itemStreamQuery.ReadNextAsync();
                Assert.AreEqual(HttpStatusCode.BadRequest, cosmosQueryResponse.StatusCode);
                string errorMessage = cosmosQueryResponse.ErrorMessage;
                Assert.AreEqual(expectedResponseMessageError, errorMessage);
            }

            // Malformed continuation token
            try
            {
                FeedIterator<dynamic> itemQuery = container.GetItemQueryIterator<dynamic>(
                    queryText: queryText,
                    continuationToken: continuationToken);
                await itemQuery.ReadNextAsync();

                Assert.Fail("Expected bad request");
            }
            catch (CosmosException ce)
            {
                Assert.IsNotNull(ce);
                string message = ce.ToString();
                Assert.IsNotNull(message);
                Assert.IsTrue(message.StartsWith($"Microsoft.Azure.Cosmos.CosmosException : {expectedResponseMessageError}"));
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
    }
}
