//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for GitHub Issue #5547: 
    /// LINQ queries using .Any() on Dictionary&lt;string, object&gt; properties should generate SQL using OBJECTTOARRAY.
    /// </summary>
    [TestClass]
    public class LinqDictionaryQueryTests
    {
        /// <summary>
        /// Test that IsDictionary correctly identifies Dictionary types.
        /// </summary>
        [TestMethod]
        public void IsDictionary_ShouldReturnTrueForDictionaryTypes()
        {
            // Test Dictionary<,>
            Assert.IsTrue(typeof(Dictionary<string, object>).IsDictionary());
            Assert.IsTrue(typeof(Dictionary<int, string>).IsDictionary());
            
            // Test IDictionary<,>
            Assert.IsTrue(typeof(IDictionary<string, object>).IsDictionary());
            Assert.IsTrue(typeof(IDictionary<int, string>).IsDictionary());
            
            // Test types that implement IDictionary<,>
            Assert.IsTrue(typeof(SortedDictionary<string, object>).IsDictionary());
        }

        /// <summary>
        /// Test that IsDictionary correctly rejects non-Dictionary types.
        /// </summary>
        [TestMethod]
        public void IsDictionary_ShouldReturnFalseForNonDictionaryTypes()
        {
            // Test arrays
            Assert.IsFalse(typeof(string[]).IsDictionary());
            Assert.IsFalse(typeof(int[]).IsDictionary());
            
            // Test lists
            Assert.IsFalse(typeof(List<string>).IsDictionary());
            Assert.IsFalse(typeof(List<object>).IsDictionary());
            
            // Test IEnumerable
            Assert.IsFalse(typeof(IEnumerable<string>).IsDictionary());
            
            // Test primitives
            Assert.IsFalse(typeof(string).IsDictionary());
            Assert.IsFalse(typeof(int).IsDictionary());
            
            // Test other collections
            Assert.IsFalse(typeof(HashSet<string>).IsDictionary());
        }
    }
}
