namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryPartitionKeyTest : QueryTestsBase
    {
        private static readonly List<string> SampleDocuments = new List<string>
        {
            @"{""id"":""id0"",""operation"":""create"",""duration"":45,""tenant"":""tenant0"",""user"":""user0"",""session"":""session0""}",
            @"{""id"":""id1"",""operation"":""update"",""duration"":23,""tenant"":""tenant0"",""user"":""user0"",""session"":""session1""}",
            @"{""id"":""id2"",""operation"":""delete"",""duration"":67,""tenant"":""tenant0"",""user"":""user1"",""session"":""session0""}",
            @"{""id"":""id3"",""operation"":""create"",""duration"":89,""tenant"":""tenant0"",""user"":""user1"",""session"":""session1""}",
            @"{""id"":""id4"",""operation"":""update"",""duration"":12,""tenant"":""tenant1"",""user"":""user0"",""session"":""session0""}",
            @"{""id"":""id5"",""operation"":""delete"",""duration"":56,""tenant"":""tenant1"",""user"":""user0"",""session"":""session1""}",
            @"{""id"":""id6"",""operation"":""create"",""duration"":34,""tenant"":""tenant1"",""user"":""user1"",""session"":""session0""}",
            @"{""id"":""id7"",""operation"":""update"",""duration"":78,""tenant"":""tenant1"",""user"":""user1"",""session"":""session1""}",
            @"{""id"":""id8"",""operation"":""delete"",""duration"":91,""tenant"":""tenant2"",""user"":""user0"",""session"":""session0""}",
            @"{""id"":""id9"",""operation"":""create"",""duration"":5,""tenant"":""tenant2"",""user"":""user1"",""session"":""session1""}",
            @"{""id"":""id10"",""operation"":""update"",""duration"":42,""tenant"":""tenant0"",""user"":""user0"",""session"":""session2""}",
            @"{""id"":""id11"",""operation"":""delete"",""duration"":73,""tenant"":""tenant1"",""user"":""user2"",""session"":""session0""}",
            @"{""id"":""id12"",""operation"":""create"",""duration"":28,""tenant"":""tenant2"",""user"":""user0"",""session"":""session1""}",
            @"{""id"":""id13"",""operation"":""update"",""duration"":61,""tenant"":""tenant0"",""user"":""user2"",""session"":""session2""}",
            @"{""id"":""id14"",""operation"":""delete"",""duration"":15,""tenant"":""tenant1"",""user"":""user1"",""session"":""session2""}",
        };

        private static PartitionKeyDefinition HierarchicalPartitionKeyDefinition =>
            new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/tenant", "/user", "/session" },
                Kind = PartitionKind.MultiHash,
                Version = Documents.PartitionKeyDefinitionVersion.V2
            };

        private static PartitionKeyDefinition SinglePartitionKeyDefinition =>
            new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/tenant" },
                Kind = PartitionKind.Hash,
                Version = Documents.PartitionKeyDefinitionVersion.V2
            };

        [TestMethod]
        public async Task TestSinglePartitionKeyContainer_Direct_SinglePartition()
        {
            await this.ExecuteTest(
                SinglePartitionKeyDefinition,
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition);
        }

        [TestMethod]
        public async Task TestSinglePartitionKeyContainer_Gateway_SinglePartition()
        {
            await this.ExecuteTest(
                SinglePartitionKeyDefinition,
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition);
        }

        [TestMethod]
        public async Task TestSinglePartitionKeyContainer_Direct_MultiPartition()
        {
            await this.ExecuteTest(
                SinglePartitionKeyDefinition,
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition);
        }

        [TestMethod]
        public async Task TestSinglePartitionKeyContainer_Gateway_MultiPartition()
        {
            await this.ExecuteTest(
                SinglePartitionKeyDefinition,
                ConnectionModes.Gateway,
                CollectionTypes.MultiPartition);
        }

        [TestMethod]
        public async Task TestHierarchicalPartitionKeyContainer_Direct_SinglePartition()
        {
            await this.ExecuteTest(
                HierarchicalPartitionKeyDefinition,
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition);
        }

        [TestMethod]
        public async Task TestHierarchicalPartitionKeyContainer_Gateway_SinglePartition()
        {
            await this.ExecuteTest(
                HierarchicalPartitionKeyDefinition,
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition);
        }

        [TestMethod]
        public async Task TestHierarchicalPartitionKeyContainer_Direct_MultiPartition()
        {
            await this.ExecuteTest(
                HierarchicalPartitionKeyDefinition,
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition);
        }

        [TestMethod]
        public async Task TestHierarchicalPartitionKeyContainer_Gateway_MultiPartition()
        {
            await this.ExecuteTest(
                HierarchicalPartitionKeyDefinition,
                ConnectionModes.Gateway,
                CollectionTypes.MultiPartition);
        }

        private async Task ExecuteTest(PartitionKeyDefinition partitionKeyDefinition, ConnectionModes connectionMode, CollectionTypes collectionType)
        {
            await this.CreateIngestQueryDeleteAsync(
                connectionMode,
                collectionType,
                SampleDocuments,
                (container, documents) => this.ExecuteAllQueries(container, partitionKeyDefinition, documents),
                partitionKeyDefinition,
                IndexingPolicy);
        }

        private static Cosmos.IndexingPolicy IndexingPolicy =>
            new Cosmos.IndexingPolicy
            {
                Automatic = true,
                IndexingMode = Cosmos.IndexingMode.Consistent,
                IncludedPaths = new Collection<Cosmos.IncludedPath>
                {
                    new Cosmos.IncludedPath
                    {
                        Path = "/*"
                    }
                }
            };

        private async Task ExecuteAllQueries(
            Container container,
            PartitionKeyDefinition partitionKeyDefinition,
            IReadOnlyList<CosmosElements.CosmosObject> documents)
        {
            Console.WriteLine("\n=== Executing All Query Tests with Partition Keys ===\n");

            // Queries must project full partition key values from the documents hit by the query.
            // The projected pk values are used to validate that only documents with the specified partition key are returned.
            List<string> queries = new List<string>
            {
                @"
                SELECT *
                FROM c",

                @"
                SELECT *
                FROM c
                WHERE STARTSWITH(c.tenant, 'tenant')",

                @"
                SELECT *
                FROM c
                WHERE c.operation = 'create' OR c.operation = 'delete'",

                @"
                SELECT
                    COUNT(1) count,
                    c.tenant,
                    c.session,
                    c.user
                FROM c
                GROUP BY c.tenant, c.session, c.user",

                @"
                SELECT *
                FROM c
                ORDER BY c.tenant",

                @"
                SELECT
                    min(c.duration) AS minDuration,
                    max(c.duration) AS maxDuration,
                    sum(c.duration) AS sumDuration,
                    c.tenant,
                    c.session,
                    c.user
                FROM c
                GROUP BY c.tenant, c.session, c.user
                ORDER BY c.tenant"
            };

            // Step 1: Get distinct partition key values
            Console.WriteLine("Step 1: Extracting distinct partition key values from documents...");
            List<string[]> distinctPkValues = GetDistinctPartitionKeyValues(documents, partitionKeyDefinition);
            Console.WriteLine($"Found {distinctPkValues.Count} distinct partition key value combinations\n");

            // Step 2: For each distinct partition key value, execute all queries
            for (int i = 0; i < distinctPkValues.Count; i++)
            {
                string[] pkValues = distinctPkValues[i];

                Console.WriteLine($"\n====== Executing queries with Partition Key #{i + 1} ======");
                Console.WriteLine($"Partition Key Values: [{string.Join(", ", pkValues)}]");
                Console.WriteLine($"Partition Key Length: {pkValues.Length}");

                // Build the partition key
                Cosmos.PartitionKey partitionKey = BuildPartitionKey(pkValues);
                Console.WriteLine($"PartitionKey: {partitionKey.ToJsonString()}\n");

                for (int k = 0; k < queries.Count; k++)
                {
                    string query = queries[k];
                    Console.WriteLine("------------");
                    Console.WriteLine($"Query {k + 1}:{query}\n");

                    QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                    {
                        PartitionKey = partitionKey
                    };

                    FeedIterator<CosmosElement> iterator = container.GetItemQueryIterator<CosmosElement>(
                        new QueryDefinition(query),
                        requestOptions: queryRequestOptions);

                    List<CosmosElement> results = new List<CosmosElement>();
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<CosmosElement> response = await iterator.ReadNextAsync();
                        results.AddRange(response);
                    }

                    Console.WriteLine($"Results: {results.Count} documents:");
                    foreach (CosmosElement result in results)
                    {
                        Console.WriteLine($"    {result}");
                    }

                    // Validate partition key matches
                    this.ValidateResultsMatchPartitionKey(results, pkValues, partitionKeyDefinition);
                    Console.WriteLine("------------\n");
                }
            }

            Console.WriteLine("\n=== All Queries Completed ===\n");
        }

        private static List<string[]> GetDistinctPartitionKeyValues(
            IReadOnlyList<CosmosElements.CosmosObject> documents,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            HashSet<string> distinctValuesSet = new HashSet<string>();
            List<string[]> distinctValuesList = new List<string[]>();
            int pathCount = partitionKeyDefinition.Paths.Count;

            foreach (CosmosObject document in documents)
            {
                string[] pkValues = ExtractPartitionKeyValues(document, partitionKeyDefinition);

                if (pathCount == 1)
                {
                    // Single partition key: add the single value
                    string key = string.Join("|", pkValues.Select(v => v ?? "<<null>>"));
                    if (distinctValuesSet.Add(key))
                    {
                        distinctValuesList.Add(pkValues);
                    }
                }
                else
                {
                    // Hierarchical partition key: generate all prefixes (1, 2, and 3 elements)
                    for (int prefixLength = 1; prefixLength <= pathCount; prefixLength++)
                    {
                        string[] prefixValues = pkValues.Take(prefixLength).ToArray();
                        string key = string.Join("|", prefixValues.Select(v => v ?? "<<null>>"));
                        
                        if (distinctValuesSet.Add(key))
                        {
                            distinctValuesList.Add(prefixValues);
                        }
                    }
                }
            }

            return distinctValuesList;
        }

        private static Cosmos.PartitionKey BuildPartitionKey(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("Values array must have at least one element", nameof(values));
            }

            if (values.Length > 3)
            {
                throw new ArgumentException("Values array cannot have more than 3 elements", nameof(values));
            }

            // Build the partition key from the values
            PartitionKeyBuilder pkBuilder = new PartitionKeyBuilder();
            
            foreach (string value in values)
            {
                if (value == null)
                {
                    pkBuilder.AddNullValue();
                }
                else
                {
                    pkBuilder.Add(value);
                }
            }

            return pkBuilder.Build();
        }

        private void ValidateResultsMatchPartitionKey(
            List<CosmosElement> results,
            string[] expectedPkValues,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            int validCount = 0;
            int invalidCount = 0;

            foreach (CosmosElement result in results)
            {
                if (result is CosmosObject cosmosObject)
                {
                    // Extract partition key values from the result
                    string[] actualPkValues = ExtractPartitionKeyValues(cosmosObject, partitionKeyDefinition);

                    if (actualPkValues != null)
                    {
                        // Check if the actual partition key values match the expected prefix
                        bool matches = DoPartitionKeyValuesMatch(expectedPkValues, actualPkValues);

                        if (matches)
                        {
                            validCount++;
                        }
                        else
                        {
                            invalidCount++;
                            Console.WriteLine($"    WARNING: Document with PK values [{string.Join(", ", actualPkValues)}] does not match expected prefix [{string.Join(", ", expectedPkValues)}]");
                        }
                    }
                }
            }

            Console.WriteLine($"\nValidation: {validCount} documents match partition key, {invalidCount} documents do not match");
            
            if (invalidCount > 0)
            {
                Assert.Fail($"Found {invalidCount} documents that do not match the specified partition key");
            }
        }

        private static bool DoPartitionKeyValuesMatch(string[] expectedPkValues, string[] actualPkValues)
        {
            // The expected PK values can be a prefix of the actual PK values
            // For example, if expected is ["tenant0"], it matches actual ["tenant0", "user0", "session0"]
            // If expected is ["tenant0", "user0"], it matches actual ["tenant0", "user0", "session0"]
            // If expected is ["tenant0", "user0", "session0"], it must match exactly

            if (expectedPkValues.Length > actualPkValues.Length)
            {
                return false;
            }

            for (int i = 0; i < expectedPkValues.Length; i++)
            {
                string expectedValue = expectedPkValues[i];
                string actualValue = actualPkValues[i];

                // Handle null comparison
                if (expectedValue == null && actualValue == null)
                {
                    continue;
                }

                if (expectedValue == null || actualValue == null)
                {
                    return false;
                }

                if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] ExtractPartitionKeyValues(CosmosObject cosmosObject, PartitionKeyDefinition partitionKeyDefinition)
        {
            string[] values = new string[partitionKeyDefinition.Paths.Count];

            for (int i = 0; i < partitionKeyDefinition.Paths.Count; i++)
            {
                string path = partitionKeyDefinition.Paths[i];
                string propertyName = path.TrimStart('/');

                if (cosmosObject.TryGetValue(propertyName, out CosmosElement element))
                {
                    if (element is CosmosString cosmosString)
                    {
                        values[i] = cosmosString.Value;
                    }
                    else if (element is CosmosNull)
                    {
                        values[i] = null;
                    }
                    else
                    {
                        // For other types, convert to string representation
                        values[i] = element.ToString();
                    }
                }
                else
                {
                    values[i] = null;
                }
            }

            return values;
        }
    }
}
