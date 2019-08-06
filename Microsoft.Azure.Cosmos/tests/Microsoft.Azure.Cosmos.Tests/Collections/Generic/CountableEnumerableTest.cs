//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for "CountableEnumerable" class.
    /// </summary>
    [TestClass]
    public class CountableEnumerableTest
    {
        /// <summary>
        /// Simple test for "CountableEnumerable".
        /// </summary>
        [TestMethod]
        public void TestConstructors()
        {
            // Constructor Test 1
            Assert.AreEqual(0, new CountableEnumerable<int>(new[] { 1 }, 0).Count);

            // Constructor Test 2
            Assert.AreEqual(1, new CountableEnumerable<int>(new[] { 1, 2 }, 1).Count);

            // Constructor Test 3
            try
            {
                new CountableEnumerable<int>(null, 1);
                Assert.Fail("ArgumentNullException should have been thrown.");
            }
            catch (ArgumentNullException)
            { }

            // Constructor Test 4
            try
            {
                new CountableEnumerable<int>(new[] { 1 }, -1);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }
        }

        /// <summary>
        /// Simple test for "CountableEnumerable".
        /// </summary>
        [TestMethod]
        public void SimpleTest()
        {
            CountableEnumerable<int> enumerable = new CountableEnumerable<int>(new[] { 1, 2, 3, 4 }, 2);
            Assert.AreEqual(2, enumerable.Count);
            Assert.AreEqual(string.Join(", ", 1, 2), string.Join(", ", enumerable));

            enumerable = new CountableEnumerable<int>(new[] { 1 }, 2);
            Assert.AreEqual(2, enumerable.Count);
            Assert.AreEqual(string.Join(", ", 1), string.Join(", ", enumerable));

            enumerable = new CountableEnumerable<int>(new[] { 1, 2 }, 2);
            Assert.AreEqual(2, enumerable.Count);
            Assert.AreEqual(string.Join(", ", 1, 2), string.Join(", ", enumerable));

            enumerable = new CountableEnumerable<int>(new[] { 1, 2 }, 0);
            Assert.AreEqual(0, enumerable.Count);
            Assert.AreEqual(string.Empty, string.Join(", ", enumerable));
        }
    }
}
