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
            // Should work documents with property attributes
            Expression<Func<TestDocumentWithPropertyTypeNameHandling, bool>> expr = a => a.Child is TestDocumentChild;
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"Child\"].$type = \"Microsoft.Azure.Cosmos.Linq.CosmosLinqTypeIsTests+TestDocumentChild, Microsoft.Azure.Cosmos.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=69c3241e6f0468ca\")", sql);
        }

        [TestMethod]
        public void TypeIsStatementWithoutTypeNameHandlingThrowsException()
        {
            // Should throw for documents with no TypeNameHandling configured
            Expression<Func<TestDocumentWithoutPropertyTypeNameHandling, bool>> expr = a => a.Child is TestDocumentChild;
            Assert.ThrowsException<DocumentQueryException>(() => SqlTranslator.TranslateExpression(expr.Body));
        }

        class TestDocumentWithPropertyTypeNameHandling
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.Objects)]
            public ITestDocumentChild Child { get; set; }
        }

        class TestDocumentWithoutPropertyTypeNameHandling
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.None)]
            public ITestDocumentChild Child { get; set; }
        }

        interface ITestDocumentChild {}

        class TestDocumentChild : ITestDocumentChild {}
    }
}
