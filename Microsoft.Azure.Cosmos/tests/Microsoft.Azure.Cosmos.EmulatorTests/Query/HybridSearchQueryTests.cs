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
    using Lucene.Net.Analysis;
    using Lucene.Net.Analysis.En;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.QueryParsers.Classic;
    using Lucene.Net.Search;
    using Lucene.Net.Store;
    using Lucene.Net.Util;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private const string CollectionDataPath = "Documents\\text-3properties-1536dimensions-100documents.json";

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();

        [Ignore("This test can only be enabled after Direct package and emulator upgrade")]
        [TestMethod]
        public async Task SanityTests()
        {
            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: RunSanityTests,
                indexingPolicy: CompositeIndexPolicy);
        }

        [Ignore("This test can only be enabled after Direct package and emulator upgrade")]
        [TestMethod]
        public async Task ParityTests()
        {
            CosmosArray documentsArray = await LoadDocuments();

            TextDocument queryDocument = new TextDocument
            {
                Title = "John",
                Text = "United States",
            };

            LuceneQueryEngine luceneQueryEngine = LuceneQueryEngine.Create(documentsArray);

            IReadOnlyList<ScoredTextDocument> luceneResults = luceneQueryEngine.RunLuceneQuery(queryDocument, skip: 0, take: -1)
                .ToList();

            IEnumerable<int> actual = luceneResults.Select(scoredTextDocument => scoredTextDocument.Document.Index);
            List<int> expected = new List<int> { 61, 51, 49, 54, 75, 24, 77, 76, 80, 25, 22, 2, 66, 57, 85 };

            if (expected.SequenceEqual(actual))
            {
                Trace.WriteLine($"Expected: {string.Join(", ", expected)}");
                Trace.WriteLine($"Actual: {string.Join(", ", actual)}");
                Assert.Fail("The query results did not match the expected results.");
            }
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
            public const string Index = "index";
            public const string Title = "title";
            public const string Text = "text";
        }

        private class LuceneQueryEngine
        {
            private const LuceneVersion Version = LuceneVersion.LUCENE_48;

            private readonly RAMDirectory directory;

            private readonly Analyzer analyzer;

            private readonly IndexSearcher indexSearcher;

            private LuceneQueryEngine(RAMDirectory directory, Analyzer analyzer, IndexSearcher indexSearcher)
            {
                this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
                this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
                this.indexSearcher = indexSearcher ?? throw new ArgumentNullException(nameof(indexSearcher));
            }

            public static LuceneQueryEngine Create(CosmosArray documentArray)
            {
                RAMDirectory directory = new RAMDirectory();
                Analyzer analyzer = new EnglishAnalyzer(Version);
                IndexWriterConfig indexConfig = new IndexWriterConfig(Version, analyzer) { OpenMode = OpenMode.CREATE };
                using IndexWriter writer = new IndexWriter(directory, indexConfig);

                foreach (CosmosElement item in documentArray)
                {
                    CosmosObject cosmosObject = item as CosmosObject;
                    Document luceneDocument = new Document();

                    CosmosNumber cosmosNumber = cosmosObject[FieldNames.Index] as CosmosNumber;
                    luceneDocument.Add(new TextField(FieldNames.Index, cosmosObject[FieldNames.Index].ToString(), Field.Store.YES));
                    luceneDocument.Add(new TextField(FieldNames.Title, cosmosObject[FieldNames.Title].ToString(), Field.Store.YES));
                    luceneDocument.Add(new TextField(FieldNames.Text, cosmosObject[FieldNames.Text].ToString(), Field.Store.YES));
                    writer.AddDocument(luceneDocument);
                }

                writer.Commit();

                DirectoryReader reader = writer.GetReader(applyAllDeletes: true);
                IndexSearcher indexSearcher = new IndexSearcher(reader);

                return new LuceneQueryEngine(directory, analyzer, indexSearcher);
            }

            public IEnumerable<ScoredTextDocument> RunLuceneQuery(TextDocument queryDocument, int skip, int take)
            {
                int topForRRF = Math.Max(120, 2 * (skip + take));

                List<ScoredTextDocument> textResults = RunQuery(this.analyzer, this.indexSearcher, FieldNames.Text, queryDocument.Text, topForRRF);
                List<ScoredTextDocument> titleResults = RunQuery(this.analyzer, this.indexSearcher, FieldNames.Title, queryDocument.Title, topForRRF);

                IReadOnlyList<ScoredTextDocument> fusedResults = ReciprocalRankFusion(new List<List<ScoredTextDocument>> { textResults, titleResults });

                IEnumerable<ScoredTextDocument> results = fusedResults;

                if (skip > 0)
                {
                    results = results.Skip(skip);
                }

                if (take > 0)
                {
                    results = results.Take(take);
                }

                return results;
            }

            private static List<ScoredTextDocument> RunQuery(
                Analyzer analyzer,
                IndexSearcher indexSearcher,
                string fieldName,
                string queryText,
                int top)
            {
                QueryParser queryParser = new QueryParser(Version, fieldName, analyzer);
                Lucene.Net.Search.Query textQuery = queryParser.Parse(queryText);
                TopDocs textDocuments = indexSearcher.Search(textQuery, top);

                List<ScoredTextDocument> result = textDocuments
                    .ScoreDocs
                    .Select(scoreDoc => 
                    {
                        Document doc = indexSearcher.Doc(scoreDoc.Doc);
                        return new ScoredTextDocument
                        {
                            Score = scoreDoc.Score,
                            Document = new TextDocument
                            {
                                Index = int.Parse(doc.Get(FieldNames.Index)),
                                Title = doc.Get(FieldNames.Title),
                                Text = doc.Get(FieldNames.Text),
                            }
                        };
                    })
                    .ToList();

                return result;
            }

            private static IReadOnlyList<ScoredTextDocument> ReciprocalRankFusion(
                IReadOnlyList<List<ScoredTextDocument>> componentResults)
            {
                // sort all as descending
                foreach (List<ScoredTextDocument> componentResult in componentResults)
                {
                    componentResult.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score));
                }

                int componentCount = componentResults.Count;
                Dictionary<int, (int[], TextDocument)> ranks = new Dictionary<int, (int[], TextDocument)>();
                foreach (List<ScoredTextDocument> componentResult in componentResults)
                {
                    foreach (ScoredTextDocument scoredTextDocument in componentResult)
                    {
                        if (!ranks.ContainsKey(scoredTextDocument.Document.Index))
                        {
                            ranks.Add(scoredTextDocument.Document.Index, (new int[componentCount], scoredTextDocument.Document));
                        }
                    }
                }

                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    List<ScoredTextDocument> componentResult = componentResults[componentIndex];
                    int rank = 1;
                    for (int index = 0; index < componentResult.Count; ++index)
                    {
                        if (index > 0 && componentResult[index].Score < componentResult[index - 1].Score)
                        {
                            ++rank;
                        }

                        ranks[componentResult[index].Document.Index].Item1[componentIndex] = rank;
                    }
                }

                List<ScoredTextDocument> result = new List<ScoredTextDocument>(ranks.Count);
                foreach ((int[] componentRanks, TextDocument document) in ranks.Values)
                {
                    double score = 0.0;
                    for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                    {
                        score += 1.0 / (60 + componentRanks[componentIndex]);
                    }

                    result.Add(new ScoredTextDocument
                    {
                        Document = document,
                        Score = score,
                    });
                }

                result.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score));
                return result;
            }
        }

        private struct ScoredTextDocument
        {
            public TextDocument Document { get; set; }

            public double Score { get; set; }
        }
    }
}
