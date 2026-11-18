namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private static readonly string CollectionDataPath = Path.Combine("Documents", "text-3properties-1536dimensions-100documents.json");

        private const double UndefinedComponentScore = 1.79769e+308;
        private const int RrfDocumentCount = 1000;
        private const int RrfLexicalMatchIndex = 49;
        private const int OrdinalRrfLexicalMatchRank = 1;

        private const string SampleVector = @"[0.02, 0, -0.02, 0, -0.04, -0.01, -0.04, -0.01, 0.06, 0.08, -0.05, -0.04, -0.03, 0.05, -0.03, 0, -0.03, 0, 0.05, 0, 0.03,
0.02, 0, 0.04, 0.05, 0.03, 0, 0, 0, -0.03, -0.01, 0.01, 0, -0.01, -0.03, -0.02, -0.05, 0.01, 0, 0.01, 0, 0.01, -0.03, -0.02, 0.02, 0.02, 0.04, 0.01, 0.04, 0.02, -0.01, -0.01, 0.02, 0.01, 0.02, -0.04, -0.01, 0.06, -0.01, -0.03, -0.04, -0.01, -0.01, 0, 0.03,
-0.02, 0.03, 0.05, 0.01, 0.04, 0.05, -0.05, -0.01, 0.03, 0.02, -0.02, 0, -0.02, -0.02, -0.04, 0.01, -0.05, 0.01, 0.05, 0, -0.02, 0.03, -0.07, 0.05, 0.02, 0.03, 0.05, 0.05, -0.01, 0.03, -0.08, -0.01, -0.03, 0.04, -0.01, -0.02, -0.01, -0.02, -0.03, 0.03, 0.03,
-0.04, 0.04, 0.02, 0, 0.03, -0.02, -0.04, 0.02, 0.01, 0.02, -0.01, 0.03, 0.02, 0.01, -0.02, 0, 0.02, 0, -0.01, 0.02, -0.05, 0.03, 0.03, 0.04, -0.02, 0.04, -0.04, 0.03, 0.03, -0.03, 0, 0.02, 0.06, 0.02, 0.02, -0.01, 0.03, 0, -0.03, -0.06, 0.02, 0, 0.02, -0.04,
-0.05, 0.01, 0.02, 0.02, 0.07, 0.05, -0.01, 0.03, -0.03, -0.06, 0.04, 0.01, -0.01, 0.04, 0.02, 0.03, -0.03, 0.03, -0.01, 0.03, -0.04, -0.02, 0.02, -0.02, -0.03, -0.02, 0.02, -0.01, -0.05, -0.07, 0.02, -0.01, 0, -0.01, -0.02, -0.02, -0.03, -0.03, 0, -0.08, -0.01,
0, -0.01, -0.03, 0.01, 0, -0.02, -0.03, -0.04, -0.01, 0.02, 0, 0, -0.04, 0.04, -0.01, 0.04, 0, -0.06, 0.02, 0.03, 0.01, 0.06, -0.02, 0, 0.01, 0.01, 0.01, 0, -0.02, 0.03, 0.02, 0.01, -0.01, -0.05, 0.03, -0.04, 0, 0.01, -0.02, -0.04, 0.02, 0, 0.09, -0.04, -0.01,
0.02, 0.01, -0.03, 0.04, 0.02, -0.02, -0.02, -0.01, 0.01, -0.04, -0.01, 0.02, 0, 0, 0.07, 0.02, 0, 0, -0.01, 0.01, 0.03, -0.02, 0, 0.03, -0.02, -0.07, -0.04, -0.03, 0, -0.03, -0.02, 0, -0.02, -0.02, -0.05, -0.02, 0, 0.05, 0.01, -0.01, -0.04, 0.02, 0, 0, 0.03,
0.02, -0.03, -0.01, -0.02, 0.06, -0.02, 0.01, 0.01, 0.04, -0.04, 0.06, -0.02, 0.01, 0.03, 0.01, 0.02, -0.02, 0.01, -0.04, 0.05, -0.03, 0.01, -0.01, 0, -0.03, -0.03, 0.04, 0.02, -0.03, -0.03, -0.02, 0.06, 0.04, -0.01, 0.01, 0.01, -0.01, -0.02, -0.02, 0.04, 0.01,
-0.01, 0.01, -0.01, 0, 0.01, -0.04, 0.01, 0, -0.04, 0.05, 0.01, 0.01, 0.09, -0.04, -0.02, 0.04, 0, 0.04, -0.04, -0.04, 0, 0, -0.01, 0.05, -0.01, 0.02, 0.01, -0.03, 0, -0.06, 0.02, 0.04, 0.01, 0.03, 0.01, -0.04, 0, 0.01, 0.05, 0.02, -0.02, 0.02, 0, -0.02, -0.04,
-0.07, -0.02, -0.05, 0.06, 0.01, 0.02, -0.03, 0.06, -0.01, -0.02, -0.02, -0.01, 0, -0.05, 0.06, -0.05, 0, -0.02, -0.02, 0, -0.01, 0.01, 0, -0.01, 0.05, 0.02, 0, 0.02, -0.02, 0.02, 0, 0.08, -0.02, 0.01, -0.03, 0.02, -0.03, 0, -0.01, -0.02, -0.04, 0.06, 0.01,
-0.03, -0.03, 0.01, -0.01, 0.01, -0.01, 0.02, -0.03, 0.03, 0.04, 0.02, -0.02, 0.04, 0.01, 0.01, 0.02, 0.01, 0, -0.03, 0.03, -0.02, -0.03, -0.02, 0.02, 0, -0.01, -0.02, -0.02, 0, -0.01, -0.03, 0.02, -0.01, 0.01, -0.08, 0.01, -0.04, -0.05, 0.02, -0.01, -0.03,
0.02, 0.01, -0.03, 0.01, 0.02, 0.03, 0.04, -0.04, 0.02, 0, 0.02, 0.02, 0.04, -0.04, -0.1, 0, 0.05, -0.01, 0.03, 0.05, 0.03, -0.02, 0.01, 0.02, -0.05, 0.01, 0, 0.05, -0.01, 0.03, -0.01, 0, 0.04, 0, 0, 0.08, 0.01, 0, -0.04, -0.03, 0, -0.02, -0.01, 0.02, 0.03,
0, -0.01, 0, 0, 0, 0.06, 0, 0, 0.01, -0.01, 0.01, 0.04, 0.07, -0.01, 0.01, 0, -0.01, -0.02, 0.01, 0.01, 0, 0.02, 0.01, 0, -0.02, 0.03, 0.02, 0.06, 0.02, -0.01, 0.03, 0.02, -0.02, 0.01, -0.01, 0.03, 0.05, 0.02, 0.01, 0, 0, 0.01, 0.03, -0.03, -0.01, -0.04, 0.03,
-0.02, 0.02, -0.02, -0.01, -0.02, 0.01, -0.04, 0.01, -0.04, 0.03, -0.02, -0.02, -0.01, -0.01, 0.07, 0.04, -0.01, 0.08, -0.04, -0.04, 0, 0, -0.01, -0.01, 0.03, -0.04, 0.02, -0.01, -0.04, 0.02, -0.07, -0.02, 0.02, -0.01, 0.02, 0.01, 0, 0.07, -0.01, 0.03, 0.01,
-0.05, 0.02, 0.02, -0.01, 0.02, 0.02, -0.03, -0.02, 0.03, -0.01, 0.02, 0, 0, 0.02, -0.01, -0.02, 0.05, 0.02, 0.01, 0.01, -0.03, -0.05, -0.03, 0.01, 0.03, -0.02, -0.01, -0.01, -0.01, 0.03, -0.01, -0.03, 0.02, -0.02, -0.03, -0.02, -0.01, -0.01, -0.01, 0, -0.01,
-0.04, -0.02, -0.02, -0.03, 0.04, 0.03, 0, -0.02, -0.01, -0.03, -0.01, -0.04, -0.04, 0.02, 0.01, -0.05, 0.04, -0.03, 0.01, -0.01, -0.03, 0.01, 0.01, 0.01, 0.02, -0.01, -0.02, -0.03, -0.01, -0.01, -0.01, -0.01, -0.03, 0, 0.01, -0.02, -0.01, -0.01, 0.01, 0, -0.04,
0.01, -0.01, 0.02, 0, 0, -0.01, 0, 0, 0.03, -0.01, -0.06, -0.04, -0.01, 0, 0.02, -0.05, -0.02, 0.02, -0.01, 0.01, 0.01, -0.01, -0.02, 0, 0.02, -0.01, -0.02, 0.04, -0.01, 0, -0.02, -0.04, -0.03, -0.03, 0, 0.03, -0.01, -0.02, 0, 0.01, -0.01, -0.04, 0.01, -0.03,
0.01, 0.03, 0, -0.02, 0, -0.04, -0.02, -0.02, 0.03, -0.02, 0.05, 0.02, 0.03, -0.02, -0.05, -0.01, 0.02, -0.04, 0.02, 0.01, -0.03, 0.01, 0.02, 0, 0.04, 0, -0.01, 0.02, 0.01, 0.02, 0.02, -0.02, 0.04, -0.01, 0, -0.01, 0, 0.01, -0.02, -0.04, 0.06, 0.01, 0, 0.01,
-0.02, 0.02, 0.05, 0, 0.03, -0.02, 0.02, -0.03, -0.02, 0.01, 0, 0.06, -0.01, 0, -0.02, -0.02, 0.01, -0.01, 0, -0.03, 0.02, 0, -0.01, -0.02, -0.01, 0.03, -0.03, 0, 0, 0, -0.03, -0.06, 0.04, 0.02, -0.03, -0.06, -0.03, -0.01, -0.03, -0.02, -0.04, 0.01, 0, -0.01,
0.02, -0.01, 0.03, 0.02, -0.02, -0.01, -0.02, -0.03, -0.01, 0.01, -0.04, 0.04, 0.03, 0.02, 0, -0.07, -0.02, -0.01, 0, 0.03, -0.01, -0.03, 0, 0.03, 0, -0.01, 0.02, 0.01, 0.02, -0.03, 0, 0.01, -0.02, 0.04, -0.04, 0, -0.05, 0, -0.02, -0.01, 0.03, 0.01, 0, -0.02,
0, -0.05, 0.01, -0.01, 0, -0.08, -0.01, -0.02, 0.02, 0.01, -0.01, -0.01, -0.01, 0, 0, -0.01, -0.03, 0, 0, -0.02, 0.05, -0.03, 0.02, 0.01, -0.02, 0.01, 0.01, 0, 0.01, -0.01, 0, -0.04, -0.06, 0.03, -0.02, 0, -0.02, 0.01, 0.03, 0.03, -0.03, -0.01, 0, 0, 0.01,
-0.02, -0.01, -0.01, -0.03, -0.02, 0.03, -0.02, 0.03, 0.01, 0.04, -0.04, 0.02, 0.02, 0.02, 0.03, 0, 0.06, -0.01, 0.02, -0.01, 0.01, -0.01, -0.01, -0.03, -0.01, 0.02, 0.01, 0.01, 0, -0.02, 0.03, 0.02, -0.01, -0.02, 0.01, 0.01, 0.04, -0.01, -0.05, 0, -0.01, 0,
0.03, -0.01, 0.02, 0.02, -0.04, 0.01, -0.03, -0.02, 0, 0.02, 0, -0.01, 0.02, 0.01, 0.04, -0.04, 0, -0.01, -0.02, 0, -0.02, 0.01, -0.02, 0, 0, 0.03, 0.04, -0.01, 0, 0, 0.03, -0.02, 0.01, -0.02, 0, -0.03, 0.04, 0, 0.01, 0.04, 0, 0.03, -0.02, 0.01, 0.01, -0.02,
0.02, -0.05, 0.03, -0.02, -0.01, 0.01, -0.01, 0.02, 0.04, 0.02, 0, -0.02, 0.02, -0.01, -0.03, -0.06, -0.01, -0.01, -0.04, 0.01, -0.01, -0.01, -0.01, -0.02, 0.03, -0.03, 0.05, 0, -0.01, -0.03, 0.03, 0.01, -0.01, -0.01, 0, 0.01, 0.01, 0.02, -0.01, 0.02, -0.02,
-0.03, 0.03, -0.02, 0.01, 0, -0.03, 0.02, 0.02, -0.02, 0.01, 0.02, -0.01, 0.02, 0, 0.02, 0.01, 0, 0.05, -0.03, 0.01, 0.03, 0.04, 0.01, 0.01, -0.01, 0.02, -0.03, 0.02, 0.01, 0, -0.01, -0.03, -0.01, 0.02, 0.03, 0, 0.03, 0.02, 0, 0.01, 0.01, 0.02, 0.01, 0.02, 0.03,
0.01, -0.03, 0.02, 0.01, 0.02, 0.03, -0.01, 0.01, -0.03, -0.01, -0.02, 0.01, 0, 0, -0.01, -0.02, -0.01, -0.01, 0.01, 0.06, 0.01, 0, -0.01, 0.01, 0, 0, -0.01, -0.01, 0, -0.02, -0.02, -0.01, -0.02, -0.01, -0.05, -0.02, 0.03, 0.02, 0, 0.03, -0.03, -0.03, 0.03, 0,
0.02, -0.03, 0.04, -0.04, 0, -0.04, 0.04, 0.01, -0.03, 0.01, -0.02, -0.01, -0.04, 0.02, -0.01, 0.01, 0.01, 0.02, -0.02, 0.03, -0.01, 0, 0.01, 0, 0.02, 0.01, 0.01, 0.03, -0.06, 0.02, 0, -0.02, 0, 0.04, -0.03, 0, 0, -0.02, 0.06, 0.01, -0.03, -0.02, -0.01, -0.03,
-0.04, 0.04, 0.03, -0.02, 0, 0.03, -0.04, -0.01, -0.02, -0.02, -0.01, 0.02, 0.02, 0.01, 0.01, 0.01, -0.02, -0.02, -0.03, -0.01, 0.01, 0, 0, 0, 0.02, -0.04, -0.01, -0.01, 0.04, -0.01, 0.01, -0.01, 0.01, -0.03, 0.01, -0.01, 0, -0.01, 0.01, 0, 0.01, -0.04, 0.01, 0,
0, 0, 0, 0.02, 0.04, 0.01, 0.01, -0.01, -0.02, 0, 0, 0.01, -0.01, 0.01, -0.01, 0, 0.04, -0.01, -0.02, -0.01, -0.01, -0.01, 0, 0, 0.01, 0.01, 0.04, -0.01, -0.01, 0, -0.03, -0.01, 0.01, -0.01, -0.02, 0.01, -0.02, 0.01, -0.03, 0.02, 0, 0.03, 0.01, -0.03, -0.01,
-0.01, 0.02, 0.01, 0, -0.01, 0.03, -0.04, 0.01, -0.01, -0.03, -0.02, 0.02, -0.01, 0, -0.01, 0.02, 0.02, 0.01, 0.03, 0, -0.03, 0, 0.02, -0.03, -0.01, 0.01, 0.06, -0.01, -0.02, 0.01, 0, 0.04, -0.04, 0.01, -0.02, 0, -0.04, 0, 0.02, 0.02, -0.02, 0.04, -0.01, 0.01,
0, 0.03, -0.03, 0.04, -0.01, -0.02, -0.02, 0.01, -0.02, -0.01, 0, -0.03, -0.01, 0.02, -0.01, -0.05, 0.02, 0.01, 0, -0.02, -0.03, 0, 0, 0, -0.01, 0.02, 0, 0.02, 0.03, -0.02, 0.02, -0.02, 0.02, -0.01, 0.02, 0, -0.07, -0.01, 0.01, 0.01, -0.01, 0.02, 0, -0.01, 0,
0.01, 0.01, -0.06, 0.04, 0, -0.04, -0.01, -0.03, -0.04, -0.01, -0.01, 0.03, -0.02, -0.01, 0.02, 0, -0.04, 0.01, 0.01, -0.01, 0.02, 0.01, 0.03, -0.01, 0, -0.02, -0.02, -0.01, 0.04, -0.02, 0.06, 0, 0, -0.02, 0, 0.01, 0, -0.02, 0.02, 0.02, -0.06, -0.02, 0, 0.02,
0.01, -0.01, 0, 0, -0.01, 0.01, -0.04, -0.01, -0.01, 0.01, -0.02, -0.03, 0.01, 0.03, -0.01, -0.01, 0, -0.01, 0, -0.01, 0.05, 0.02, 0, 0, 0.02, -0.01, 0.02, -0.03, -0.01, -0.02, 0.02, 0, 0.01, -0.06, -0.01, 0.01, 0.01, 0.02, 0.02, -0.02, 0.03, 0.01, -0.01, -0.01,
0, 0, 0.03, 0.05, 0.05, -0.01, 0.01, -0.03, 0, -0.01, -0.01, 0, -0.02, 0.02, 0, 0.02, -0.01, 0.01, -0.02, 0.01, 0, -0.02, 0.02, 0.01, -0.03, 0.03, -0.04, -0.02, -0.01, 0.01, -0.04, -0.03, -0.02, -0.03, 0.01, 0, 0, -0.02, -0.01, 0.02, 0.01, -0.01, 0.01, 0.03,
-0.01, -0.02, -0.01, 0, 0, -0.03, 0, 0.02, 0.03, 0.01, -0.01, 0.02, 0.04, -0.04, 0.02, 0.01, -0.02, -0.01, 0.03, -0.04, -0.01, 0, 0.01, 0.01, 0, 0.03, 0.05, 0, 0, 0.05, 0.01, -0.01, 0, -0.01, 0, -0.01, -0.01, 0.03, -0.01, 0.02, 0, 0, -0.01, 0, -0.02, -0.02,
0.05, -0.02, -0.01, -0.01, -0.01, 0.02, 0, -0.01, 0, 0, 0, -0.02, -0.04, 0.01, 0.01, -0.01, 0.01, 0, -0.06, -0.01, -0.04, -0.03, 0.01, 0, -0.01, 0.03, -0.04, -0.01, 0, 0.04, 0.03]";

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();
        private static readonly IndexingPolicy OrdinalRrfIndexingPolicy = CreateOrdinalRrfIndexingPolicy();

        private static readonly VectorEmbeddingPolicy OrdinalRrfEmbeddingPolicy = new VectorEmbeddingPolicy(
            new Collection<Embedding>
            {
                new Embedding
                {
                    Path = "/vector",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Euclidean,
                    Dimensions = 2,
                },
            });

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task SanityTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new int[][] { new[] { 2, 57, 85 }, new[] { 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (c.index = 2)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<int>{ 2 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<int>{ 2 },
                    partitionKey: new PartitionKey(2)),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new int[][] { new[] { 2, 57, 85 }, new[] { 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')
                    OFFSET 1 LIMIT 5",
                    new int[][] { new[] { 57, 85 }, new[] { 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 5 LIMIT 10",
                    new List<int>{ 2, 24, 76, 54, 80, 22, 85, 57 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 98, 97, 93, 90, 83, 74, 55, 47, 75 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 0 LIMIT 11",
                    new List<int>{ 51, 98, 97, 93, 90, 83, 74, 55, 47, 75, 49 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), VectorDistance(c.vector, {SampleVector}))",
                    new List<int>{ 98, 55, 75, 93, 26, 49, 24, 90, 51, 41 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 98, 55, 75, 93, 26, 49, 24, 90, 51, 41 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), VectorDistance(c.image, {SampleVector}), VectorDistance(c.backup_image, {SampleVector}), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 98, 93, 55, 90, 97, 51, 74, 26, 41, 83 }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task FullTextScoreProjectionAndFilterPredicateTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new int[][] { new[] { 2, 57, 85 }, new[] { 2, 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (c.index = 2) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<int>{ 2 },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<int>{ 2 },
                    ValidationMode.TextOrTitle,
                    new PartitionKey(2)),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new int[][] { new[] { 2, 57, 85 }, new[] { 2, 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')
                    OFFSET 1 LIMIT 5",
                    new int[][] { new[] { 57, 85 }, new[] { 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 5 LIMIT 10",
                    new List<int>{ 2, 24, 76, 54, 80, 22, 85, 57 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 51, 98, 97, 93, 90, 83, 74, 55, 47, 75 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 0 LIMIT 11",
                    new List<int>{ 51, 98, 97, 93, 90, 83, 74, 55, 47, 75, 49 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), VectorDistance(c.vector, {SampleVector}))",
                    new List<int>{ 98, 55, 75, 93, 26, 49, 24, 90, 51, 41 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 98, 55, 75, 93, 26, 49, 24, 90, 51, 41 }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), VectorDistance(c.image, {SampleVector}), VectorDistance(c.backup_image, {SampleVector}), FullTextScore(c.text, 'United States'))",
                    new List<int>{ 98, 93, 55, 90, 97, 51, 74, 26, 41, 83 }),
            };

            await this.RunTests(testCases, enableFullTextPreviewFeatures: true);
        }

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task FullTextScoreProjectionAndFilterPredicateWeightedRRFTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, 1])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [10, 10])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [0.1, 0.1])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80 },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [-1, -1])",
                    new List<int>{ 77, 85, 75, 76, 51, 22, 2, 24, 80, 57, 49, 61, 54 },
                    ValidationMode.TextOrTitleOrUnitedStates),
            };

            await this.RunTests(testCases, enableFullTextPreviewFeatures: true);
        }

        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task WeightedRankFusionTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, 1])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [10, 10])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80, 22, 85, 57 }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [0.1, 0.1])",
                    new List<int>{ 51, 49, 61, 75, 77, 2, 24, 76, 54, 80 }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [-1, -1])",
                    new List<int>{ 77, 85, 75, 76, 51, 22, 2, 24, 80, 57, 49, 61, 54 }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        public async Task HybridSearchRrfOrdinalRankingProducesExpectedRanks()
        {
            // One document matches the full-text terms while the other 999 receive the same lexical score.
            // Unique vector distances make changes in the tied documents' ordinal ranks observable.
            IEnumerable<string> documents = Enumerable.Range(0, RrfDocumentCount)
                .Select(index => $@"{{
                    ""id"": ""{index}"",
                    ""index"": {index},
                    ""text"": ""{(index == RrfLexicalMatchIndex ? "unique target" : "nonmatch")}"",
                    ""vector"": [{index}, 0]
                }}");

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documents,
                query: ValidateRrfOrdinalRanking,
                partitionKey: "/index",
                indexingPolicy: OrdinalRrfIndexingPolicy,
                vectorEmbeddingPolicy: OrdinalRrfEmbeddingPolicy);
        }

        [TestMethod]
        public async Task HybridSearchRrfTiesAreBrokenByRid()
        {
            IEnumerable<string> documents = new[]
            {
                @"{ ""id"": ""left"", ""index"": 0, ""vector"": [0, 0] }",
                @"{ ""id"": ""right"", ""index"": 1, ""vector"": [10, 0] }",
            };

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documents,
                query: ValidateRrfRidTieBreak,
                partitionKey: "/index",
                indexingPolicy: OrdinalRrfIndexingPolicy,
                vectorEmbeddingPolicy: OrdinalRrfEmbeddingPolicy);
        }

        private static async Task ValidateRrfRidTieBreak(
            Container container,
            IReadOnlyList<CosmosObject> _)
        {
            const string ridQuery = "SELECT c._rid AS Rid FROM c";
            IReadOnlyList<RidDocument> documentsByRid = await QueryWithoutContinuationTokensAsync<RidDocument>(
                container,
                ridQuery);
            Assert.AreEqual(2, documentsByRid.Count);
            string[] expectedRids = documentsByRid
                .Select(document => document.Rid)
                .OrderByDescending(rid => Documents.ResourceId.Parse(rid).Document)
                .ThenByDescending(rid => rid, StringComparer.Ordinal)
                .ToArray();

            // The component queries produce inverse ranks, so equal weights give both documents the same RRF score.
            const string hybridQuery = @"
                SELECT c._rid AS Rid
                FROM c
                ORDER BY RANK RRF(VectorDistance(c.vector, [0, 0]), VectorDistance(c.vector, [10, 0]))";
            IReadOnlyList<RidDocument> actual = await QueryWithoutContinuationTokensAsync<RidDocument>(
                container,
                hybridQuery);

            CollectionAssert.AreEqual(
                expectedRids,
                actual.Select(document => document.Rid).ToArray(),
                "Documents with equal RRF scores were not ordered by decoded document RID descending.");

            // Both documents have identical distances to the midpoint in both components.
            const string tiedComponentQuery = @"
                SELECT c._rid AS Rid
                FROM c
                ORDER BY RANK RRF(VectorDistance(c.vector, [5, 0]), VectorDistance(c.vector, [5, 0]))";
            IReadOnlyList<RidDocument> tiedComponentResults = await QueryWithoutContinuationTokensAsync<RidDocument>(
                container,
                tiedComponentQuery);

            CollectionAssert.AreEqual(
                expectedRids,
                tiedComponentResults.Select(document => document.Rid).ToArray(),
                "Documents with tied component scores were not ranked by decoded document RID descending.");
        }

        private static async Task ValidateRrfOrdinalRanking(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            const string hybridQuery = @"
                SELECT TOP 1000 c.index AS Index
                FROM c
                ORDER BY RANK RRF(FullTextScore(c.text, 'unique target'), VectorDistance(c.vector, [0, 0]))";

            int[] ordinalOrder = await QueryRrfOrderAsync(container, hybridQuery);
            int[] repeatedOrdinalOrder = await QueryRrfOrderAsync(container, hybridQuery);

            CollectionAssert.AreEqual(
                ordinalOrder,
                repeatedOrdinalOrder,
                "Repeated ordinal-ranking queries returned different orders.");
            Assert.AreEqual(RrfDocumentCount, ordinalOrder.Length);
            Assert.AreEqual(RrfDocumentCount, ordinalOrder.Distinct().Count());
            Assert.AreEqual(
                OrdinalRrfLexicalMatchRank,
                Array.IndexOf(ordinalOrder, RrfLexicalMatchIndex) + 1);
        }

        private static async Task<int[]> QueryRrfOrderAsync(
            Container container,
            string query)
        {
            return (await QueryWithoutContinuationTokensAsync<OrdinalRrfDocument>(
                container,
                query))
                .Select(document => document.Index)
                .ToArray();
        }

        [TestMethod]
        public async Task StatisticsCacheSkipsStatisticsRequestsTests()
        {
            CosmosArray documentsArray = await LoadDocuments();
            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documentsArray.Select(document => document.ToString()),
                query: (container, _) => RunStatisticsCacheTestsAsync(container),
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunStatisticsCacheTestsAsync(Container container)
        {
            const string rankQuery = @"
                SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                FROM c
                ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))";
            const string regroupedQuery = @"
                SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                FROM c
                ORDER BY RANK RRF(FullTextScore(c.text, 'United States'), FullTextScore(c.title, 'John'))";
            const string subsetQuery = @"
                SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                FROM c
                ORDER BY RANK FullTextScore(c.text, 'United States')";

            Assert.IsTrue((await container.GetFeedRangesAsync()).Count > 1, "The container must have multiple partitions.");

            using StatisticsCacheClient uncached = new StatisticsCacheClient(null);
            using StatisticsCacheClient cached = new StatisticsCacheClient(TimeSpan.FromMinutes(10));
            using StatisticsCacheClient otherCached = new StatisticsCacheClient(TimeSpan.FromMinutes(10));

            // Warm up: a cold cache collects statistics like an uncached client.
            StatisticsCacheRun warmUpExpected = await uncached.RunAsync(container, rankQuery, FullTextScoreScope.Global);
            Assert.IsTrue(warmUpExpected.StatisticsRequestCount > 0, "An uncached Global query must collect statistics.");

            StatisticsCacheRun warmUp = await cached.RunAsync(container, rankQuery, FullTextScoreScope.Global);
            Assert.AreEqual(warmUpExpected.StatisticsRequestCount, warmUp.StatisticsRequestCount, "Warm-up");
            CollectionAssert.AreEqual(warmUpExpected.Indices, warmUp.Indices, "Warm-up");

            // Every run uses a new container handle, so hits also show that handles of one client share its cache.
            List<StatisticsCacheCase> cases = new List<StatisticsCacheCase>
            {
                new StatisticsCacheCase("Repeated query", cached, rankQuery, FullTextScoreScope.Global, expectHit: true),
                new StatisticsCacheCase("Regrouped components", cached, regroupedQuery, FullTextScoreScope.Global, expectHit: true),
                new StatisticsCacheCase("Subset of paths and terms", cached, subsetQuery, FullTextScoreScope.Global, expectHit: true),
                new StatisticsCacheCase("Local after Global warm-up", cached, rankQuery, FullTextScoreScope.Local, expectHit: false),
                new StatisticsCacheCase("Separate client", otherCached, rankQuery, FullTextScoreScope.Global, expectHit: false),
            };

            for (int index = 0; index < cases.Count; index++)
            {
                StatisticsCacheCase testCase = cases[index];
                Trace.WriteLine($"Case: {testCase.Name}");

                StatisticsCacheRun expected = await uncached.RunAsync(container, testCase.Query, testCase.Scope);
                StatisticsCacheRun actual = await testCase.Client.RunAsync(container, testCase.Query, testCase.Scope);

                Assert.IsTrue(expected.StatisticsRequestCount > 0, testCase.Name);
                Assert.AreEqual(testCase.ExpectHit ? 0 : expected.StatisticsRequestCount, actual.StatisticsRequestCount, testCase.Name);
                CollectionAssert.AreEqual(expected.Indices, actual.Indices, testCase.Name);
                Assert.AreEqual(
                    testCase.ExpectHit ? expected.RequestCharge - expected.StatisticsRequestCharge : expected.RequestCharge,
                    actual.RequestCharge,
                    0.01,
                    testCase.Name);
            }

            // Writes do not invalidate cached counts; reusing them within the configured age is accepted staleness.
            await container.CreateItemAsync(
                new { id = Guid.NewGuid().ToString(), index = 1000, title = "John", text = "John lived in the United States." },
                new PartitionKey(1000));
            StatisticsCacheRun afterWrite = await cached.RunAsync(container, rankQuery, FullTextScoreScope.Global);
            Assert.AreEqual(0, afterWrite.StatisticsRequestCount, "After write");
        }
        private async Task RunTests(IEnumerable<SanityTestCase> testCases, bool enableFullTextPreviewFeatures = false)
        {
            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: async (container, _) =>
                {
                    bool isEmulator = this.Client.Endpoint.IsLoopback;
                    AccountProperties account = await this.Client.ReadAccountAsync();
                    IDictionary<string, object> queryEngineConfiguration = new Dictionary<string, object>(account.QueryEngineConfiguration);
                    QueryPartitionProvider provider = await this.Client.DocumentClient.QueryPartitionProvider;
                    if (enableFullTextPreviewFeatures)
                    {
                        queryEngineConfiguration["queryEnableFullTextPreviewFeatures"] = true;
                        provider.Update(queryEngineConfiguration);
                    }
                    else if (!isEmulator && !queryEngineConfiguration.ContainsKey("queryEnableFullTextPreviewFeatures"))
                    {
                        queryEngineConfiguration["queryEnableFullTextPreviewFeatures"] = false;
                        provider.Update(queryEngineConfiguration);
                    }

                    await RunTests(container, testCases, provider, queryEngineConfiguration, isEmulator);
                },
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunTests(
            Container container,
            IEnumerable<SanityTestCase> testCases,
            QueryPartitionProvider provider,
            IDictionary<string, object> queryEngineConfiguration,
            bool isEmulator)
        {
            foreach (FullTextScoreScope fullTextScoreScope in new[]{ FullTextScoreScope.Local, FullTextScoreScope.Global })
            {
                foreach (SanityTestCase testCase in testCases)
                {
                    QueryRequestOptions testRequestOptions = new QueryRequestOptions
                    {
                        FullTextScoreScope = fullTextScoreScope,
                        EnableOptimisticDirectExecution = false,
                    };

                    if (testCase.PartitionKey.HasValue)
                    {
                        testRequestOptions.PartitionKey = testCase.PartitionKey;
                    }

                    Dictionary<int, double> expectedScores = null;
                    if (testCase.ComputeExpectedOrderingOnLiveAccount && !isEmulator)
                    {
                        HybridSearchQueryInfo queryInfo = await GetHybridSearchQueryInfoAsync(container, provider, testCase.Query, testRequestOptions);
                        IDictionary<string, object> referenceConfiguration = new Dictionary<string, object>(queryEngineConfiguration)
                        {
                            ["queryEnableFullTextPreviewFeatures"] = true,
                        };

                        provider.Update(referenceConfiguration);
                        try
                        {
                            expectedScores = await ComputeExpectedRrfScoresAsync(container, queryInfo, testCase.Query, testRequestOptions);
                        }
                        finally
                        {
                            provider.Update(queryEngineConfiguration);
                        }
                    }

                    List<TextDocument> result = await RunQueryCombinationsAsync<TextDocument>(
                        container,
                        testCase.Query,
                        queryRequestOptions: testRequestOptions,
                        queryDrainingMode: QueryDrainingMode.HoldState);

                    if (testCase.ValidationMode != ValidationMode.None)
                    {
                        Assert.IsTrue(
                            result.All(document =>
                                (testCase.ValidationMode.HasFlag(ValidationMode.TitleScore) && document.TitleScore > 0) ||
                                (testCase.ValidationMode.HasFlag(ValidationMode.TextScore) && document.TextScore > 0) ||
                                (testCase.ValidationMode.HasFlag(ValidationMode.UnitedStatesScore) && document.UnitedStatesScore > 0)),
                            $"Every document must have a positive score for at least one {testCase.ValidationMode} term.");
                    }

                    IEnumerable<int> actual = result.Select(document => document.Index);

                    if (expectedScores != null)
                    {
                        ValidateRrfOrdering(testCase.Query, expectedScores, actual.ToArray());
                        continue;
                    }

                    if (!testCase.ExpectedOrderings.Any(expected => expected.SequenceEqual(actual)))
                    {
                        Trace.WriteLine($"Query: {testCase.Query}");
                        Trace.WriteLine($"Actual: {string.Join(", ", actual)}");

                        string errorMessage = @"The query results did not match the expected results." +
                            "Please set HybridSearchCrossPartitionQueryPipelineStage.HybridSearchDebugTraceHelpers.Enabled = true to debug.";
                        Assert.Fail(errorMessage);
                    }
                }
            }
        }

        private static async Task<HybridSearchQueryInfo> GetHybridSearchQueryInfoAsync(
            Container container,
            QueryPartitionProvider provider,
            string queryText,
            QueryRequestOptions requestOptions)
        {
            ContainerProperties containerProperties = await container.ReadContainerAsync();
            return provider.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: JsonConvert.SerializeObject(new SqlQuerySpec(queryText)),
                partitionKeyDefinition: containerProperties.PartitionKey,
                vectorEmbeddingPolicy: containerProperties.VectorEmbeddingPolicy,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: requestOptions.PartitionKey.HasValue,
                allowDCount: true,
                useSystemPrefix: false,
                hybridSearchSkipOrderByRewrite: !requestOptions.IsHybridSearchQueryPlanOptimizationDisabled,
                geospatialType: GeospatialType.Geography).Result.HybridSearchQueryInfo;
        }

        private static async Task<Dictionary<int, double>> ComputeExpectedRrfScoresAsync(
            Container container,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            string queryText,
            QueryRequestOptions requestOptions)
        {
            SqlQuery query = SqlQueryParser.Parse(queryText);
            SqlFunctionCallScalarExpression rrf = (SqlFunctionCallScalarExpression)query.OrderByClause.OrderByItems.Single().Expression;
            SqlArrayCreateScalarExpression weights = rrf.Arguments.Last() as SqlArrayCreateScalarExpression;
            int componentCount = rrf.Arguments.Length - (weights == null ? 0 : 1);
            Assert.AreEqual(componentCount, hybridSearchQueryInfo.ComponentQueryInfos.Count);
            bool useDefaultComponentWeight = hybridSearchQueryInfo.ComponentWeights == null || hybridSearchQueryInfo.ComponentWeights.Count == 0;
            Assert.AreEqual(useDefaultComponentWeight, weights == null);
            Dictionary<int, double> scores = new Dictionary<int, double>();
            HashSet<int> expectedIndices = null;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                SqlFunctionCallScalarExpression component = (SqlFunctionCallScalarExpression)rrf.Arguments[componentIndex];
                QueryInfo componentQueryInfo = hybridSearchQueryInfo.ComponentQueryInfos[componentIndex];
                SortOrder sortOrder = componentQueryInfo.HasOrderBy ? componentQueryInfo.OrderBy[0] : SortOrder.Descending;
                double scoreSign = 1;
                if (weights != null)
                {
                    double queryWeight = ReadNumericLiteral(weights.Items[componentIndex]);
                    Assert.AreEqual(Math.Abs(queryWeight), hybridSearchQueryInfo.ComponentWeights[componentIndex]);
                    if (queryWeight < 0)
                    {
                        if (componentQueryInfo.HasOrderBy)
                        {
                            Assert.AreEqual(SortOrder.Ascending, sortOrder);
                        }
                        else
                        {
                            scoreSign = -1;
                        }
                    }
                }

                string componentQuery = $"SELECT c.index AS Index, c._rid AS Rid, {component} AS Score " +
                    $"{query.FromClause} {query.WhereClause} ORDER BY RANK {component}";
                List<ComponentScoreDocument> documents = new List<ComponentScoreDocument>();
                using (FeedIterator<ComponentScoreDocument> iterator = container.GetItemQueryIterator<ComponentScoreDocument>(
                    componentQuery,
                    requestOptions: requestOptions))
                {
                    while (iterator.HasMoreResults)
                    {
                        documents.AddRange(await iterator.ReadNextAsync());
                    }
                }

                HashSet<int> indices = new HashSet<int>(documents.Select(document => document.Index));
                Assert.AreEqual(documents.Count, indices.Count, "A component must return each eligible document once.");
                if (expectedIndices == null)
                {
                    expectedIndices = indices;
                }
                else
                {
                    CollectionAssert.AreEquivalent(expectedIndices.ToArray(), indices.ToArray(), "Components must rank the same eligible documents.");
                }

                double ComponentScore(ComponentScoreDocument document) => scoreSign * (document.Score ?? UndefinedComponentScore);
                ulong DocumentRid(ComponentScoreDocument document) => Documents.ResourceId.Parse(document.Rid).Document;
                IOrderedEnumerable<ComponentScoreDocument> ordered = sortOrder == SortOrder.Ascending
                    ? documents.OrderBy(ComponentScore).ThenBy(DocumentRid)
                    : documents.OrderByDescending(ComponentScore).ThenByDescending(DocumentRid);
                double weight = useDefaultComponentWeight ? 1 : hybridSearchQueryInfo.ComponentWeights[componentIndex];
                int rank = 1;
                // Ordinal ranking assigns consecutive ranks, with component ties ordered by document RID.
                foreach (ComponentScoreDocument document in ordered)
                {
                    scores.TryGetValue(document.Index, out double score);
                    scores[document.Index] = score + (weight / (60 + rank));
                    rank++;
                }
            }

            return scores;
        }

        private static void ValidateRrfOrdering(
            string queryText,
            IReadOnlyDictionary<int, double> scores,
            IReadOnlyList<int> actual)
        {
            Assert.AreEqual(actual.Count, actual.Distinct().Count(), "Hybrid results must not contain duplicate documents.");
            Assert.IsTrue(actual.All(scores.ContainsKey), "Hybrid results must contain only eligible documents.");
            SqlQuery query = SqlQueryParser.Parse(queryText);
            IEnumerable<double> expected = scores.Values.OrderByDescending(score => score);
            if (query.OffsetLimitClause != null)
            {
                expected = expected
                    .Skip(checked((int)ReadNumericLiteral(query.OffsetLimitClause.OffsetSpec.OffsetExpression)))
                    .Take(checked((int)ReadNumericLiteral(query.OffsetLimitClause.LimitSpec.LimitExpression)));
            }

            if (query.SelectClause.TopSpec != null)
            {
                expected = expected.Take(checked((int)ReadNumericLiteral(query.SelectClause.TopSpec.TopExpresion)));
            }

            // Equal fused scores may appear in any order, including at TOP/OFFSET boundaries.
            CollectionAssert.AreEqual(
                expected.ToArray(),
                actual.Select(index => scores[index]).ToArray(),
                $"Hybrid results must follow the independently computed ordinal scores. Query: {queryText}");
        }

        private static double ReadNumericLiteral(SqlScalarExpression expression)
        {
            return expression switch
            {
                SqlLiteralScalarExpression { Literal: SqlNumberLiteral number } => Number64.ToDouble(number.Value),
                SqlUnaryScalarExpression { OperatorKind: SqlUnaryScalarOperatorKind.Minus } negative => -ReadNumericLiteral(negative.Expression),
                _ => throw new AssertFailedException($"Expected a numeric literal in the test query, not {expression}."),
            };
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

        private static IndexingPolicy CreateOrdinalRrfIndexingPolicy()
        {
            IndexingPolicy policy = new IndexingPolicy();
            policy.IncludedPaths.Add(new IncludedPath { Path = IndexingPolicy.DefaultPath });
            policy.VectorIndexes.Add(new VectorIndexPath
            {
                Path = "/vector",
                Type = VectorIndexType.Flat,
            });
            return policy;
        }

        private static SanityTestCase MakeSanityTest(
            string query,
            IReadOnlyList<int> expectedIndices,
            ValidationMode validationMode = ValidationMode.None,
            PartitionKey? partitionKey = null)
        {
            return MakeSanityTest(query, new[] { expectedIndices }, validationMode, partitionKey);
        }

        private static SanityTestCase MakeSanityTest(
            string query,
            IReadOnlyList<IReadOnlyList<int>> expectedOrderings,
            ValidationMode validationMode = ValidationMode.None,
            PartitionKey? partitionKey = null)
        {
            return new SanityTestCase
            {
                Query = query,
                ExpectedOrderings = expectedOrderings,
                ValidationMode = validationMode,
                PartitionKey = partitionKey,
                ComputeExpectedOrderingOnLiveAccount = SqlQueryParser.Parse(query).OrderByClause.OrderByItems.Single().Expression
                    is SqlFunctionCallScalarExpression function && function.Name.Value == "RRF",
            };
        }

        private sealed class SanityTestCase
        {
            public string Query { get; init; }

            public IReadOnlyList<IReadOnlyList<int>> ExpectedOrderings { get; init; }

            public bool ComputeExpectedOrderingOnLiveAccount { get; init; }

            public PartitionKey? PartitionKey { get; init; }

            public ValidationMode ValidationMode { get; init; }
        }

        [Flags]
        private enum ValidationMode
        {
            None = 0,
            TitleScore = 1 << 0,
            TextScore = 1 << 1,
            UnitedStatesScore = 1 << 2,
            TextOrTitle = TextScore | TitleScore,
            TextOrTitleOrUnitedStates = TitleScore | TextScore | UnitedStatesScore,
        }

        private sealed class TextDocument
        {
            public int Index { get; set; }

            public string Title { get; set; }

            public string Text { get; set; }

            public double TitleScore { get; set; }

            public double TextScore { get; set; }

            public double UnitedStatesScore { get; set; }
        }

        private sealed class ComponentScoreDocument
        {
            public int Index { get; set; }

            public string Rid { get; set; }

            public double? Score { get; set; }
        }

        private sealed class OrdinalRrfDocument
        {
            public int Index { get; set; }
        }

        private sealed class RidDocument
        {
            public string Rid { get; set; }
        }

        private sealed class StatisticsCacheCase
        {
            public StatisticsCacheCase(string name, StatisticsCacheClient client, string query, FullTextScoreScope scope, bool expectHit)
            {
                this.Name = name;
                this.Client = client;
                this.Query = query;
                this.Scope = scope;
                this.ExpectHit = expectHit;
            }

            public string Name { get; }

            public StatisticsCacheClient Client { get; }

            public string Query { get; }

            public FullTextScoreScope Scope { get; }

            public bool ExpectHit { get; }
        }

        private sealed class StatisticsCacheRun
        {
            public StatisticsCacheRun(List<int> indices, double requestCharge, int statisticsRequestCount, double statisticsRequestCharge)
            {
                this.Indices = indices;
                this.RequestCharge = requestCharge;
                this.StatisticsRequestCount = statisticsRequestCount;
                this.StatisticsRequestCharge = statisticsRequestCharge;
            }

            public List<int> Indices { get; }

            public double RequestCharge { get; }

            public int StatisticsRequestCount { get; }

            public double StatisticsRequestCharge { get; }
        }

        private sealed class StatisticsCacheClient : IDisposable
        {
            private readonly StatisticsRequestCounter counter = new StatisticsRequestCounter();
            private readonly CosmosClient client;

            public StatisticsCacheClient(TimeSpan? fullTextStatisticsCacheMaxAge)
            {
                CosmosClientOptions options = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    FullTextStatisticsCacheMaxAge = fullTextStatisticsCacheMaxAge,
                };
                options.CustomHandlers.Add(this.counter);
                this.client = SDK.EmulatorTests.TestCommon.CreateCosmosClient(options);
            }

            public async Task<StatisticsCacheRun> RunAsync(Container container, string query, FullTextScoreScope scope)
            {
                this.counter.Reset();
                Container handle = this.client.GetContainer(container.Database.Id, container.Id);
                QueryRequestOptions requestOptions = new QueryRequestOptions { FullTextScoreScope = scope };

                List<int> indices = new List<int>();
                double requestCharge = 0;
                using FeedIterator<TextDocument> iterator = handle.GetItemQueryIterator<TextDocument>(query, requestOptions: requestOptions);
                while (iterator.HasMoreResults)
                {
                    FeedResponse<TextDocument> page = await iterator.ReadNextAsync();
                    requestCharge += page.RequestCharge;
                    indices.AddRange(page.Select(document => document.Index));
                }

                return new StatisticsCacheRun(indices, requestCharge, this.counter.Count, this.counter.RequestCharge);
            }

            public void Dispose()
            {
                this.client.Dispose();
            }
        }

        private sealed class StatisticsRequestCounter : RequestHandler
        {
            private readonly object gate = new object();

            public int Count { get; private set; }

            public double RequestCharge { get; private set; }

            public void Reset()
            {
                lock (this.gate)
                {
                    this.Count = 0;
                    this.RequestCharge = 0;
                }
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                bool isStatisticsRequest = await IsStatisticsRequestAsync(request);
                ResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (isStatisticsRequest)
                {
                    lock (this.gate)
                    {
                        this.Count++;
                        this.RequestCharge += response.Headers.RequestCharge;
                    }
                }

                return response;
            }

            private static async Task<bool> IsStatisticsRequestAsync(RequestMessage request)
            {
                if (request.Content == null)
                {
                    return false;
                }

                using MemoryStream body = new MemoryStream();
                await request.Content.CopyToAsync(body);
                request.Content.Position = 0;
                return Encoding.UTF8.GetString(body.ToArray()).Contains("_FullTextWordCount");
            }
        }
        private static class FieldNames
        {
            public const string Items = "items";
            public const string Index = "index";
            public const string Title = "title";
            public const string Text = "text";
            public const string Rid = "_rid";
        }
    }
}
