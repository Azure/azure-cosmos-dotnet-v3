namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class SkipTakeQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryCrossPartitionTop()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;
            string partitionKey = "field_0";

            QueryOracle.QueryOracleUtil util = new QueryOracle.QueryOracle2(seed);
            IEnumerable<string> documents = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionTopHelper,
                "/" + partitionKey);
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionOffsetLimit()
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

            List<string> documents = new List<string>();
            // Shuffle them so that they end up in different pages.
            people = people.OrderBy((person) => Guid.NewGuid()).ToList();
            foreach (Person person in people)
            {
                documents.Add(JsonConvert.SerializeObject(person));
            }

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionOffsetLimit,
                "/id");
        }

        private async Task TestQueryCrossPartitionOffsetLimit(
            Container container,
            IEnumerable<Document> documents)
        {
            foreach (int offsetCount in new int[] { 0, 1, 10, documents.Count() })
            {
                foreach (int limitCount in new int[] { 0, 1, 10, documents.Count() })
                {
                    foreach (int pageSize in new int[] { 1, 10, documents.Count() })
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

                        IEnumerable<JToken> expectedResults = documents.Select(document => document.propertyBag);
                        // ORDER BY
                        expectedResults = expectedResults.OrderBy(x => x["guid"].Value<string>(), StringComparer.Ordinal);

                        // SELECT VALUE c.name
                        expectedResults = expectedResults.Select(document => document["guid"]);

                        // SKIP TAKE
                        expectedResults = expectedResults.Skip(offsetCount);
                        expectedResults = expectedResults.Take(limitCount);

                        List<JToken> queryResults = await QueryTestsBase.RunQueryAsync<JToken>(
                            container,
                            query,
                            queryRequestOptions: queryRequestOptions);
                        Assert.IsTrue(
                            expectedResults.SequenceEqual(queryResults, JsonTokenEqualityComparer.Value),
                            $@"
                                {query} (without continuations) didn't match
                                expected: {JsonConvert.SerializeObject(expectedResults)}
                                actual: {JsonConvert.SerializeObject(queryResults)}");
                    }
                }
            }
        }

        private async Task TestQueryCrossPartitionTopHelper(Container container, IEnumerable<Document> documents)
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
                            FeedIterator<dynamic> documentQuery = container.GetItemQueryIterator<dynamic>(
                                    query,
                                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 0, MaxItemCount = pageSize });

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
}
