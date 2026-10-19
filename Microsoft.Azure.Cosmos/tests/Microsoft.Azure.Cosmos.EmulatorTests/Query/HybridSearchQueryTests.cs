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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private const string CollectionDataPath = "Documents\\text-3properties-1536dimensions-100documents.json";

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

        [TestMethod]
        public async Task SanityTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (c.index = 2)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } },
                    partitionKey: new PartitionKey(2)),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')
                    OFFSET 1 LIMIT 5",
                    new List<List<int>>{ new List<int>{ 57, 85 }, new List<int>{ 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 5 LIMIT 10",
                    new List<List<int>>{
                        new List<int>{ 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 0 LIMIT 11",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), VectorDistance(c.vector, {SampleVector}))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), VectorDistance(c.image, {SampleVector}), VectorDistance(c.backup_image, {SampleVector}), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        public async Task FullTextScoreProjectionAndFilterPredicateTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (c.index = 2) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } },
                    ValidationMode.TextOrTitle,
                    new PartitionKey(2)),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0)
                    ORDER BY RANK FullTextScore(c.title, 'John')
                    OFFSET 1 LIMIT 5",
                    new List<List<int>>{ new List<int>{ 57, 85 }, new List<int>{ 85, 57 } },
                    ValidationMode.TextOrTitle),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 5 LIMIT 10",
                    new List<List<int>>{
                        new List<int>{ 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 24, 77, 76, 80, 2, 22, 85, 57 },
                    },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 0 LIMIT 11",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), VectorDistance(c.vector, {SampleVector}))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), VectorDistance(c.image, {SampleVector}), VectorDistance(c.backup_image, {SampleVector}), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
            };

            await this.RunTests(testCases, enableFullTextPreviewFeatures: true);
        }

        [TestMethod]
        public async Task FullTextScoreProjectionAndFilterPredicateWeightedRRFTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0) 
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, 1])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                    },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [10, 10])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [0.1, 0.1])",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } },
                    ValidationMode.TextOrTitleOrUnitedStates),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text, FullTextScore(c.title, 'John') as TitleScore, FullTextScore(c.text, 'John') as TextScore, FullTextScore(c.text, 'United States') as UnitedStatesScore
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')) AND (FullTextScore(c.title, 'John') > 0 OR FullTextScore(c.text, 'John') > 0 OR FullTextScore(c.text, 'United States') > 0)
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [-1, -1])",
                    new List<List<int>>{ new List<int>{ 57, 85, 22, 80, 76, 77, 24, 75, 54, 49, 51, 2, 61 } },
                    ValidationMode.TextOrTitleOrUnitedStates),
            };

            await this.RunTests(testCases, enableFullTextPreviewFeatures: true);
        }

        [TestMethod]
        public async Task WeightedRankFusionTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, 1])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                    }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [10, 10])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [0.1, 0.1])",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [-1, -1])",
                    new List<List<int>>{ new List<int>{ 57, 85, 22, 80, 76, 77, 24, 75, 54, 49, 51, 2, 61 } }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        [Ignore("Requires a COMBINEDSCORE-capable ServiceInterop dependency and emulator. Direct 3.44.1 cannot plan this function.")]
        public async Task CombinedScoreTests()
        {
            CosmosArray documents = await LoadDocuments();
            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documents.Select(document => document.ToString()),
                query: async (container, _) =>
                {
                    AccountProperties account = await this.Client.ReadAccountAsync();
                    QueryPartitionProvider provider = await this.Client.DocumentClient.QueryPartitionProvider;
                    provider.Update(new Dictionary<string, object>(account.QueryEngineConfiguration)
                    {
                        ["queryEnableFullTextPreviewFeatures"] = true,
                    });

                    const string title = "FullTextScore(c.title, 'John')";
                    const string text = "FullTextScore(c.text, 'United States')";
                    const string projection = "c._rid AS Rid, c.index AS Index, " +
                        "(FullTextScore(c.title, 'John') ?? -1) AS TitleScore, " +
                        "(FullTextScore(c.text, 'United States') ?? -1) AS TextScore";
                    const string filter = " FROM c WHERE (FullTextContains(c.title, 'John') OR " +
                        "FullTextContains(c.text, 'United States')) AND c.index >= 0 ORDER BY RANK ";
                    (string Suffix, double TitleWeight, double TextWeight)[] cases =
                    {
                        ("", 1, 1),
                        (", [2, 1]", 2, 1),
                        (", 3", 1, 1),
                        (", [2, -1], 3", 2, -1),
                        (", [0, 1]", 0, 1),
                        (", @weights, @multiplier", 2, -1),
                    };

                    foreach (FullTextScoreScope scope in new[] { FullTextScoreScope.Local, FullTextScoreScope.Global })
                    {
                        foreach (bool disableOptimization in new[] { false, true })
                        {
                            QueryRequestOptions options = new QueryRequestOptions
                            {
                                FullTextScoreScope = scope,
                                IsHybridSearchQueryPlanOptimizationDisabled = disableOptimization,
                                MaxItemCount = 2,
                            };
                            List<CosmosElement> oracle = await RunQueryCombinationsAsync(
                                container, "SELECT " + projection + filter + title,
                                options, QueryDrainingMode.HoldState);
                            Assert.IsTrue(oracle.Count > 2);

                            foreach ((string suffix, double titleWeight, double textWeight) in cases)
                            {
                                foreach (bool offsetLimit in new[] { false, true })
                                {
                                    QueryDefinition query = new QueryDefinition(
                                        (offsetLimit ? "SELECT " : "SELECT TOP 5 ") + projection + filter +
                                        $"COMBINEDSCORE({title}, {text}{suffix})" +
                                        (offsetLimit ? " OFFSET 1 LIMIT 5" : ""))
                                        .WithParameter("@weights", new[] { 2, -1 })
                                        .WithParameter("@multiplier", 3);
                                    List<CosmosElement> actual = new List<CosmosElement>();
                                    using FeedIterator<CosmosElement> iterator = container.GetItemQueryIterator<CosmosElement>(query, requestOptions: options);
                                    while (iterator.HasMoreResults)
                                    {
                                        actual.AddRange(await iterator.ReadNextAsync());
                                    }

                                    double Contribution(double weight, CosmosElement projectedScore)
                                    {
                                        double score = Number64.ToDouble(((CosmosNumber)projectedScore).Value);
                                        return weight == 0 ? 0 : weight * (weight < 0 && score == -1 ? 1.79769e308 : score);
                                    }

                                    string[] expected = oracle.Cast<CosmosObject>()
                                        .OrderByDescending(row => Contribution(titleWeight, row["TitleScore"]) + Contribution(textWeight, row["TextScore"]))
                                        .ThenBy(row => ((CosmosString)row["Rid"]).Value.ToString(), StringComparer.Ordinal)
                                        .Skip(offsetLimit ? 1 : 0).Take(5).Select(row => row.ToString()).ToArray();
                                    CollectionAssert.AreEqual(expected, actual.Select(row => row.ToString()).ToArray());
                                }
                            }
                        }
                    }
                },
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
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
                    if (enableFullTextPreviewFeatures)
                    {
                        AccountProperties account = await this.Client.ReadAccountAsync();
                        IDictionary<string, object> queryEngineConfiguration = new Dictionary<string, object>(account.QueryEngineConfiguration)
                        {
                            ["queryEnableFullTextPreviewFeatures"] = true,
                        };

                        QueryPartitionProvider provider = await this.Client.DocumentClient.QueryPartitionProvider;
                        provider.Update(queryEngineConfiguration);
                    }

                    await RunTests(container, testCases);
                },
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunTests(Container container, IEnumerable<SanityTestCase> testCases)
        {
            foreach (FullTextScoreScope fullTextScoreScope in new[]{ FullTextScoreScope.Local, FullTextScoreScope.Global })
            {
                foreach (SanityTestCase testCase in testCases)
                {
                    QueryRequestOptions testRequestOptions = new QueryRequestOptions
                    {
                        FullTextScoreScope = fullTextScoreScope,
                    };

                    if (testCase.PartitionKey.HasValue)
                    {
                        testRequestOptions.PartitionKey = testCase.PartitionKey;
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

                    bool match = false;
                    foreach (IReadOnlyList<int> expectedIndices in testCase.ExpectedIndices)
                    {
                        if (expectedIndices.SequenceEqual(actual))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (!match)
                    {
                        Trace.WriteLine($"Query: {testCase.Query}");
                        Trace.WriteLine($"Actual: {string.Join(", ", actual)}");

                        string errorMessage = @"The query results did not match any of the expected results." +
                            "Please set HybridSearchCrossPartitionQueryPipelineStage.HybridSearchDebugTraceHelpers.Enabled = true to debug." +
                            "Usually, the failure may be due to some swaps in the results that have equal scores. You can see this in the debug output." +
                            "The solution is to add another expected result that matches the actual results (provided the scores are in decresing order).";
                        Assert.Fail(errorMessage);
                    }
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

        private static SanityTestCase MakeSanityTest(
            string query,
            IReadOnlyList<IReadOnlyList<int>> expectedIndices,
            ValidationMode validationMode = ValidationMode.None,
            PartitionKey? partitionKey = null)
        {
            return new SanityTestCase
            {
                Query = query,
                ExpectedIndices = expectedIndices,
                ValidationMode = validationMode,
                PartitionKey = partitionKey,
            };
        }

        private sealed class SanityTestCase
        {
            public string Query { get; init; }

            public IReadOnlyList<IReadOnlyList<int>> ExpectedIndices { get; init; }

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
