

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
    public sealed class DistinctQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryDistinct()
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestQueryDistinct,
                "/id");
        }

        private async Task TestQueryDistinct(Container container, IEnumerable<Document> documents, dynamic testArgs = null)
        {
            #region Queries
            // To verify distint queries you can run it once without the distinct clause and run it through a hash set 
            // then compare to the query with the distinct clause.
            List<string> queries = new List<string>()
            {
                // basic distinct queries
                "SELECT {0} VALUE null",

                // number value distinct queries
                "SELECT {0} VALUE c.income from c",

                // string value distinct queries
                "SELECT {0} VALUE c.name from c",

                // array value distinct queries
                "SELECT {0} VALUE c.children from c",

                // object value distinct queries
                "SELECT {0} VALUE c.pet from c",

                // scalar expressions distinct query
                "SELECT {0} VALUE c.age % 2 FROM c",

                // distinct queries with order by
                "SELECT {0} VALUE c.age FROM c ORDER BY c.age",

                // distinct queries with top and no matching order by
                "SELECT {0} TOP 2147483647 VALUE c.age FROM c",

                // distinct queries with top and  matching order by
                "SELECT {0} TOP 2147483647 VALUE c.age FROM c ORDER BY c.age",

                // distinct queries with aggregates
                "SELECT {0} VALUE MAX(c.age) FROM c",

                // distinct queries with joins
                "SELECT {0} VALUE c.age FROM p JOIN c IN p.children",

                // distinct queries in subqueries
                "SELECT {0} r.age, s FROM r JOIN (SELECT DISTINCT VALUE c FROM (SELECT 1 a) c) s WHERE r.age > 25",

                // distinct queries in scalar subqeries
                "SELECT {0} p.name, (SELECT DISTINCT VALUE p.age) AS Age FROM p",

                // select *
                "SELECT {0} * FROM c",
            };
            #endregion
            #region ExecuteNextAsync API
            // run the query with distinct and without + MockDistinctMap
            // Should receive same results
            // PageSize = 1 guarantees that the backend will return some duplicates.
            foreach (string query in queries)
            {
                string queryWithoutDistinct = string.Format(query, "");

                QueryRequestOptions requestOptions = new QueryRequestOptions() { MaxItemCount = 100, MaxConcurrency = 100 };
                FeedIterator<JToken> documentQueryWithoutDistinct = container.GetItemQueryIterator<JToken>(
                        queryWithoutDistinct,
                        requestOptions: requestOptions);

                MockDistinctMap documentsSeen = new MockDistinctMap();
                List<JToken> documentsFromWithoutDistinct = new List<JToken>();
                while (documentQueryWithoutDistinct.HasMoreResults)
                {
                    FeedResponse<JToken> cosmosQueryResponse = await documentQueryWithoutDistinct.ReadNextAsync();
                    foreach (JToken document in cosmosQueryResponse)
                    {
                        if (documentsSeen.Add(document, out Cosmos.Query.Core.UInt128 hash))
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
                    List<JToken> documentsFromWithDistinct = new List<JToken>();
                    FeedIterator<JToken> documentQueryWithDistinct = container.GetItemQueryIterator<JToken>(
                        queryWithDistinct,
                        requestOptions: requestOptions);

                    while (documentQueryWithDistinct.HasMoreResults)
                    {
                        FeedResponse<JToken> cosmosQueryResponse = await documentQueryWithDistinct.ReadNextAsync();
                        documentsFromWithDistinct.AddRange(cosmosQueryResponse);
                    }

                    Assert.AreEqual(documentsFromWithDistinct.Count, documentsFromWithoutDistinct.Count());
                    for (int i = 0; i < documentsFromWithDistinct.Count; i++)
                    {
                        JToken documentFromWithDistinct = documentsFromWithDistinct.ElementAt(i);
                        JToken documentFromWithoutDistinct = documentsFromWithoutDistinct.ElementAt(i);
                        Assert.IsTrue(
                            JsonTokenEqualityComparer.Value.Equals(documentFromWithDistinct, documentFromWithoutDistinct),
                            $"{documentFromWithDistinct} did not match {documentFromWithoutDistinct} at index {i} for {queryWithDistinct}, with page size: {pageSize} on a container");
                    }
                }
            }
            #endregion
            #region Continuation Token Support
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
                List<JToken> documentsFromWithoutDistinct = await QueryTestsBase.RunQueryCombinationsAsync<JToken>(
                    container,
                    queryWithoutDistinct,
                    new QueryRequestOptions()
                    {
                        MaxConcurrency = 10,
                        MaxItemCount = 100,
                    },
                    QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState);
                documentsFromWithoutDistinct = documentsFromWithoutDistinct
                    .Where(document => documentsSeen.Add(document, out Cosmos.Query.Core.UInt128 hash))
                    .ToList();

                foreach (int pageSize in new int[] { 1, 10, 100 })
                {
                    string queryWithDistinct = string.Format(query, "DISTINCT");
                    List<JToken> documentsFromWithDistinct = await QueryTestsBase.RunQueryCombinationsAsync<JToken>(
                        container,
                        queryWithDistinct,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 10,
                            MaxItemCount = pageSize
                        },
                        QueryDrainingMode.ContinuationToken | QueryDrainingMode.HoldState);

                    Assert.IsTrue(
                        documentsFromWithDistinct.SequenceEqual(documentsFromWithoutDistinct, JsonTokenEqualityComparer.Value),
                        $"Documents didn't match for {queryWithDistinct} on a Partitioned container");
                }
            }
            #endregion
            #region TryGetContinuationToken Support
            // Run the ordered distinct query through the continuation api, should result in the same set
            // since the previous hash is passed in the continuation token.
            foreach (string query in new string[]
            {
                "SELECT {0} VALUE c.age FROM c ORDER BY c.age",
                "SELECT {0} VALUE c.name FROM c ORDER BY c.name",
                "SELECT {0} VALUE c.name from c",
                "SELECT {0} VALUE c.age from c",
                "SELECT {0} VALUE c.mixedTypeField from c",
                "SELECT {0} TOP 2147483647 VALUE c.city from c",
                "SELECT {0} VALUE c.age from c ORDER BY c.name",
            })
            {
                string queryWithoutDistinct = string.Format(query, "");
                MockDistinctMap documentsSeen = new MockDistinctMap();
                List<JToken> documentsFromWithoutDistinct = await QueryTestsBase.RunQueryCombinationsAsync<JToken>(
                    container,
                    queryWithoutDistinct,
                    new QueryRequestOptions()
                    {
                        MaxConcurrency = 10,
                        MaxItemCount = 100,
                    },
                    QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);
                documentsFromWithoutDistinct = documentsFromWithoutDistinct
                    .Where(document => documentsSeen.Add(document, out Cosmos.Query.Core.UInt128 hash))
                    .ToList();

                foreach (int pageSize in new int[] { 1, 10, 100 })
                {
                    string queryWithDistinct = string.Format(query, "DISTINCT");
                    List<JToken> documentsFromWithDistinct = await QueryTestsBase.RunQueryCombinationsAsync<JToken>(
                        container,
                        queryWithDistinct,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 10,
                            MaxItemCount = pageSize
                        },
                        QueryDrainingMode.HoldState | QueryDrainingMode.CosmosElementContinuationToken);

                    Assert.IsTrue(
                        documentsFromWithDistinct.SequenceEqual(documentsFromWithoutDistinct, JsonTokenEqualityComparer.Value),
                        $"Documents didn't match for {queryWithDistinct} on a Partitioned container");
                }
            }
            #endregion
        }

        private sealed class MockDistinctMap
        {
            // using custom comparer, since newtonsoft thinks this:
            // JToken.DeepEquals(JToken.Parse("8.1851780346865681E+307"), JToken.Parse("1.0066367885961673E+308"))
            // >> True
            private readonly HashSet<JToken> jTokenSet = new HashSet<JToken>(JsonTokenEqualityComparer.Value);

            public bool Add(JToken jToken, out Cosmos.Query.Core.UInt128 hash)
            {
                hash = default;
                return this.jTokenSet.Add(jToken);
            }
        }
    }
}
