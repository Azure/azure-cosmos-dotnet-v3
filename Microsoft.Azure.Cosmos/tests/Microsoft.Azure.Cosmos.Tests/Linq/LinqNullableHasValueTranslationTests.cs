//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LinqNullableHasValueTranslationTests
    {
        private const string HasValue = "(IS_DEFINED(doc[\"Score\"]) AND (NOT IS_NULL(doc[\"Score\"])))";

        [TestMethod]
        public void HasValue_ExcludesMissingAndNull()
        {
            Assert.AreEqual(HasValue, Translate(doc => doc.Score.HasValue));
        }

        [TestMethod]
        public void NotHasValue_IncludesMissingAndNull()
        {
            Assert.AreEqual($"(NOT {HasValue})", Translate(doc => !doc.Score.HasValue));
        }

        private static string Translate<TResult>(Expression<Func<Document, TResult>> selector)
        {
            return SqlTranslator.TranslateExpression(selector.Body);
        }

        private sealed class Document
        {
            public int? Score { get; set; }
        }
    }
}
