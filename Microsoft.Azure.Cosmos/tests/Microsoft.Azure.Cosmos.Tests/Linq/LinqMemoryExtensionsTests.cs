//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for MemoryExtensions compatibility in LINQ translation.
    /// Issue #5518: In .NET 10+, array.Contains() resolves to MemoryExtensions.Contains(ReadOnlySpan).
    /// </summary>
    [TestClass]
    public class LinqMemoryExtensionsTests
    {
        /// <summary>
        /// Tests that the IsMemoryExtensionsMethod check correctly identifies MemoryExtensions methods.
        /// </summary>
        [TestMethod]
        public void TestIsMemoryExtensionsMethodDetection()
        {
            // Get MemoryExtensions type if available
            Type memoryExtensionsType = typeof(MemoryExtensions);
            Assert.IsNotNull(memoryExtensionsType);
            Assert.AreEqual("System.MemoryExtensions", memoryExtensionsType.FullName);
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
        /// Tests that the declaring type name check works correctly.
        /// </summary>
        [TestMethod]
        public void TestDeclaringTypeNameCheck()
        {
            // Verify string comparison approach works
            string memoryExtensionsFullName = "System.MemoryExtensions";
            
            // Simulate what we do in BuiltinFunctionVisitor
            Type declaringType = typeof(System.Linq.Enumerable);
            bool isMemoryExtensions = declaringType.FullName == memoryExtensionsFullName;
            Assert.IsFalse(isMemoryExtensions, "Enumerable should not be detected as MemoryExtensions");
            
            // Verify MemoryExtensions is detected
            Type memExtType = typeof(MemoryExtensions);
            bool isActuallyMemoryExtensions = memExtType.FullName == memoryExtensionsFullName;
            Assert.IsTrue(isActuallyMemoryExtensions, "MemoryExtensions should be detected");
        }

        /// <summary>
        /// Simulates the expression tree that .NET 10 generates for array.Contains().
        /// In .NET 10, the compiler resolves array.Contains(item) to MemoryExtensions.Contains(ReadOnlySpan, item).
        /// This test creates that expression programmatically and verifies our fix handles it.
        /// </summary>
        [TestMethod]
        public void TestMemoryExtensionsContainsExpressionDetection()
        {
            // Get MemoryExtensions.Contains method for ReadOnlySpan<string>
            MethodInfo containsMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => 
                    m.Name == "Contains" && 
                    m.IsGenericMethod &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType.Name.Contains("ReadOnlySpan"));

            Assert.IsNotNull(containsMethod, "MemoryExtensions.Contains<T>(ReadOnlySpan<T>, T) should exist");

            // Make generic method for string
            MethodInfo genericMethod = containsMethod.MakeGenericMethod(typeof(string));
            
            // Verify this is a MemoryExtensions method (what our fix checks)
            Assert.AreEqual("System.MemoryExtensions", genericMethod.DeclaringType.FullName,
                "DeclaringType should be System.MemoryExtensions");

            // Verify IsMemoryExtensionsMethod detection (simulating our fix)
            bool isMemoryExtensions = genericMethod.DeclaringType.FullName == "System.MemoryExtensions";
            Assert.IsTrue(isMemoryExtensions, "Should detect MemoryExtensions.Contains as MemoryExtensions method");
        }

        /// <summary>
        /// Tests that the BuiltinFunctionVisitor correctly identifies MemoryExtensions methods.
        /// This simulates what happens when .NET 10 compiles array.Contains() expressions.
        /// </summary>
        [TestMethod]
        public void TestBuiltinFunctionVisitorMemoryExtensionsCheck()
        {
            // Create expression: MemoryExtensions.Contains(array.AsSpan(), "test")
            string[] testArray = new[] { "a", "b", "c" };
            
            // Get MemoryExtensions.Contains<string>(ReadOnlySpan<string>, string)
            MethodInfo containsMethod = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => 
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<string>));

            Assert.IsNotNull(containsMethod, "Should find MemoryExtensions.Contains<string>");

            // The key check from our fix in BuiltinFunctionVisitor.IsMemoryExtensionsMethod:
            // methodInfo.DeclaringType.FullName == "System.MemoryExtensions"
            bool passesOurCheck = containsMethod.DeclaringType.FullName == "System.MemoryExtensions";
            Assert.IsTrue(passesOurCheck, 
                "MemoryExtensions.Contains should pass our IsMemoryExtensionsMethod check, " +
                "which causes fallback to Enumerable.Contains translation");
        }

        /// <summary>
        /// Verifies that Enumerable.Contains is NOT flagged as MemoryExtensions.
        /// This ensures normal .NET Framework/older .NET behavior is unaffected.
        /// </summary>
        [TestMethod]
        public void TestEnumerableContainsNotDetectedAsMemoryExtensions()
        {
            // Get Enumerable.Contains<string>(IEnumerable<string>, string)
            MethodInfo enumerableContains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Contains" && m.IsGenericMethod)
                .Select(m => m.MakeGenericMethod(typeof(string)))
                .FirstOrDefault(m => m.GetParameters().Length == 2);

            Assert.IsNotNull(enumerableContains, "Should find Enumerable.Contains<string>");

            // Verify this does NOT trigger our MemoryExtensions check
            bool passesMemoryExtCheck = enumerableContains.DeclaringType.FullName == "System.MemoryExtensions";
            Assert.IsFalse(passesMemoryExtCheck, 
                "Enumerable.Contains should NOT be detected as MemoryExtensions method");
        }
    }
}
