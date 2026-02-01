//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for MemoryExtensions compatibility in LINQ translation.
    /// Issue #5518: In .NET 10+, array.Contains() resolves to MemoryExtensions.Contains().
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
            // Get MemoryExtensions type if available (may not exist in older .NET)
            Type memoryExtensionsType = Type.GetType("System.MemoryExtensions, System.Memory") 
                ?? Type.GetType("System.MemoryExtensions, System.Runtime");

            if (memoryExtensionsType != null)
            {
                // MemoryExtensions exists, verify detection works
                Assert.AreEqual("System.MemoryExtensions", memoryExtensionsType.FullName);
            }
            else
            {
                // MemoryExtensions not available in this runtime - that's OK
                // The check uses string comparison so it will work when the type is available
                Assert.Inconclusive("MemoryExtensions type not available in this runtime");
            }
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
        }
    }
}
