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

        // [Ignore("This test can only be enabled after Direct package and emulator upgrade")]
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

        [TestMethod]
        public async Task LuceneSanityTest()
        {
            CosmosArray documentsArray = await LoadDocuments();

            TextDocument queryDocument = new TextDocument
            {
                Title = "John",
                Text = "United States",
            };

            LuceneQueryEngine luceneQueryEngine = LuceneQueryEngine.Create(documentsArray);

            IReadOnlyList<ScoredTextDocument> luceneResults = luceneQueryEngine.RunLuceneQuery(queryDocument, skip: 0, take: -1, x => x.Title)
                .ToList();

            IEnumerable<int> actual = luceneResults.Select(scoredTextDocument => scoredTextDocument.Document.Index);
            List<int> expected = new List<int> { 61, 51, 49, 54, 75, 24, 77, 76, 80, 25, 22, 2, 66, 57, 85 };

            if (!expected.SequenceEqual(actual))
            {
                Trace.WriteLine($"Expected: {string.Join(", ", expected)}");
                Trace.WriteLine($"Actual: {string.Join(", ", actual)}");
                Assert.Fail("The query results did not match the expected results.");
            }
        }

        // [Ignore("This test can only be enabled after Direct package and emulator upgrade")]
        [TestMethod]
        public async Task ParityTests()
        {
            CosmosArray documentsArray = await LoadDocuments();

            LuceneQueryEngine luceneQueryEngine = LuceneQueryEngine.Create(documentsArray);

            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: (container, cosmosDocuments) => RunParityTests(container, luceneQueryEngine, cosmosDocuments),
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunParityTests(Container container, LuceneQueryEngine luceneQueryEngine, IReadOnlyList<CosmosObject> cosmosDocuments)
        {
            List<ParityTestCase> testCases = new List<ParityTestCase>
            {
                MakeParityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, ['John']), FullTextScore(c.text, ['United States']))",
                    MakeLuceneQuery(title: "John", text: "United States", skip: 0, take: -1)),
            };

            TextDocumentKeySelector ridSelector = ConstructRidMap(cosmosDocuments);

            foreach (ParityTestCase testCase in testCases)
            {
                IEnumerable<ScoredTextDocument> luceneResults = luceneQueryEngine.RunLuceneQuery(
                    testCase.LuceneQuery.QueryDocument,
                    testCase.LuceneQuery.Skip,
                    testCase.LuceneQuery.Take,
                    ridSelector);

                IReadOnlyList<TextDocument> cosmosDbResults = await RunQueryCombinationsAsync<TextDocument>(
                    container,
                    testCase.SqlQuery,
                    queryRequestOptions: null,
                    queryDrainingMode: QueryDrainingMode.HoldState);

                IEnumerable<int> luceneIndices = luceneResults.Select(scoredTextDocument => scoredTextDocument.Document.Index);
                IEnumerable<int> cosmosDbIndices = cosmosDbResults.Select(document => document.Index);

                if (!luceneIndices.SequenceEqual(cosmosDbIndices))
                {
                    Trace.WriteLine($"Query: {testCase.SqlQuery}");
                    Trace.WriteLine($"Expected: {string.Join(", ", luceneIndices)}");
                    Trace.WriteLine($"Actual: {string.Join(", ", cosmosDbIndices)}");
                    Assert.Fail("The query results did not match the expected results.");
                }
            }
        }

        private static async Task RunSanityTests(Container container, IReadOnlyList<CosmosObject> _)
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
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

            foreach (SanityTestCase testCase in testCases)
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

        private static TextDocumentKeySelector ConstructRidMap(IReadOnlyList<CosmosObject> cosmosDocuments)
        {
            Dictionary<int, string> ridMap = new Dictionary<int, string>(cosmosDocuments.Count);
            foreach (CosmosObject cosmosObject in cosmosDocuments)
            {
                Assert.IsTrue(cosmosObject.TryGetValue(FieldNames.Rid, out CosmosString rid));
                Assert.IsTrue(cosmosObject.TryGetValue(FieldNames.Index, out CosmosNumber index));
                Assert.IsTrue(index.Value.IsInteger);
                ridMap.Add((int)Number64.ToLong(index.Value), rid.Value);
            }

            return document => ridMap[document.Index];
        }

        private static SanityTestCase MakeSanityTest(string query, IReadOnlyList<int> expectedIndices)
        {
            return new SanityTestCase
            {
                Query = query,
                ExpectedIndices = expectedIndices,
            };
        }

        private static LuceneQuery MakeLuceneQuery(string title, string text, int skip = 0, int take = -1)
        {
            return new LuceneQuery
            {
                QueryDocument = new TextDocument
                {
                    Title = title,
                    Text = text,
                },
                Skip = skip,
                Take = take,
            };
        }

        private static ParityTestCase MakeParityTest(string sqlQuery, LuceneQuery luceneQuery)
        {
            return new ParityTestCase
            {
                SqlQuery = sqlQuery,
                LuceneQuery = luceneQuery,
            };
        }

        private sealed class LuceneQuery
        {
            public TextDocument QueryDocument { get; init; }

            public int Skip { get; init; }

            public int Take { get; init; }
        }

        private sealed class ParityTestCase
        {
            public string SqlQuery { get; init; }

            public LuceneQuery LuceneQuery { get; init; }
        }

        private sealed class SanityTestCase
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

        delegate string TextDocumentKeySelector(TextDocument document);

        private static class FieldNames
        {
            public const string Items = "items";
            public const string Index = "index";
            public const string Title = "title";
            public const string Text = "text";
            public const string Rid = "_rid";
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

            public IEnumerable<ScoredTextDocument> RunLuceneQuery(TextDocument queryDocument, int skip, int take, TextDocumentKeySelector textDocumentKeySelector)
            {
                int topForRRF = Math.Max(120, 2 * (skip + take));

                List<ScoredTextDocument> titleResults = RunQuery(this.analyzer, this.indexSearcher, FieldNames.Title, queryDocument.Title, topForRRF);
                List<ScoredTextDocument> textResults = RunQuery(this.analyzer, this.indexSearcher, FieldNames.Text, queryDocument.Text, topForRRF);

                IEnumerable<ScoredTextDocument> fusedResults = ReciprocalRankFusion(new List<List<ScoredTextDocument>> { titleResults, textResults }, textDocumentKeySelector);

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

            private static IEnumerable<ScoredTextDocument> ReciprocalRankFusion(
                IReadOnlyList<List<ScoredTextDocument>> componentResults,
                TextDocumentKeySelector textDocumentKeySelector)
            {
                // sort all as descending
                foreach (List<ScoredTextDocument> componentResult in componentResults)
                {
                    componentResult.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score));
                }

                int componentCount = componentResults.Count;
                Dictionary<int, (double[], int[], TextDocument)> ranks = new Dictionary<int, (double[], int[], TextDocument)>();
                foreach (List<ScoredTextDocument> componentResult in componentResults)
                {
                    foreach (ScoredTextDocument scoredTextDocument in componentResult)
                    {
                        if (!ranks.ContainsKey(scoredTextDocument.Document.Index))
                        {
                            ranks.Add(scoredTextDocument.Document.Index, (new double[componentCount], new int[componentCount], scoredTextDocument.Document));
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

                        ranks[componentResult[index].Document.Index].Item1[componentIndex] = componentResult[index].Score;
                        ranks[componentResult[index].Document.Index].Item2[componentIndex] = rank;
                    }

                    ++rank;
                    foreach (KeyValuePair<int, (double[], int[], TextDocument)> kvp in ranks)
                    {
                        if (kvp.Value.Item2[componentIndex] == 0)
                        {
                            kvp.Value.Item2[componentIndex] = rank;
                        }
                    }
                }

                DebugTraceHelper.Trace2ColumnHeader();

                List<ScoredTextDocument> rrfScoredDocuments = new List<ScoredTextDocument>(ranks.Count);
                foreach ((double[] scores, int[] componentRanks, TextDocument document) in ranks.Values)
                {
                    double rrfScore = 0.0;
                    for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                    {
                        rrfScore += 1.0 / (60 + componentRanks[componentIndex]);
                    }

                    DebugTraceHelper.TraceRanksAndScores(scores, componentRanks, document, rrfScore);

                    rrfScoredDocuments.Add(new ScoredTextDocument
                    {
                        Document = document,
                        Score = rrfScore,
                    });
                }

                IEnumerable<ScoredTextDocument> result = rrfScoredDocuments
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => textDocumentKeySelector(x.Document));

                return result;
            }

            private static class DebugTraceHelper
            {
                private const bool Enabled = true;
#pragma warning disable CS0162 // Unreachable code detected

                [Conditional("DEBUG")]
                public static void Trace2ColumnHeader()
                {
                    if (!Enabled)
                    {
                        return;
                    }

                    StringBuilder builder = new StringBuilder();
                    builder.Append("Payload");
                    builder.Append("\t");
                    builder.Append("Score0");
                    builder.Append("\t");
                    builder.Append("Score1");
                    builder.Append("\t");
                    builder.Append("RRFScore");
                    builder.Append("\t");
                    builder.Append("Rank0");
                    builder.Append("\t");
                    builder.Append("Rank1");
                    Trace.WriteLine(builder.ToString());
                }

                [Conditional("DEBUG")]
                public static void TraceRanksAndScores(double[] scores, int[] componentRanks, TextDocument document, double rrfScore)
                {
                    if (!Enabled)
                    {
                        return;
                    }

                    StringBuilder builder = new StringBuilder();
                    builder.Append(@$"{{""Index"":{document.Index},""Title"":""{document.Title}"",""Text"":""{document.Text}""}}");
                    builder.Append("\t");
                    
                    foreach (double score in scores)
                    {
                        builder.Append(score);
                        builder.Append("\t");
                    }

                    builder.Append(rrfScore);
                    builder.Append("\t");

                    foreach (int rank in componentRanks)
                    {
                        builder.Append(rank);
                        builder.Append("\t");
                    }

                    builder.Remove(builder.Length - 1, 1);

                    Trace.WriteLine(builder.ToString());
                }
#pragma warning restore CS0162 // Unreachable code detected
            }
        }

        private struct ScoredTextDocument
        {
            public TextDocument Document { get; set; }

            public double Score { get; set; }
        }
    }
}
