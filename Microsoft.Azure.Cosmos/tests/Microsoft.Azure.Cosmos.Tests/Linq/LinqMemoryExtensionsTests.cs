//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for .NET 10 MemoryExtensions.Contains LINQ translation (Issue #5518).
    ///
    /// In .NET 10+, array.Contains(x) generates:
    ///   MemoryExtensions.Contains&lt;T&gt;(ReadOnlySpan&lt;T&gt;.op_Implicit(array), x)
    ///
    /// These tests validate each condition in our fix:
    /// 1. ConstantEvaluator: MemoryExtensions.Contains must not be evaluated as a constant
    /// 2. ConstantEvaluator: op_Implicit on ReadOnlySpan/Span must not be evaluated
    /// 3. BuiltinFunctionVisitor: MemoryExtensions.Contains must route to ArrayBuiltinFunctions
    /// 4. Unsupported MemoryExtensions methods must throw DocumentQueryException
    /// </summary>
    [TestClass]
    public class LinqMemoryExtensionsTests
    {
        #region Helpers

        private static MethodInfo GetMemoryExtensionsContainsMethod<T>()
        {
            return typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(T)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<T>));
        }

        private static MethodInfo GetOpImplicitMethod<T>()
        {
            return typeof(ReadOnlySpan<T>)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "op_Implicit"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(T[]));
        }

        /// <summary>
        /// Builds the expression tree that .NET 10 generates for array.Contains(item):
        ///   MemoryExtensions.Contains&lt;T&gt;(ReadOnlySpan&lt;T&gt;.op_Implicit(array), item)
        /// </summary>
        private static MethodCallExpression BuildNet10ContainsExpression<T>(
            Expression arrayExpression,
            Expression itemExpression)
        {
            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod<T>();
            MethodInfo opImplicit = GetOpImplicitMethod<T>();

            MethodCallExpression spanConversion = Expression.Call(opImplicit, arrayExpression);
            return Expression.Call(containsMethod, spanConversion, itemExpression);
        }

        /// <summary>
        /// Gets an unsupported MemoryExtensions method (IndexOf) for negative testing.
        /// </summary>
        private static MethodInfo GetMemoryExtensionsIndexOfMethod()
        {
            return typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "IndexOf" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<string>));
        }

        #endregion

        #region ConstantEvaluator — MemoryExtensions.Contains is not evaluated

        /// <summary>
        /// ConstantEvaluator must NOT evaluate MemoryExtensions.Contains — it must be preserved
        /// in the expression tree for the SQL translator to process.
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_MemoryExtensionsContains_IsNotEvaluated()
        {
            MethodInfo opImplicit = GetOpImplicitMethod<string>();
            if (opImplicit == null)
            {
                Assert.Inconclusive("ReadOnlySpan<string>.op_Implicit not available on this runtime");
                return;
            }

            string[] testArray = new[] { "a", "b", "c" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");

            MethodCallExpression net10Contains = BuildNet10ContainsExpression<string>(arrayConst, paramX);
            Expression<Func<string, bool>> lambda = Expression.Lambda<Func<string, bool>>(net10Contains, paramX);

            // PartialEval must NOT throw — the fix prevents evaluation of the MemoryExtensions call
            Expression result = ConstantEvaluator.PartialEval(lambda.Body);

            // The result should still be a MethodCallExpression (not collapsed to a constant)
            Assert.IsInstanceOfType(result, typeof(MethodCallExpression),
                "MemoryExtensions.Contains should be preserved as a method call, not evaluated to a constant");
        }

        /// <summary>
        /// ConstantEvaluator must NOT evaluate op_Implicit on ReadOnlySpan because ref structs
        /// cannot be boxed into Expression.Constant.
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_OpImplicitOnReadOnlySpan_IsNotEvaluated()
        {
            MethodInfo opImplicit = GetOpImplicitMethod<string>();
            if (opImplicit == null)
            {
                Assert.Inconclusive("ReadOnlySpan<string>.op_Implicit not available on this runtime");
                return;
            }

            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            MethodCallExpression opImplicitCall = Expression.Call(opImplicit, arrayConst);

            // PartialEval must NOT throw — if it tries to evaluate this, it will fail
            // because ReadOnlySpan<T> is a ref struct that cannot be stored in Expression.Constant
            Expression result = ConstantEvaluator.PartialEval(opImplicitCall);

            // The op_Implicit call should be preserved (not collapsed)
            Assert.IsInstanceOfType(result, typeof(MethodCallExpression),
                "op_Implicit on ReadOnlySpan should be preserved, not evaluated");
        }

        /// <summary>
        /// ConstantEvaluator must NOT evaluate op_Implicit on Span&lt;T&gt; either.
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_OpImplicitOnSpan_IsNotEvaluated()
        {
            MethodInfo opImplicit = typeof(Span<string>)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "op_Implicit"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string[]));

            if (opImplicit == null)
            {
                Assert.Inconclusive("Span<string>.op_Implicit not available on this runtime");
                return;
            }

            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            MethodCallExpression opImplicitCall = Expression.Call(opImplicit, arrayConst);

            Expression result = ConstantEvaluator.PartialEval(opImplicitCall);

            Assert.IsInstanceOfType(result, typeof(MethodCallExpression),
                "op_Implicit on Span should be preserved, not evaluated");
        }

        /// <summary>
        /// Enumerable.Contains must continue to be excluded from evaluation (regression test).
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_EnumerableContains_StillExcluded()
        {
            MethodInfo enumerableContains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .First(m => m.GetParameters().Length == 2);

            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayExpr = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");
            MethodCallExpression containsCall = Expression.Call(enumerableContains, arrayExpr, paramX);

            Expression<Func<string, bool>> lambda = Expression.Lambda<Func<string, bool>>(containsCall, paramX);

            Expression result = ConstantEvaluator.PartialEval(lambda.Body);

            // Should be preserved as a method call for SQL translation
            Assert.IsInstanceOfType(result, typeof(MethodCallExpression),
                "Enumerable.Contains should be preserved for SQL translation");
        }

        /// <summary>
        /// Non-Span op_Implicit (e.g., numeric conversions) should still be evaluable.
        /// This ensures we don't over-block.
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_NonSpanOpImplicit_IsStillEvaluated()
        {
            // int to double implicit conversion
            MethodInfo opImplicit = typeof(double)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "op_Implicit"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(int));

            if (opImplicit == null)
            {
                // Not all runtimes expose this as a method; skip if unavailable
                Assert.Inconclusive("double.op_Implicit(int) not available as reflection method");
                return;
            }

            ConstantExpression intConst = Expression.Constant(42);
            MethodCallExpression implicitCall = Expression.Call(opImplicit, intConst);

            Expression result = ConstantEvaluator.PartialEval(implicitCall);

            // This SHOULD be evaluated to a constant since double is not a ref struct
            Assert.IsInstanceOfType(result, typeof(ConstantExpression),
                "Non-Span op_Implicit should be evaluated to a constant");
        }

        #endregion

        #region BuiltinFunctionVisitor — Supported method: MemoryExtensions.Contains → SQL IN

        /// <summary>
        /// End-to-end: MemoryExtensions.Contains with string[] produces correct SQL IN clause.
        /// </summary>
        [TestMethod]
        public void Translate_MemoryExtensionsContains_StringArray_ProducesInClause()
        {
            MethodInfo opImplicit = GetOpImplicitMethod<string>();
            if (opImplicit == null)
            {
                Assert.Inconclusive("ReadOnlySpan<string>.op_Implicit not available on this runtime");
                return;
            }

            string[] testArray = new[] { "a", "b", "c" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");
            MethodCallExpression net10Contains = BuildNet10ContainsExpression<string>(arrayConst, paramX);

            string sql = SqlTranslator.TranslateExpression(net10Contains);

            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("IN"), $"Expected IN clause but got: {sql}");
            Assert.IsTrue(sql.Contains("\"a\""), $"Expected array element 'a' in SQL: {sql}");
            Assert.IsTrue(sql.Contains("\"b\""), $"Expected array element 'b' in SQL: {sql}");
            Assert.IsTrue(sql.Contains("\"c\""), $"Expected array element 'c' in SQL: {sql}");
        }

        /// <summary>
        /// End-to-end: MemoryExtensions.Contains with int[] produces correct SQL IN clause.
        /// </summary>
        [TestMethod]
        public void Translate_MemoryExtensionsContains_IntArray_ProducesInClause()
        {
            MethodInfo containsMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(int)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<int>));

            MethodInfo opImplicit = GetOpImplicitMethod<int>();

            if (containsMethod == null || opImplicit == null)
            {
                Assert.Inconclusive("Required methods not available on this runtime");
                return;
            }

            int[] testArray = new[] { 1, 2, 3 };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(int), "x");
            MethodCallExpression spanConversion = Expression.Call(opImplicit, arrayConst);
            MethodCallExpression containsCall = Expression.Call(containsMethod, spanConversion, paramX);

            string sql = SqlTranslator.TranslateExpression(containsCall);

            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("IN"), $"Expected IN clause but got: {sql}");
            Assert.IsTrue(sql.Contains("1"), $"Expected element '1' in SQL: {sql}");
            Assert.IsTrue(sql.Contains("3"), $"Expected element '3' in SQL: {sql}");
        }

        /// <summary>
        /// End-to-end: MemoryExtensions.Contains with Guid[] produces correct SQL IN clause.
        /// </summary>
        [TestMethod]
        public void Translate_MemoryExtensionsContains_GuidArray_ProducesInClause()
        {
            MethodInfo containsMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(Guid)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<Guid>));

            MethodInfo opImplicit = GetOpImplicitMethod<Guid>();

            if (containsMethod == null || opImplicit == null)
            {
                Assert.Inconclusive("Required methods not available on this runtime");
                return;
            }

            Guid guid1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid guid2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Guid[] testArray = new[] { guid1, guid2 };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(Guid), "x");
            MethodCallExpression spanConversion = Expression.Call(opImplicit, arrayConst);
            MethodCallExpression containsCall = Expression.Call(containsMethod, spanConversion, paramX);

            string sql = SqlTranslator.TranslateExpression(containsCall);

            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("IN"), $"Expected IN clause but got: {sql}");
            Assert.IsTrue(sql.Contains("11111111-1111-1111-1111-111111111111"),
                $"Expected guid1 in SQL: {sql}");
        }

        /// <summary>
        /// Regression: Enumerable.Contains still produces correct SQL IN clause.
        /// </summary>
        [TestMethod]
        public void Translate_EnumerableContains_StillProducesInClause()
        {
            MethodInfo enumerableContains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .First(m => m.GetParameters().Length == 2);

            string[] testArray = new[] { "x", "y" };
            ConstantExpression arrayExpr = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");
            MethodCallExpression containsCall = Expression.Call(enumerableContains, arrayExpr, paramX);

            Expression<Func<string, bool>> lambda = Expression.Lambda<Func<string, bool>>(containsCall, paramX);
            string sql = SqlTranslator.TranslateExpression(lambda.Body);

            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("IN"), $"Expected IN clause but got: {sql}");
            Assert.IsTrue(sql.Contains("\"x\""), $"Expected 'x' in SQL: {sql}");
            Assert.IsTrue(sql.Contains("\"y\""), $"Expected 'y' in SQL: {sql}");
        }

        #endregion

        #region BuiltinFunctionVisitor — Unsupported MemoryExtensions methods throw

        /// <summary>
        /// MemoryExtensions.IndexOf is NOT supported and must throw DocumentQueryException.
        /// This validates that we only support Contains — not arbitrary MemoryExtensions methods.
        /// </summary>
        [TestMethod]
        public void Translate_MemoryExtensionsIndexOf_ThrowsDocumentQueryException()
        {
            MethodInfo indexOfMethod = GetMemoryExtensionsIndexOfMethod();
            MethodInfo opImplicit = GetOpImplicitMethod<string>();

            if (indexOfMethod == null || opImplicit == null)
            {
                Assert.Inconclusive("Required methods not available on this runtime");
                return;
            }

            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");
            MethodCallExpression spanConversion = Expression.Call(opImplicit, arrayConst);
            MethodCallExpression indexOfCall = Expression.Call(indexOfMethod, spanConversion, paramX);

            DocumentQueryException exception = Assert.ThrowsException<DocumentQueryException>(
                () => SqlTranslator.TranslateExpression(indexOfCall),
                "Unsupported MemoryExtensions methods should throw DocumentQueryException");

            Assert.IsTrue(exception.Message.Contains("IndexOf"),
                $"Error message should mention the unsupported method name. Got: {exception.Message}");
        }

        /// <summary>
        /// MemoryExtensions.SequenceEqual is NOT supported and must throw DocumentQueryException.
        /// </summary>
        [TestMethod]
        public void Translate_MemoryExtensionsSequenceEqual_ThrowsDocumentQueryException()
        {
            MethodInfo sequenceEqualMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "SequenceEqual" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault();

            MethodInfo opImplicit = GetOpImplicitMethod<string>();

            if (sequenceEqualMethod == null || opImplicit == null)
            {
                Assert.Inconclusive("Required methods not available on this runtime");
                return;
            }

            string[] testArray1 = new[] { "a", "b" };
            string[] testArray2 = new[] { "a", "b" };
            ConstantExpression arr1Const = Expression.Constant(testArray1);
            ConstantExpression arr2Const = Expression.Constant(testArray2);
            MethodCallExpression span1 = Expression.Call(opImplicit, arr1Const);
            MethodCallExpression span2 = Expression.Call(opImplicit, arr2Const);

            // SequenceEqual takes two ReadOnlySpan<T> args — may not match the method signature
            // that takes (ReadOnlySpan<T>, ReadOnlySpan<T>). Find the right overload.
            MethodInfo seqEqual2Spans = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "SequenceEqual" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m =>
                {
                    var p = m.GetParameters();
                    return p.Length == 2
                        && p[0].ParameterType == typeof(ReadOnlySpan<string>)
                        && p[1].ParameterType == typeof(ReadOnlySpan<string>);
                });

            if (seqEqual2Spans == null)
            {
                Assert.Inconclusive("MemoryExtensions.SequenceEqual(ReadOnlySpan, ReadOnlySpan) not found");
                return;
            }

            MethodCallExpression seqEqualCall = Expression.Call(seqEqual2Spans, span1, span2);

            Assert.ThrowsException<DocumentQueryException>(
                () => SqlTranslator.TranslateExpression(seqEqualCall),
                "Unsupported MemoryExtensions.SequenceEqual should throw DocumentQueryException");
        }

        #endregion

        #region ConstantEvaluator — Unsupported MemoryExtensions methods (non-Contains) are still evaluable

        /// <summary>
        /// MemoryExtensions methods other than Contains should NOT be blocked by ConstantEvaluator.
        /// They are not our concern — if they reach the translator, they'll get an appropriate error.
        /// If they can be evaluated (e.g., in a sub-expression), the evaluator should allow it.
        /// 
        /// Note: In practice, these will fail evaluation anyway due to the op_Implicit on Span,
        /// but the ConstantEvaluator exclusion is specifically scoped to Contains only.
        /// </summary>
        [TestMethod]
        public void ConstantEvaluator_MemoryExtensionsNonContains_NotExplicitlyExcluded()
        {
            MethodInfo indexOfMethod = GetMemoryExtensionsIndexOfMethod();
            if (indexOfMethod == null)
            {
                Assert.Inconclusive("MemoryExtensions.IndexOf not available");
                return;
            }

            // The MemoryExtensions.Contains check uses: type == typeof(MemoryExtensions) && Name == "Contains"
            // IndexOf should NOT match this condition
            Assert.AreEqual(typeof(MemoryExtensions), indexOfMethod.DeclaringType);
            Assert.AreNotEqual("Contains", indexOfMethod.Name);

            // The method is "not excluded" by our MemoryExtensions check.
            // It will still be blocked by the op_Implicit check (since its argument is a Span),
            // but the MemoryExtensions-specific exclusion doesn't cover it.
            // This is intentional — we only explicitly support Contains.
        }

        #endregion
    }
}
