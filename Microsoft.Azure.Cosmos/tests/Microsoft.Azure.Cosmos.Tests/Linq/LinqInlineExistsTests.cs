//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for inline EXISTS optimization in WHERE predicates.
    /// This optimization enables .Any() filters to work with ORDER BY RANK.
    /// Related to Issue #5509.
    /// </summary>
    [TestClass]
    public class LinqInlineExistsTests
    {
        private Container container;

        [TestInitialize]
        public void TestInitialize()
        {
            this.container = MockCosmosUtil.CreateMockCosmosClient().GetContainer("test", "test");
        }

        /// <summary>
        /// Verifies that .Any() in WHERE generates inline EXISTS instead of JOIN binding.
        /// The EXISTS subquery will contain JOINs for iterating nested arrays, which is expected.
        /// What we're avoiding is the outer JOIN (SELECT VALUE EXISTS(...)) AS v pattern.
        /// </summary>
        [TestMethod]
        [TestCategory("LINQ")]
        public void AnyInWhereGeneratesInlineExists()
        {
            // Arrange
            IOrderedQueryable<ProductDocument> query = this.container.GetItemLinqQueryable<ProductDocument>();

            // Act - Simple Any in WHERE
            IQueryable<ProductDocument> linqQuery = query
                .Where(p => p.Tags.Any(t => t.Name == "Electronics"));

            string sql = linqQuery.ToQueryDefinition().QueryText;

            // Assert - Should contain inline EXISTS in WHERE clause
            Assert.IsTrue(sql.Contains("EXISTS"), $"Query should contain EXISTS. Actual: {sql}");
            Assert.IsTrue(sql.Contains("WHERE EXISTS"), $"EXISTS should be directly in WHERE clause. Actual: {sql}");
            // The problematic pattern is: JOIN (SELECT VALUE EXISTS(...)) AS vN WHERE vN
            // We should NOT have a JOIN that wraps the EXISTS result
            Assert.IsFalse(sql.Contains("JOIN (SELECT VALUE EXISTS"), 
                $"Query should NOT have JOIN wrapping EXISTS. Actual: {sql}");
        }

        /// <summary>
        /// Verifies that multiple .Any() calls in WHERE generate multiple inline EXISTS.
        /// </summary>
        [TestMethod]
        [TestCategory("LINQ")]
        public void MultipleAnyInWhereGeneratesMultipleInlineExists()
        {
            // Arrange
            IOrderedQueryable<ProductDocument> query = this.container.GetItemLinqQueryable<ProductDocument>();

            // Act - Multiple Any in WHERE with AND
            IQueryable<ProductDocument> linqQuery = query
                .Where(p => p.Tags.Any(t => t.Name == "Electronics") && p.Categories.Any(c => c == "Tech"));

            string sql = linqQuery.ToQueryDefinition().QueryText;

            // Assert - Should contain two inline EXISTS
            int existsCount = sql.Split(new[] { "EXISTS" }, StringSplitOptions.None).Length - 1;
            Assert.AreEqual(2, existsCount, $"Query should contain 2 EXISTS clauses. Actual: {sql}");
            // Should not have the binding pattern
            Assert.IsFalse(sql.Contains("JOIN (SELECT VALUE EXISTS"), 
                $"Query should NOT have JOIN wrapping EXISTS. Actual: {sql}");
        }

        /// <summary>
        /// Verifies that .Any() in SELECT still uses JOIN binding (not inline).
        /// This is necessary because the result needs to be projected.
        /// </summary>
        [TestMethod]
        [TestCategory("LINQ")]
        public void AnyInSelectStillUsesJoinBinding()
        {
            // Arrange
            IOrderedQueryable<ProductDocument> query = this.container.GetItemLinqQueryable<ProductDocument>();

            // Act - Any in SELECT (projection)
            var linqQuery = query
                .Select(p => new { HasTags = p.Tags.Any() });

            string sql = linqQuery.ToQueryDefinition().QueryText;

            // Assert - Should use JOIN for projection (this is expected and necessary)
            Assert.IsTrue(sql.Contains("JOIN"), $"Query with Any in SELECT should use JOIN. Actual: {sql}");
        }

        /// <summary>
        /// Verifies that simple .Any() without predicate in WHERE works.
        /// </summary>
        [TestMethod]
        [TestCategory("LINQ")]
        public void SimpleAnyWithoutPredicateInWhere()
        {
            // Arrange
            IOrderedQueryable<ProductDocument> query = this.container.GetItemLinqQueryable<ProductDocument>();

            // Act - Simple Any without predicate
            IQueryable<ProductDocument> linqQuery = query
                .Where(p => p.Tags.Any());

            string sql = linqQuery.ToQueryDefinition().QueryText;

            // Assert
            Assert.IsTrue(sql.Contains("EXISTS"), $"Query should contain EXISTS. Actual: {sql}");
            // Should not have the binding pattern
            Assert.IsFalse(sql.Contains("JOIN (SELECT VALUE EXISTS"), 
                $"Query should NOT have JOIN wrapping EXISTS. Actual: {sql}");
        }

        /// <summary>
        /// Test document class with nested collections.
        /// </summary>
        private class ProductDocument
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<Tag> Tags { get; set; }
            public List<string> Categories { get; set; }
            public float[] Embedding { get; set; }
        }

        private class Tag
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}
