//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;

    /// <summary>
    /// Tests for "PartialReadOnlyList" class.
    /// </summary>
    [TestClass]
    public class PartialReadOnlyListTest
    {
        /// <summary>
        /// Tests for "PartialReadOnlyList" constructors.
        /// </summary>
        [TestMethod]
        public void TestConstructors()
        {
            // Constructor Test 1
            Assert.AreEqual(1, new PartialReadOnlyList<int>(new[] { 1 }.ToList(), 1).Count);

            // Constructor Test 2
            Assert.AreEqual(1, new PartialReadOnlyList<int>(new[] { 1, 2 }.ToList(), 0, 1).Count);

            // Constructor Test 3
            try
            {
                new PartialReadOnlyList<int>(null, 1);
                Assert.Fail("ArgumentNullException should have been thrown.");
            }
            catch (ArgumentNullException)
            { }

            // Constructor Test 4
            try
            {
                new PartialReadOnlyList<int>(new[] { 1 }.ToList(), 2);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }

            // Constructor Test 5
            try
            {
                new PartialReadOnlyList<int>(new[] { 1 }.ToList(), -1, 1);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }

            // Constructor Test 6
            try
            {
                new PartialReadOnlyList<int>(new[] { 1 }.ToList(), -1, 1);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }

            // Constructor Test 7
            try
            {
                new PartialReadOnlyList<int>(new[] { 1 }.ToList(), 1, 1);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }
        }


        /// <summary>
        /// Simple test for "PartialReadOnlyList".
        /// </summary>
        [TestMethod]
        public void SimpleTest()
        {
            PartialReadOnlyList<int> list = new PartialReadOnlyList<int>(new[] { 1, 2, 3, 4, 5 }.ToList(), 3, 2);
            Assert.AreEqual(4, list[0]);
            Assert.AreEqual(5, list[1]);
            Assert.AreEqual(string.Join(", ", 4, 5), string.Join(", ", list));

            try
            {
                int i = list[-1];
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }

            try
            {
                int i = list[2];
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }
        }
    }
}