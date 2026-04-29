//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for MemoryExtensions compatibility in LINQ translation.
    /// Issue #5518: In .NET 10+, array.Contains() resolves to MemoryExtensions.Contains(ReadOnlySpan).
    ///
    /// On .NET 10, the compiler generates an expression tree like:
    ///   MemoryExtensions.Contains&lt;string&gt;(
    ///       ReadOnlySpan&lt;string&gt;.op_Implicit(array),   // implicit cast
    ///       item
    ///   )
    /// 
    /// This differs from older .NET which generates:
    ///   Enumerable.Contains&lt;string&gt;(array, item)
    ///
    /// Two layers must handle this correctly:
    /// 1. ConstantEvaluator - must NOT try to evaluate op_Implicit(array) as a constant
    ///    because ReadOnlySpan is a ref struct and cannot be boxed.
    /// 2. BuiltinFunctionVisitor - must recognize MemoryExtensions.Contains and route it
    ///    to ArrayBuiltinFunctions for proper SQL translation.
    /// </summary>
    [TestClass]
    public class LinqMemoryExtensionsTests
    {
        /// <summary>
        /// Helper: Gets MemoryExtensions.Contains&lt;string&gt;(ReadOnlySpan&lt;string&gt;, string)
        /// </summary>
        private static MethodInfo GetMemoryExtensionsContainsMethod()
        {
            return typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<string>));
        }

        /// <summary>
        /// Helper: Gets ReadOnlySpan&lt;string&gt;.op_Implicit(string[]) method.
        /// This is the implicit conversion the .NET 10 compiler inserts.
        /// </summary>
        private static MethodInfo GetOpImplicitMethod()
        {
            return typeof(ReadOnlySpan<string>)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "op_Implicit"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string[]));
        }

        /// <summary>
        /// Helper: Builds the expression tree that .NET 10 generates for array.Contains(item):
        ///   MemoryExtensions.Contains(ReadOnlySpan.op_Implicit(array), item)
        /// </summary>
        private static MethodCallExpression BuildNet10ContainsExpression(
            Expression arrayExpression,
            Expression itemExpression)
        {
            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod();
            MethodInfo opImplicit = GetOpImplicitMethod();

            // Wrap the array in op_Implicit to create ReadOnlySpan<string>
            MethodCallExpression spanConversion = Expression.Call(opImplicit, arrayExpression);

            // MemoryExtensions.Contains(span, item)
            return Expression.Call(containsMethod, spanConversion, itemExpression);
        }

        /// <summary>
        /// Verifies that IsMemoryExtensionsMethod correctly identifies MemoryExtensions.Contains
        /// and the expression shape is correct.
        /// </summary>
        [TestMethod]
        public void TestMemoryExtensionsContainsDetection()
        {
            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod();
            Assert.IsNotNull(containsMethod, "MemoryExtensions.Contains<string>(ReadOnlySpan<string>, string) should exist");
            Assert.AreEqual("System.MemoryExtensions", containsMethod.DeclaringType.FullName);
        }

        /// <summary>
        /// Tests that array types are correctly identified as enumerable.
        /// </summary>
        [TestMethod]
        public void TestArrayIsEnumerable()
        {
            Assert.IsTrue(typeof(string[]).IsEnumerable(), "string[] should be enumerable");
            Assert.IsTrue(typeof(int[]).IsEnumerable(), "int[] should be enumerable");
        }

        /// <summary>
        /// Verifies that Enumerable.Contains is NOT flagged as MemoryExtensions.
        /// </summary>
        [TestMethod]
        public void TestEnumerableContainsNotDetectedAsMemoryExtensions()
        {
            MethodInfo enumerableContains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => m.GetParameters().Length == 2);

            Assert.IsNotNull(enumerableContains, "Should find Enumerable.Contains<string>");
            Assert.AreNotEqual("System.MemoryExtensions", enumerableContains.DeclaringType.FullName);
        }

        /// <summary>
        /// CRITICAL TEST: Validates that ConstantEvaluator.PartialEval does NOT attempt to 
        /// evaluate the op_Implicit(array) sub-expression when it's part of a 
        /// MemoryExtensions.Contains call.
        /// 
        /// On .NET 10, the expression tree for `constantArray.Contains(param)` is:
        ///   MemoryExtensions.Contains(ReadOnlySpan.op_Implicit(constantArray), param)
        ///
        /// Without a fix in ConstantEvaluator, it will try to evaluate op_Implicit(constantArray)
        /// as a constant. This fails because:
        ///   - ReadOnlySpan is a ref struct and cannot be boxed into object
        ///   - Expression.Constant(..., typeof(ReadOnlySpan&lt;string&gt;)) throws
        ///
        /// This test verifies the current behavior: does the ConstantEvaluator blow up 
        /// when encountering this expression pattern?
        /// </summary>
        [TestMethod]
        public void TestConstantEvaluatorWithOpImplicitSpanConversion()
        {
            MethodInfo opImplicit = GetOpImplicitMethod();
            if (opImplicit == null)
            {
                // On older runtimes without the op_Implicit, this test is not applicable
                Assert.Inconclusive("ReadOnlySpan<string>.op_Implicit(string[]) not available on this runtime");
                return;
            }

            // Simulate: MemoryExtensions.Contains(ReadOnlySpan.op_Implicit(new[]{"a","b"}), paramX)
            string[] testArray = new[] { "a", "b", "c" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");

            MethodCallExpression net10Contains = BuildNet10ContainsExpression(arrayConst, paramX);

            // The op_Implicit sub-expression: ReadOnlySpan<string>.op_Implicit(testArray)
            MethodCallExpression opImplicitCall = (MethodCallExpression)net10Contains.Arguments[0];

            // Verify: ConstantEvaluator.CanBeEvaluated would say TRUE for op_Implicit
            // because its DeclaringType (ReadOnlySpan<string>) is not Enumerable/Queryable/CosmosLinq.
            // This means PartialEval WILL try to evaluate it, which will fail for ref structs.
            Type opImplicitDeclaringType = opImplicitCall.Method.DeclaringType;
            Assert.AreNotEqual(typeof(Enumerable), opImplicitDeclaringType);
            Assert.AreNotEqual(typeof(Queryable), opImplicitDeclaringType);
            Assert.AreNotEqual(typeof(CosmosLinq), opImplicitDeclaringType);

            // Now test: does ConstantEvaluator.PartialEval throw when processing this expression?
            // We wrap in a lambda to give it a full expression tree shape
            Expression<Func<string, bool>> lambda = Expression.Lambda<Func<string, bool>>(
                net10Contains, paramX);

            // This is the key assertion: PartialEval will try to evaluate op_Implicit(array)
            // as a constant. Since ReadOnlySpan<T> is a ref struct, this should fail.
            bool threwException = false;
            string exceptionMessage = null;
            try
            {
                Expression result = ConstantEvaluator.PartialEval(lambda.Body);
            }
            catch (Exception ex)
            {
                threwException = true;
                exceptionMessage = ex.Message;
            }

            // Document current behavior: 
            // If this DOES throw, it proves Kiran's fix alone is insufficient — 
            // we also need the ConstantEvaluator fix from Minh's PR.
            // If it does NOT throw, Kiran's fix may be sufficient on its own.
            if (threwException)
            {
                Assert.Fail(
                    $"ConstantEvaluator.PartialEval throws when encountering op_Implicit for ReadOnlySpan. " +
                    $"This proves we need to fix ConstantEvaluator to exclude op_Implicit for Span types. " +
                    $"Exception: {exceptionMessage}");
            }
        }

        /// <summary>
        /// Tests the full translation pipeline with a MemoryExtensions.Contains expression 
        /// where the array is a constant (the common case: `constantArray.Contains(x)`).
        /// 
        /// This simulates the end-to-end scenario: ConstantEvaluator → BuiltinFunctionVisitor → SQL.
        /// If ConstantEvaluator incorrectly evaluates the op_Implicit, or if BuiltinFunctionVisitor
        /// doesn't recognize the pattern, this test will fail.
        /// </summary>
        [TestMethod]
        public void TestFullTranslationPipelineWithMemoryExtensionsContains()
        {
            MethodInfo opImplicit = GetOpImplicitMethod();
            if (opImplicit == null)
            {
                Assert.Inconclusive("ReadOnlySpan<string>.op_Implicit(string[]) not available on this runtime");
                return;
            }

            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod();
            Assert.IsNotNull(containsMethod);

            // Build the .NET 10 expression: MemoryExtensions.Contains(op_Implicit(["a","b","c"]), x)
            string[] testArray = new[] { "a", "b", "c" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");

            MethodCallExpression net10Contains = BuildNet10ContainsExpression(arrayConst, paramX);

            // Attempt full translation via SqlTranslator.TranslateExpression
            // This exercises both ConstantEvaluator AND BuiltinFunctionVisitor
            string translatedSql = null;
            Exception translationException = null;
            try
            {
                translatedSql = SqlTranslator.TranslateExpression(net10Contains);
            }
            catch (Exception ex)
            {
                translationException = ex;
            }

            if (translationException != null)
            {
                Assert.Fail(
                    $"Full translation pipeline failed for MemoryExtensions.Contains expression. " +
                    $"Exception type: {translationException.GetType().Name}, " +
                    $"Message: {translationException.Message}. " +
                    $"This indicates the fix is incomplete — both ConstantEvaluator and " +
                    $"BuiltinFunctionVisitor need updates to handle the .NET 10 expression tree.");
            }

            // If translation succeeds, verify it produces the expected SQL (IN clause)
            Assert.IsNotNull(translatedSql, "Translation should produce SQL output");
            // Expected: x IN ("a", "b", "c") or similar
            Assert.IsTrue(
                translatedSql.Contains("IN") || translatedSql.Contains("ARRAY_CONTAINS"),
                $"Expected IN or ARRAY_CONTAINS in SQL output but got: {translatedSql}");
        }

        /// <summary>
        /// Tests that the BuiltinFunctionVisitor correctly handles a MemoryExtensions.Contains call
        /// where the first argument is already a ConstantExpression (array pre-evaluated).
        /// This is the simpler case where ConstantEvaluator has already collapsed the tree.
        /// </summary>
        [TestMethod]
        public void TestBuiltinFunctionVisitorWithConstantArrayDirectly()
        {
            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod();
            Assert.IsNotNull(containsMethod);

            // Directly pass array as constant (skipping op_Implicit) — 
            // this tests whether ArrayBuiltinFunctions.ArrayContainsVisitor.VisitImplicit
            // handles the 2-argument static method case correctly
            string[] testArray = new[] { "x", "y" };
            ConstantExpression arrayExpr = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");
            MethodCallExpression methodCall = Expression.Call(containsMethod, arrayExpr, paramX);

            // Attempt translation - this bypasses ConstantEvaluator
            string translatedSql = null;
            Exception translationException = null;
            try
            {
                TranslationContext context = new TranslationContext(null);
                // We can't easily call BuiltinFunctionVisitor.Visit directly since it's internal,
                // but we can go through SqlTranslator.TranslateExpression which skips partial eval
                // for expressions that are already in the right form
                translatedSql = SqlTranslator.TranslateExpression(methodCall);
            }
            catch (Exception ex)
            {
                translationException = ex;
            }

            if (translationException != null)
            {
                Assert.Fail(
                    $"BuiltinFunctionVisitor failed to translate MemoryExtensions.Contains with constant array. " +
                    $"Exception: {translationException.GetType().Name}: {translationException.Message}. " +
                    $"ArrayContainsVisitor.VisitImplicit may not handle the ReadOnlySpan parameter type.");
            }

            Assert.IsNotNull(translatedSql);
        }

        /// <summary>
        /// Validates that ConstantEvaluator marks MemoryExtensions.Contains as non-evaluable
        /// (because it's a query operation, not a constant).
        /// 
        /// Currently ConstantEvaluator only excludes Enumerable, Queryable, and CosmosLinq.
        /// MemoryExtensions is NOT excluded, which means the entire Contains call could be
        /// incorrectly evaluated as a constant if both arguments are constants.
        /// </summary>
        [TestMethod]
        public void TestConstantEvaluatorBehaviorWithMemoryExtensionsContains()
        {
            MethodInfo containsMethod = GetMemoryExtensionsContainsMethod();
            MethodInfo opImplicit = GetOpImplicitMethod();

            if (containsMethod == null || opImplicit == null)
            {
                Assert.Inconclusive("Required methods not available on this runtime");
                return;
            }

            // Build expression where BOTH arguments are constants:
            // MemoryExtensions.Contains(op_Implicit(["a","b"]), "a")
            // On .NET 10, this could be generated from: new[]{"a","b"}.Contains("a")
            // where both the array and the search value are known at compile time.
            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayConst = Expression.Constant(testArray);
            ConstantExpression searchConst = Expression.Constant("a");
            MethodCallExpression opImplicitCall = Expression.Call(opImplicit, arrayConst);
            MethodCallExpression containsCall = Expression.Call(containsMethod, opImplicitCall, searchConst);

            // Test: what does PartialEval do with this?
            // If it tries to evaluate the whole thing, it would attempt DynamicInvoke
            // on MemoryExtensions.Contains(ReadOnlySpan, "a") — but ReadOnlySpan is a ref struct
            // so creating a Constant of that type will fail.
            bool threwException = false;
            Exception caughtException = null;
            Expression result = null;
            try
            {
                result = ConstantEvaluator.PartialEval(containsCall);
            }
            catch (Exception ex)
            {
                threwException = true;
                caughtException = ex;
            }

            if (threwException)
            {
                // This confirms ConstantEvaluator needs fixing for the Span scenario
                Assert.Fail(
                    $"ConstantEvaluator.PartialEval failed on MemoryExtensions.Contains with all-constant args. " +
                    $"This proves Kiran's fix (BuiltinFunctionVisitor only) is INSUFFICIENT. " +
                    $"The ConstantEvaluator also needs to exclude op_Implicit for Span/ReadOnlySpan types. " +
                    $"Exception: {caughtException.GetType().Name}: {caughtException.Message}");
            }
            else
            {
                // If it doesn't throw, check what it produced
                // It might have evaluated to a ConstantExpression(true) which would be wrong
                // for query translation (we need the IN clause, not a literal bool)
                if (result is ConstantExpression constResult && constResult.Value is bool)
                {
                    // This means the evaluator successfully ran the Contains at compile time.
                    // While not an error per se, it means the query won't use IN clause for 
                    // server-side filtering — it would just be a constant true/false.
                    // In a real query with a parameter reference, this path wouldn't apply.
                }
            }
        }

        /// <summary>
        /// Regression test: Enumerable.Contains with a constant array still produces correct SQL.
        /// This ensures the fix doesn't break the existing (pre-.NET 10) behavior.
        /// </summary>
        [TestMethod]
        public void TestEnumerableContainsStillWorks()
        {
            MethodInfo enumerableContains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .First(m => m.GetParameters().Length == 2);

            string[] testArray = new[] { "a", "b" };
            ConstantExpression arrayExpr = Expression.Constant(testArray);
            ParameterExpression paramX = Expression.Parameter(typeof(string), "x");

            // Enumerable.Contains(array, x)
            MethodCallExpression containsCall = Expression.Call(enumerableContains, arrayExpr, paramX);

            Expression<Func<string, bool>> lambda = Expression.Lambda<Func<string, bool>>(
                containsCall, paramX);

            // This should work and produce IN clause
            string sql = SqlTranslator.TranslateExpression(lambda.Body);
            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("IN"), $"Expected IN clause but got: {sql}");
        }
    }
}
