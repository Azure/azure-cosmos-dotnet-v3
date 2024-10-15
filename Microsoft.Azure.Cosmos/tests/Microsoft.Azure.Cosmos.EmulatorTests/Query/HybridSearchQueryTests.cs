namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Azure;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private const string CollectionDataPath = "Documents\\text-3properties-1536dimensions-100documents.json";

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();

        [Ignore("This test can only be enabled after Direct package upgrade")]
        [TestMethod]
        public async Task SanityTests()
        {
            Trace.WriteLine("Started HybridSearchQueryTests.SanityTests...");
            Trace.AutoFlush = true;

            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: RunSanityTests,
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunSanityTests(Container container, IReadOnlyList<CosmosObject> _)
        {
            List<SanityTest> testCases = new List<SanityTest>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, ['John'])",
                    new List<int>{ 2, 57, 85 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, ['John'])",
                    new List<int>{ 2, 57, 85 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, ['John'])
                    OFFSET 1 LIMIT 5",
                    new List<int>{ 57, 85 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))",
                    new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 25, 22, 2, 66, 57, 85 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))",
                    new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 25 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))
                    OFFSET 5 LIMIT 10",
                    new List<int>{ 24, 77, 76, 80, 25, 22, 2, 66, 57, 85 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))",
                    new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 25 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))
                    OFFSET 0 LIMIT 13",
                    new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 25, 22, 2, 66 }),
            };

            foreach (SanityTest testCase in testCases)
            {
                List<TextDocument> result = await RunQueryCombinationsAsync<TextDocument>(
                    container,
                    testCase.Query,
                    queryRequestOptions: null,
                    queryDrainingMode: QueryDrainingMode.HoldState);

                IEnumerable<int> actual = result.Select(document => document.Index);
                if (!testCase.ExpectedIndices.SequenceEqual(actual))
                {
                    Trace.WriteLine($"Query: {testCase.Query}");
                    Trace.WriteLine($"Expected: {string.Join(", ", testCase.ExpectedIndices)}");
                    Trace.WriteLine($"Actual: {string.Join(", ", actual)}");
                    Assert.Fail("The query results did not match the expected results.");
                }
            }
        }

        private static async Task<CosmosArray> LoadDocuments()
        {
            // read the json file
            string json = await File.ReadAllTextAsync(CollectionDataPath);
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(json);
            ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(jsonBuffer);
            CosmosObject rootObject = CosmosObject.CreateFromBuffer(readOnlyMemory);
            Assert.IsTrue(rootObject.TryGetValue(FieldNames.Items, out CosmosArray items), "Failed to find items in the json file.");
            return items;
        }

        private static IndexingPolicy CreateIndexingPolicy()
        {
            IndexingPolicy policy = new IndexingPolicy();

            policy.IncludedPaths.Add(new IncludedPath { Path = IndexingPolicy.DefaultPath });
            policy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = $"/index" },
                new CompositePath { Path = $"/mixedTypefield" },
            });

            return policy;
        }

        private static SanityTest MakeSanityTest(string query, IReadOnlyList<int> expectedIndices)
        {
            return new SanityTest
            {
                Query = query,
                ExpectedIndices = expectedIndices,
            };
        }

        private sealed class SanityTest
        {
            public string Query { get; init; }

            public IReadOnlyList<int> ExpectedIndices { get; init; }
        }

        private sealed class TextDocument
        {
            public int Index { get; set; }

            public string Title { get; set; }

            public string Text { get; set; }
        }

        private static class FieldNames
        {
            public const string Items = "items";
            public const string Title = "title";
            public const string Text = "text";
        }
    }
}
