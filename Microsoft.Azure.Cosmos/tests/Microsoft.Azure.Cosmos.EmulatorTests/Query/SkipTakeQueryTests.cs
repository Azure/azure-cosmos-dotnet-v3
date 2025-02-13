namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SkipTakeQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryCrossPartitionTopAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numDocuments = 100;
            string partitionKey = "field_0";

            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> documentsToInsert = util.GetDocuments(numDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documentsToInsert,
                ImplementationAsync,
                "/" + partitionKey);

            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                List<string> queryFormats = new List<string>()
                {
                    "SELECT {0} TOP {1} * FROM c",
                    // Can't do order by since order by needs to look at all partitions before returning a single document =>
                    // thus we can't tell how many documents the SDK needs to recieve.
                    //"SELECT {0} TOP {1} * FROM c ORDER BY c._ts",

                    // Can't do aggregates since that also retrieves more documents than the user sees
                    //"SELECT {0} TOP {1} VALUE AVG(c._ts) FROM c",
                };

                foreach (string queryFormat in queryFormats)
                {
                    foreach (bool useDistinct in new bool[] { true, false })
                    {
                        foreach (int topCount in new int[] { 0, 1, 10 })
                        {
                            foreach (int pageSize in new int[] { 1, 10 })
                            {
                                // Run the query and use the query metrics to make sure the query didn't grab more documents
                                // than needed.

                                string query = string.Format(queryFormat, useDistinct ? "DISTINCT" : string.Empty, topCount);
                                FeedOptions feedOptions = new FeedOptions
                                {
                                    MaxBufferedItemCount = 1000,

                                };

                                // Max DOP needs to be 0 since the query needs to run in serial => 
                                // otherwise the parallel code will prefetch from other partitions,
                                // since the first N-1 partitions might be empty.
                                using (FeedIterator<dynamic> documentQuery = container.GetItemQueryIterator<dynamic>(
                                        query,
                                        requestOptions: new QueryRequestOptions() { MaxConcurrency = 0, MaxItemCount = pageSize }))
                                {
                                    //QueryMetrics aggregatedQueryMetrics = QueryMetrics.Zero;
                                    int numberOfDocuments = 0;
                                    while (documentQuery.HasMoreResults)
                                    {
                                        FeedResponse<dynamic> cosmosQueryResponse = await documentQuery.ReadNextAsync();

                                        numberOfDocuments += cosmosQueryResponse.Count();
                                        //foreach (QueryMetrics queryMetrics in cosmosQueryResponse.QueryMetrics.Values)
                                        //{
                                        //    aggregatedQueryMetrics += queryMetrics;
                                        //}
                                    }

                                    Assert.IsTrue(
                                        numberOfDocuments <= topCount,
                                        $"Received {numberOfDocuments} documents with query: {query} and pageSize: {pageSize}");
                                }

                                //if (!useDistinct)
                                //{
                                //    Assert.IsTrue(
                                //        aggregatedQueryMetrics.OutputDocumentCount <= topCount,
                                //        $"Received {aggregatedQueryMetrics.OutputDocumentCount} documents query: {query} and pageSize: {pageSize}");
                                //}
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionOffsetLimitAsync()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 10;

            Random rand = new Random(seed);
            List<Person> people = new List<Person>();

            for (int i = 0; i < numberOfDocuments; i++)
            {
                // Generate random people
                Person person = PersonGenerator.GetRandomPerson(rand);
                for (int j = 0; j < rand.Next(0, 4); j++)
                {
                    // Force an exact duplicate
                    people.Add(person);
                }
            }

            List<string> documentsToInsert = new List<string>();

            // Shuffle them so that they end up in different pages.
            people = people.OrderBy((person) => Guid.NewGuid()).ToList();
            foreach (Person person in people)
            {
                documentsToInsert.Add(JsonConvert.SerializeObject(person));
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documentsToInsert,
                ImplementationAsync,
                "/id");

            async Task ImplementationAsync(
                Container container,
                IReadOnlyList<CosmosObject> documents)
            {
                foreach (int offsetCount in new int[] { 0, 1, 10, documents.Count })
                {
                    foreach (int limitCount in new int[] { 0, 1, 10, documents.Count })
                    {
                        foreach (int pageSize in new int[] { 1, 10, documents.Count })
                        {
                            string query = $@"
                                SELECT VALUE c.guid
                                FROM c
                                ORDER BY c.guid
                                OFFSET {offsetCount} LIMIT {limitCount}";

                            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                            {
                                MaxItemCount = pageSize,
                                MaxBufferedItemCount = 1000,
                                MaxConcurrency = 2
                            };

                            IEnumerable<CosmosElement> expectedResults = documents;

                            // ORDER BY
                            expectedResults = expectedResults.OrderBy(x => (x as CosmosObject)["guid"].ToString(), StringComparer.Ordinal);

                            // SELECT VALUE c.name
                            expectedResults = expectedResults.Select(document => (document as CosmosObject)["guid"]);

                            // SKIP TAKE
                            expectedResults = expectedResults.Skip(offsetCount);
                            expectedResults = expectedResults.Take(limitCount);

                            List<CosmosElement> queryResults = await QueryTestsBase.RunQueryAsync(
                                container,
                                query,
                                queryRequestOptions: queryRequestOptions);
                            Assert.IsTrue(
                                expectedResults.SequenceEqual(queryResults),
                                $@"
                                {query} (without continuations) didn't match
                                expected: {JsonConvert.SerializeObject(expectedResults)}
                                actual: {JsonConvert.SerializeObject(queryResults)}");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestTopOffsetLimitClientRanges()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                await QueryTestsBase.NoOp();

                foreach (string query in new[]
                    {
                        "SELECT c.name FROM c OFFSET 0 LIMIT 10",
                        "SELECT c.name FROM c OFFSET 2147483647 LIMIT 10",
                        "SELECT c.name FROM c OFFSET 10 LIMIT 0",
                        "SELECT c.name FROM c OFFSET 10 LIMIT 2147483647",
                        "SELECT TOP 0 c.name FROM c",
                        "SELECT TOP 2147483647 c.name FROM c",
                    })
                {
                    List<CosmosElement> expectedValues = new List<CosmosElement>();
                    FeedIterator<CosmosElement> resultSetIterator = container.GetItemQueryIterator<CosmosElement>(
                        query,
                        requestOptions: new QueryRequestOptions() { MaxConcurrency = 0 });

                    while (resultSetIterator.HasMoreResults)
                    {
                        expectedValues.AddRange(await resultSetIterator.ReadNextAsync());
                    }

                    Assert.AreEqual(0, expectedValues.Count);
                }
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                QueryTestsBase.NoDocuments,
                ImplementationAsync);
        }
    }
}
