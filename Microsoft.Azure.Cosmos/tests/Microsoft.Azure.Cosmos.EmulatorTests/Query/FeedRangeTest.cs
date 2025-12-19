namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.EmulatorTests.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class FeedRangeTest : QueryTestsBase
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
                Version = PartitionKeyDefinitionVersion.V2
            };

        private static PartitionKeyDefinition SinglePartitionKeyDefinition =>
            new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/tenant" },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2
            };

        [TestMethod]
        public async Task TestSinglePartitionKeyContainer_Direct_SinglePartition()
        {
            await this.ExecuteTest(
                SinglePartitionKeyDefinition,
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition);
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
                CollectionTypes.MultiPartition);
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

        private static string GetEpk(PartitionKeyDefinition partitionKeyDefinition, params string[] values)
        {
            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyDefinition));
            }

            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("Values array must have at least one element", nameof(values));
            }

            if (values.Length > 3)
            {
                throw new ArgumentException("Values array cannot have more than 3 elements", nameof(values));
            }

            if (values.Length != partitionKeyDefinition.Paths.Count)
            {
                throw new ArgumentException(
                    $"Number of values ({values.Length}) must match number of partition key paths ({partitionKeyDefinition.Paths.Count})",
                    nameof(values));
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

            Cosmos.PartitionKey pk = pkBuilder.Build();

            string epk = pk.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);

            return epk;
        }

        private async Task ExecuteAllQueries(
            Container container,
            PartitionKeyDefinition partitionKeyDefinition,
            IReadOnlyList<CosmosElements.CosmosObject> documents)
        {
            Console.WriteLine("\n=== Executing All Query Tests ===\n");

            // Queries must project full partition key values from the documents hit by the query.
            // The projected pk values are used to validate that only documents within the FeedRange are returned.
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

            // Step 2: Generate EPKs for each distinct partition key value
            Console.WriteLine("Step 2: Generating EPK values...");
            List<(string[] pkValues, string epk)> epkList = GenerateEpksForPartitionKeys(distinctPkValues, partitionKeyDefinition);
            Console.WriteLine($"Generated {epkList.Count} EPK values\n");

            // Step 3: Sort EPKs and create ranges between consecutive values
            Console.WriteLine("Step 3: Sorting EPK values and creating ranges...");
            List<(string[] pkValues, string epk)> sortedEpks = epkList.OrderBy(item => item.epk, StringComparer.Ordinal).ToList();
            Console.WriteLine($"Sorted {sortedEpks.Count} unique EPK values\n");

            // Print sorted EPKs
            foreach ((string[] pkValues, string epk) in sortedEpks)
            {
                Console.WriteLine($"  EPK: {epk}, PK Values: [{string.Join(", ", pkValues)}]");
            }

            // Step 4: Create FeedRanges from each minEpk to all subsequent maxEpks and execute queries
            for (int i = 0; i < sortedEpks.Count - 1; i++)
            {
                (string[] minPkValues, string minEpk) = sortedEpks[i];

                // Nested loop: for each minEpk, test ranges to all subsequent maxEpks
                for (int j = i; j < sortedEpks.Count; j++)
                {
                    (string[] maxPkValues, string maxEpk) = sortedEpks[j];

                    Console.WriteLine($"\n====== Executing queries for EPK range [min:{i + 1}, max:{j + 1}] ======");
                    Console.WriteLine($"Min EPK: {minEpk} (inclusive)");
                    Console.WriteLine($"Max EPK: {maxEpk} (exclusive)");
                    
                    // Print all PK values within the range (inclusive)
                    Console.WriteLine("PkValues: [");
                    for (int pkIndex = i; pkIndex <= j; pkIndex++)
                    {
                        (string[] pkValues, string epk) = sortedEpks[pkIndex];
                        Console.WriteLine($"  [{string.Join(", ", pkValues)}]" + (pkIndex == j ? " (exclusive)" : "(inclusive),"));
                    }
                    Console.WriteLine(")");

                    // Create FeedRange from min to max EPK values
                    FeedRange feedRange = CreateFeedRangeFromEpkRange(minEpk, maxEpk);
                    Console.WriteLine($"FeedRange: {feedRange.ToJsonString()}\n");

                    for (int k = 0; k < queries.Count; k++)
                    {
                        string query = queries[k];
                        Console.WriteLine("------------");
                        Console.WriteLine($"Query {k + 1}:{query}\n");

                        FeedIterator<CosmosElement> iterator = container.GetItemQueryIterator<CosmosElement>(
                            feedRange,
                            new QueryDefinition(query));
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

                        // Validate EPK ranges
                        await this.ValidateResultsWithinFeedRange(results, feedRange, partitionKeyDefinition);
                        Console.WriteLine("------------\n");
                    }
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

        private static List<(string[] pkValues, string epk)> GenerateEpksForPartitionKeys(
            List<string[]> distinctPkValues,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            List<(string[] pkValues, string epk)> epkList = new List<(string[], string)>();

            foreach (string[] pkValues in distinctPkValues)
            {
                // Create a partition key definition matching the number of values
                PartitionKeyDefinition pkDef = new PartitionKeyDefinition
                {
                    Paths = new Collection<string>(partitionKeyDefinition.Paths.Take(pkValues.Length).ToList()),
                    Kind = partitionKeyDefinition.Kind,
                    Version = partitionKeyDefinition.Version
                };

                string epk = GetEpk(pkDef, pkValues);
                epkList.Add((pkValues, epk));
            }

            return epkList;
        }

        private static FeedRange CreateFeedRangeFromEpkRange(string minEpk, string maxEpk)
        {
            string feedRangeSerialization = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                Range = new { min = minEpk, max = maxEpk }
            });

            return FeedRange.FromJsonString(feedRangeSerialization);
        }

        private async Task ValidateResultsWithinFeedRange(
            List<CosmosElement> results,
            FeedRange feedRange,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            // Get the EPK ranges from the FeedRange
            IReadOnlyList<Documents.Routing.Range<string>> epkRanges = await ((FeedRangeInternal)feedRange).GetEffectiveRangesAsync(
                await this.Client.DocumentClient.GetPartitionKeyRangeCacheAsync(Tracing.NoOpTrace.Singleton),
                null,
                partitionKeyDefinition,
                Tracing.NoOpTrace.Singleton);

            int validCount = 0;
            int invalidCount = 0;

            foreach (CosmosElement result in results)
            {
                if (result is CosmosObject cosmosObject)
                {
                    // Extract partition key values from the result
                    string[] pkValues = ExtractPartitionKeyValues(cosmosObject, partitionKeyDefinition);

                    if (pkValues != null)
                    {
                        // Get the EPK for this document
                        string documentEpk = GetEpk(partitionKeyDefinition, pkValues);

                        // Check if the EPK is within any of the FeedRange EPK ranges
                        bool isWithinRange = false;
                        foreach (Documents.Routing.Range<string> epkRange in epkRanges)
                        {
                            if (IsEpkWithinRange(documentEpk, epkRange))
                            {
                                isWithinRange = true;
                                break;
                            }
                        }

                        if (isWithinRange)
                        {
                            validCount++;
                        }
                        else
                        {
                            invalidCount++;
                            Console.WriteLine($"    WARNING: Document with EPK '{documentEpk}' is outside FeedRange");
                        }
                    }
                }
            }

            Console.WriteLine($"\nValidation: {validCount} documents within range, {invalidCount} documents outside range");
            
            if (invalidCount > 0)
            {
                Assert.Fail($"Found {invalidCount} documents outside the specified FeedRange");
            }
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

        private static bool IsEpkWithinRange(string epk, Documents.Routing.Range<string> range)
        {
            // Check if epk >= range.Min
            bool isAboveMin = string.Compare(epk, range.Min, StringComparison.Ordinal) >= 0;
            if (!range.IsMinInclusive)
            {
                isAboveMin = string.Compare(epk, range.Min, StringComparison.Ordinal) > 0;
            }

            // Check if epk < range.Max
            bool isBelowMax = string.Compare(epk, range.Max, StringComparison.Ordinal) < 0;
            if (range.IsMaxInclusive)
            {
                isBelowMax = string.Compare(epk, range.Max, StringComparison.Ordinal) <= 0;
            }

            return isAboveMin && isBelowMax;
        }
    }
}

