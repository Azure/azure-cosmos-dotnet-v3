//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestClass]
    public class ConstantEvaluatorTests
    {
        [TestMethod]
        public void ClosuresAreEvaluated()
        {
            int a = 1;
            // In class member init
            Expression<Func<int, object>> closureInClassExpression = x => new TestClass { Property = x + a };
            Expression<Func<int, object>> closureInClassExpressionExpected = x => new TestClass { Property = x + 1 };
            Expression closureInClassExpressionActual = ConstantEvaluator.PartialEval(closureInClassExpression.Body);
            Assert.AreEqual(closureInClassExpressionExpected.Body.ToString(), closureInClassExpressionActual.ToString());

            // In anonymous object
            Expression<Func<int, object>> closureInAnonymousObjectExpression = x => new { Property = x + a };
            Expression<Func<int, object>> closureInAnonymousObjectExpressionExpected = x => new { Property = x + 1 };
            Expression closureInAnonymousObjectExpressionActual = ConstantEvaluator.PartialEval(closureInAnonymousObjectExpression.Body);
            Assert.AreEqual(closureInAnonymousObjectExpressionExpected.Body.ToString(), closureInAnonymousObjectExpressionActual.ToString());
        }

        private class TestClass
        {
            public int Property { get; set; }
        }
    }
}
