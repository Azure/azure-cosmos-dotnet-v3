//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosLinqTypeIsTests
    {
        [TestMethod]
        public void TypeIsStatementWithTypeNameHandlingIsTranslated()
        {
            // Should work for document properties with TypeNameHandling configured
            Expression<Func<TestDocument, bool>> expr = a => a.Child is TestDocumentChild;
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"Child\"][\"$type\"] = \"Microsoft.Azure.Cosmos.Linq.CosmosLinqTypeIsTests+TestDocumentChild, Microsoft.Azure.Cosmos.Tests\")", sql);
        }

        [TestMethod]
        public void TypeIsStatementOnNestedPropertiesWithTypeNameHandlingIsTranslated()
        {
            // Should work for nested document properties with TypeNameHandling configured
            Expression<Func<TestDocument, bool>> expr = a => a.Child.InnerChild is TestDocumentChild;
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"Child\"][\"InnerChild\"][\"$type\"] = \"Microsoft.Azure.Cosmos.Linq.CosmosLinqTypeIsTests+TestDocumentChild, Microsoft.Azure.Cosmos.Tests\")", sql);
        }

        [TestMethod]
        public void TypeIsStatementWithoutTypeNameHandlingThrowsException()
        {
            // Should throw for document properties with no TypeNameHandling configured
            Expression<Func<TestDocument, bool>> expr = a => a.ChildWithoutTypeNameHandling is TestDocumentChild;
            Assert.ThrowsException<DocumentQueryException>(() => SqlTranslator.TranslateExpression(expr.Body));
        }

        class TestDocument
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.Objects)]
            public ITestDocumentChild Child { get; set; }

            [JsonProperty(TypeNameHandling = TypeNameHandling.None)]
            public ITestDocumentChild ChildWithoutTypeNameHandling { get; set; }
        }

        interface ITestDocumentChild
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.Objects)]
            ITestDocumentChild InnerChild { get; }
        }

        class TestDocumentChild : ITestDocumentChild
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.Objects)]
            public ITestDocumentChild InnerChild { get; set; }
        }
    }
}
