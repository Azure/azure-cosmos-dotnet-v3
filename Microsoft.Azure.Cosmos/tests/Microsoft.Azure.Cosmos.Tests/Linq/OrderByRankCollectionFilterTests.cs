//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Linq.CosmosLinqExtensions;

    /// <summary>
    /// Tests for the post-translation rewrite of <c>Any()</c> collection filters that are
    /// incompatible with <c>ORDER BY RANK</c> (Issue #5509).
    ///
    /// The service rejects queries that combine a JOINed <c>SELECT VALUE EXISTS(...)</c>
    /// subquery with <c>ORDER BY RANK</c>. The SDK now rewrites such queries into the
    /// equivalent <c>WHERE EXISTS(...)</c> form, which the service accepts.
    ///
    /// The rewrite is rank-scoped: queries without <c>ORDER BY RANK</c>, and queries where the
    /// join variable is used outside the WHERE clause (e.g. in a Select projection), keep the
    /// existing JOIN form.
    /// </summary>
    [TestClass]
    public class OrderByRankCollectionFilterTests
    {
        private readonly CosmosClient client;

        public OrderByRankCollectionFilterTests()
        {
            this.client = new CosmosClient(
                "https://fake-account.documents.azure.com:443/",
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQc4caD5dK27iYHAP+A==");
        }

        [TestMethod]
        public void AnyFilterWithOrderByRank_RewritesJoinToExists()
        {
            float[] vector = new float[] { 1.0f, 2.0f, 3.0f };

            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics"))
                .OrderByRank(p => p.Embedding.VectorDistance(vector, false, null))
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(queryText.Contains("WHERE EXISTS("), $"Expected EXISTS in query: {queryText}");
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
            Assert.IsTrue(queryText.Contains("ORDER BY RANK"), $"Expected ORDER BY RANK: {queryText}");
        }

        [TestMethod]
        public void AnyFilterAndPredicateWithOrderByRank_RewritesJoinToExists()
        {
            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics") && p.Name == "phone")
                .OrderByRank(p => p.Name.FullTextScore("test"))
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(queryText.Contains("WHERE (EXISTS("), $"Expected EXISTS in query: {queryText}");
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
            Assert.IsTrue(queryText.Contains("ORDER BY RANK"), $"Expected ORDER BY RANK: {queryText}");
        }

        [TestMethod]
        public void TwoAnyFiltersWithOrderByRank_RewriteBothJoinsToExists()
        {
            float[] vector = new float[] { 1.0f, 2.0f, 3.0f };

            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics") && p.Tags.Any(t => t.Name == "Books"))
                .OrderByRank(p => p.Embedding.VectorDistance(vector, false, null))
                .ToQueryDefinition()
                .QueryText;

            Assert.AreEqual(2, Regex.Matches(queryText, "EXISTS\\(").Count, queryText);
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
        }

        [TestMethod]
        public void AnyFilterWithOrderByRankAndTake_EmitsTopWithExists()
        {
            float[] vector = new float[] { 1.0f, 2.0f, 3.0f };

            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics"))
                .OrderByRank(p => p.Embedding.VectorDistance(vector, false, null))
                .Take(10)
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(queryText.Contains("SELECT TOP 10"), $"Expected TOP 10 in query: {queryText}");
            Assert.IsTrue(queryText.Contains("WHERE EXISTS("), $"Expected EXISTS in query: {queryText}");
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
        }

        [TestMethod]
        public void AnyFilterWithoutRank_PreservesJoin()
        {
            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics"))
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(
                queryText.Contains("JOIN (SELECT VALUE EXISTS"),
                $"Expected JOINed subquery to be preserved: {queryText}");
        }

        [TestMethod]
        public void AnyFilterWithNonRankOrderBy_PreservesJoin()
        {
            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Tags.Any(t => t.Name == "Electronics"))
                .OrderBy(p => p.Name)
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(
                queryText.Contains("JOIN (SELECT VALUE EXISTS"),
                $"Expected JOINed subquery to be preserved: {queryText}");
        }

        [TestMethod]
        public void NotAnyAndAnyFilterWithOrderByRank_RewritesJoinToExists()
        {
            // A negated Any() is translated to NOT EXISTS directly, so the filter already
            // contains an EXISTS subquery when the rewrite substitutes the other join.
            float[] vector = new float[] { 1.0f, 2.0f, 3.0f };

            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => !p.Tags.Any(t => t.Name == "Books") && p.Tags.Any(t => t.Name == "Electronics"))
                .OrderByRank(p => p.Embedding.VectorDistance(vector, false, null))
                .ToQueryDefinition()
                .QueryText;

            Assert.AreEqual(2, Regex.Matches(queryText, "EXISTS\\(").Count, queryText);
            Assert.IsTrue(queryText.Contains("NOT EXISTS("), queryText);
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
        }

        [TestMethod]
        public void ContainsAndAnyFilterWithOrderByRank_RewritesJoinToExists()
        {
            // The filter contains a CONTAINS function call alongside the join reference.
            float[] vector = new float[] { 1.0f, 2.0f, 3.0f };

            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Where(p => p.Name.Contains("phone") && p.Tags.Any(t => t.Name == "Electronics"))
                .OrderByRank(p => p.Embedding.VectorDistance(vector, false, null))
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(queryText.Contains("CONTAINS(root[\"Name\"], \"phone\")"), queryText);
            Assert.IsTrue(queryText.Contains("WHERE (CONTAINS(root[\"Name\"], \"phone\") AND EXISTS("), queryText);
            Assert.IsFalse(queryText.Contains("JOIN (SELECT VALUE EXISTS"), $"Expected no JOINed subquery: {queryText}");
        }

        [TestMethod]
        public void AnyProjectionWithOrderByRank_PreservesJoin()
        {
            // The Any() join variable lives on the projection node while the rank order-by is on
            // the outer node, so the rewrite (which is scoped to a single rank-carrying node)
            // must not apply and the JOIN form must be preserved.
            string queryText = this.client
                .GetContainer("db", "container")
                .GetItemLinqQueryable<Product>()
                .Select(p => new { HasTag = p.Tags.Any(t => t.Name == "Electronics") })
                .OrderByRank(p => p.HasTag ? 1.0 : 0.0)
                .ToQueryDefinition()
                .QueryText;

            Assert.IsTrue(
                queryText.Contains("JOIN (SELECT VALUE EXISTS"),
                $"Expected JOINed subquery to be preserved: {queryText}");
        }

        private class Product
        {
            public string id { get; set; }

            public List<Tag> Tags { get; set; }

            public float[] Embedding { get; set; }

            public string Name { get; set; }
        }

        private class Tag
        {
            public string Name { get; set; }
        }
    }
}
