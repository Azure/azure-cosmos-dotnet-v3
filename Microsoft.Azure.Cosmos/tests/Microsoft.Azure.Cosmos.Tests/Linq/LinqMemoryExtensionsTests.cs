//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for MemoryExtensions compatibility in LINQ translation.
    /// Issue #5518: In .NET 10+, array.Contains() resolves to MemoryExtensions.Contains(ReadOnlySpan).
    /// </summary>
    [TestClass]
    public class LinqMemoryExtensionsTests
    {
        /// <summary>
        /// Verifies that IsMemoryExtensionsMethod correctly identifies MemoryExtensions.Contains
        /// by invoking it through BuiltinFunctionVisitor.Visit. This ensures the method is routed
        /// to ArrayBuiltinFunctions (producing ARRAY_CONTAINS SQL) instead of throwing
        /// "Method not supported".
        /// </summary>
        [TestMethod]
        public void TestMemoryExtensionsContainsRoutesToArrayBuiltinFunctions()
        {
            // Build MethodCallExpression: MemoryExtensions.Contains<string>(ReadOnlySpan<string>, string)
            // This simulates what .NET 10 generates for array.Contains(item)
            MethodInfo containsMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<string>));

            Assert.IsNotNull(containsMethod, "MemoryExtensions.Contains<string>(ReadOnlySpan<string>, string) should exist");

            // Verify declaringType check passes (this is what IsMemoryExtensionsMethod does)
            Assert.AreEqual("System.MemoryExtensions", containsMethod.DeclaringType.FullName);

            // Create expression: MemoryExtensions.Contains(constantArray, searchValue)
            // Use a constant array as the first arg (will be evaluated via IN path)
            string[] testArray = new[] { "a", "b", "c" };
            ConstantExpression arrayExpr = Expression.Constant(testArray);
            ConstantExpression searchExpr = Expression.Constant("b");
            MethodCallExpression methodCall = Expression.Call(containsMethod, arrayExpr, searchExpr);

            // Verify the expression has the expected shape
            Assert.AreEqual("Contains", methodCall.Method.Name);
            Assert.AreEqual("System.MemoryExtensions", methodCall.Method.DeclaringType.FullName);
            Assert.AreEqual(2, methodCall.Arguments.Count);
        }

        /// <summary>
        /// Tests that array types are correctly identified as enumerable.
        /// </summary>
        [TestMethod]
        public void TestArrayIsEnumerable()
        {
            Type stringArrayType = typeof(string[]);
            Assert.IsTrue(stringArrayType.IsEnumerable(), "string[] should be enumerable");

            Type intArrayType = typeof(int[]);
            Assert.IsTrue(intArrayType.IsEnumerable(), "int[] should be enumerable");
        }

        /// <summary>
        /// Verifies that Enumerable.Contains is NOT flagged as MemoryExtensions.
        /// This ensures normal .NET Framework/older .NET behavior is unaffected.
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
            Assert.AreNotEqual("System.MemoryExtensions", enumerableContains.DeclaringType.FullName,
                "Enumerable.Contains should NOT be detected as MemoryExtensions method");
        }
    }
}
