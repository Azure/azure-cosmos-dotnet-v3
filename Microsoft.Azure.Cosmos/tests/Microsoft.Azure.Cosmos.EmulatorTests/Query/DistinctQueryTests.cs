namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public sealed class DistinctQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestDistinct_ExecuteNextAsync()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                #region Queries
                // To verify distint queries you can run it once without the distinct clause and run it through a hash set 
                // then compare to the query with the distinct clause.
                List<(string, bool)> queries = new List<(string, bool)>()
                {
                    // basic distinct queries
                    ("SELECT {0} VALUE null", true),

                    // number value distinct queries
                    ("SELECT {0} VALUE c.income from c", true),

                    // string value distinct queries
                    ("SELECT {0} VALUE c.name from c", true),

                    // array value distinct queries
                    ("SELECT {0} VALUE c.children from c", true),

                    // object value distinct queries
                    ("SELECT {0} VALUE c.pet from c", true),

                    // scalar expressions distinct query
                    ("SELECT {0} VALUE c.age % 2 FROM c", true),

                    // distinct queries with order by
                    ("SELECT {0} VALUE c.age FROM c ORDER BY c.age", false),

                    // distinct queries with top and no matching order by
                    ("SELECT {0} TOP 2147483647 VALUE c.age FROM c", false),

                    // distinct queries with top and  matching order by
                    ("SELECT {0} TOP 2147483647 VALUE c.age FROM c ORDER BY c.age", false),

                    // distinct queries with aggregates
                    ("SELECT {0} VALUE MAX(c.age) FROM c", false),

                    // distinct queries with joins
                    ("SELECT {0} VALUE c.age FROM p JOIN c IN p.children", true),

                    // distinct queries in subqueries
                    ("SELECT {0} r.age, s FROM r JOIN (SELECT DISTINCT VALUE c FROM (SELECT 1 a) c) s WHERE r.age > 25", false),

                    // distinct queries in scalar subqeries
                    ("SELECT {0} p.name, (SELECT DISTINCT VALUE p.age) AS Age FROM p", true),

                    // select *
                    ("SELECT {0} * FROM c", true)
                };
                #endregion
                #region ExecuteNextAsync API
                // run the query with distinct and without + MockDistinctMap
                // Should receive same results
                // PageSize = 1 guarantees that the backend will return some duplicates.
                foreach ((string query, bool allowDCount) in queries)
                {
                    string queryWithoutDistinct = string.Format(query, "");

                    QueryRequestOptions requestOptions = new QueryRequestOptions() { MaxItemCount = 100, MaxConcurrency = 100 };
                    FeedIterator<CosmosElement> documentQueryWithoutDistinct = container.GetItemQueryIterator<CosmosElement>(
                        queryWithoutDistinct,
                        requestOptions: requestOptions);

                    MockDistinctMap documentsSeen = new MockDistinctMap();
                    List<CosmosElement> documentsFromWithoutDistinct = new List<CosmosElement>();
                    while (documentQueryWithoutDistinct.HasMoreResults)
                    {
                        FeedResponse<CosmosElement> cosmosQueryResponse = await documentQueryWithoutDistinct.ReadNextAsync();
                        foreach (CosmosElement document in cosmosQueryResponse)
                        {
                            if (documentsSeen.Add(document, out UInt128 hash))
                            {
                                documentsFromWithoutDistinct.Add(document);
                            }
                            else
                            {
                                // No Op for debugging purposes.
                            }
                        }
                    }

                    foreach (int pageSize in new int[] { 1, 10, 100 })
                    {
                        string queryWithDistinct = string.Format(query, "DISTINCT");
                        List<CosmosElement> documentsFromWithDistinct = new List<CosmosElement>();
                        FeedIterator<CosmosElement> documentQueryWithDistinct = container.GetItemQueryIterator<CosmosElement>(
                            queryWithDistinct,
                            requestOptions: requestOptions);

                        while (documentQueryWithDistinct.HasMoreResults)
                        {
                            FeedResponse<CosmosElement> cosmosQueryResponse = await documentQueryWithDistinct.ReadNextAsync();
                            documentsFromWithDistinct.AddRange(cosmosQueryResponse);
                        }

                        Assert.AreEqual(documentsFromWithDistinct.Count, documentsFromWithoutDistinct.Count);
                        for (int i = 0; i < documentsFromWithDistinct.Count; i++)
                        {
                            CosmosElement documentFromWithDistinct = documentsFromWithDistinct.ElementAt(i);
                            CosmosElement documentFromWithoutDistinct = documentsFromWithoutDistinct.ElementAt(i);
                            Assert.AreEqual(
                                expected: documentFromWithoutDistinct,
                                actual: documentFromWithDistinct,
                                message: $"{documentFromWithDistinct} did not match {documentFromWithoutDistinct} at index {i} for {queryWithDistinct}, with page size: {pageSize} on a container");
                        }

                        if (allowDCount)
                        {
                            string queryWithDCount = $"SELECT VALUE COUNT(1) FROM({queryWithDistinct})";

                            List<CosmosElement> documentsWithDCount = new List<CosmosElement>();
                            FeedIterator<CosmosElement> documentQueryWithDCount = container.GetItemQueryIterator<CosmosElement>(
                                queryWithDCount,
                                requestOptions: requestOptions);

                            while (documentQueryWithDCount.HasMoreResults)
                            {
                                FeedResponse<CosmosElement> cosmosQueryResponse = await documentQueryWithDCount.ReadNextAsync();
                                documentsWithDCount.AddRange(cosmosQueryResponse);
                            }

                            Assert.AreEqual(1, documentsWithDCount.Count);
                            long dcount = Number64.ToLong((documentsWithDCount.First() as CosmosNumber).Value);
                            Assert.AreEqual(documentsFromWithoutDistinct.Count, dcount);
                        }
                    }
                }
                #endregion
            }

            await this.TestQueryDistinctBaseAsync(ImplementationAsync);
        }

        [TestMethod]
        public async Task TestDistinct_ContinuationTokenSupportAsync()
        {
            async Task ImplemenationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                // Run the ordered distinct query through the continuation api, should result in the same set
                // since the previous hash is passed in the continuation token.
                foreach (string query in new string[]
                {
                "SELECT {0} VALUE c.age FROM c ORDER BY c.age",
                "SELECT {0} VALUE c.name FROM c ORDER BY c.name",
                })
                {
                    string queryWithoutDistinct = string.Format(query, "");
                    MockDistinctMap documentsSeen = new MockDistinctMap();
                    List<CosmosElement> documentsFromWithoutDistinct = await QueryTestsBase.RunQueryCombinationsAsync(
                        container,
                        queryWithoutDistinct,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 10,
                            MaxItemCount = 100,
                        },
                        QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState);
                    documentsFromWithoutDistinct = documentsFromWithoutDistinct
                        .Where(document => documentsSeen.Add(document, out UInt128 hash))
                        .ToList();

                    foreach (int pageSize in new int[] { 1, 10, 100 })
                    {
                        string queryWithDistinct = string.Format(query, "DISTINCT");
                        List<CosmosElement> documentsFromWithDistinct = await QueryTestsBase.RunQueryCombinationsAsync(
                            container,
                            queryWithDistinct,
                            new QueryRequestOptions()
                            {
                                MaxConcurrency = 10,
                                MaxItemCount = pageSize
                            },
                            QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState);

                        Assert.AreEqual(
                            expected: CosmosArray.Create(documentsFromWithDistinct),
                            actual: CosmosArray.Create(documentsFromWithoutDistinct),
                            message: $"Documents didn't match for {queryWithDistinct} on a Partitioned container");
                    }
                }
            }

            await this.TestQueryDistinctBaseAsync(ImplemenationAsync);
        }

        [TestMethod]
        public async Task TestDistinct_CosmosElementContinuationTokenAsync()
        {
            async Task ImplemenationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                // Run the ordered distinct query through the continuation api, should result in the same set
                // since the previous hash is passed in the continuation token.
                foreach ((string query, bool allowDCount) in new (string, bool)[]
                {
                    ("SELECT {0} VALUE c.age FROM c ORDER BY c.age", false),
                    ("SELECT {0} VALUE c.name FROM c ORDER BY c.name", false),
                    ("SELECT {0} VALUE c.name from c", true),
                    ("SELECT {0} VALUE c.age from c", true),
                    ("SELECT {0} VALUE c.mixedTypeField from c", true),
                    ("SELECT {0} TOP 2147483647 VALUE c.city from c", false),
                    ("SELECT {0} VALUE c.age from c ORDER BY c.name", false)
                })
                {
                    string queryWithoutDistinct = string.Format(query, "");
                    MockDistinctMap documentsSeen = new MockDistinctMap();
                    List<CosmosElement> documentsFromWithoutDistinct = await QueryTestsBase.RunQueryCombinationsAsync(
                        container,
                        queryWithoutDistinct,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 10,
                            MaxItemCount = 100,
                        },
                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);
                    documentsFromWithoutDistinct = documentsFromWithoutDistinct
                        .Where(document => documentsSeen.Add(document, out UInt128 hash))
                        .ToList();

                    foreach (int pageSize in new int[] { 1, 10, 100 })
                    {
                        string queryWithDistinct = string.Format(query, "DISTINCT");
                        List<CosmosElement> documentsFromWithDistinct = await QueryTestsBase.RunQueryCombinationsAsync(
                            container,
                            queryWithDistinct,
                            new QueryRequestOptions()
                            {
                                MaxConcurrency = 10,
                                MaxItemCount = pageSize
                            },
                            QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                        Assert.AreEqual(
                            expected: CosmosArray.Create(documentsFromWithDistinct),
                            actual: CosmosArray.Create(documentsFromWithoutDistinct),
                            message: $"Documents didn't match for {queryWithDistinct} on a Partitioned container");

                        if (allowDCount)
                        {
                            string queryWithDCount = $"SELECT VALUE COUNT(1) FROM({queryWithDistinct})";
                            List<CosmosElement> documentsWithDCount = await QueryTestsBase.RunQueryCombinationsAsync(
                                container,
                                queryWithDCount,
                                new QueryRequestOptions()
                                {
                                    MaxConcurrency = 10,
                                    MaxItemCount = pageSize
                                },
                                QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                            Assert.AreEqual(1, documentsWithDCount.Count);
                            long dcount = Number64.ToLong((documentsWithDCount.First() as CosmosNumber).Value);
                            Assert.AreEqual(documentsFromWithoutDistinct.Count, dcount);
                        }
                    }
                }
            }

            await this.TestQueryDistinctBaseAsync(ImplemenationAsync);
        }

        private async Task TestQueryDistinctBaseAsync(Query query)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 100;

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
            // Shuffle them so they end up in different pages
            people = people.OrderBy((person) => Guid.NewGuid()).ToList();
            foreach (Person person in people)
            {
                documents.Add(JsonConvert.SerializeObject(person));
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                query,
                "/id");
        }

        private sealed class MockDistinctMap
        {
            private readonly HashSet<CosmosElement> elementSet = new HashSet<CosmosElement>();

            public bool Add(CosmosElement element, out UInt128 hash)
            {
                hash = default;
                return this.elementSet.Add(element);
            }
        }
    }
}