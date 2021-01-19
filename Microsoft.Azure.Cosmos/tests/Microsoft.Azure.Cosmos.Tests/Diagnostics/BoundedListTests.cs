//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BoundedListTests
    {
        [TestMethod]
        public void CapacityValidationTests()
        {
            foreach (int x in new[] {-512, -256, -1, 0 })
            {
                Assert.IsFalse(BoundedList<int>.TryCreate(x, out _));
            }

            foreach (int x in new[] { 1, 2, 8, 256 })
            {
                Assert.IsTrue(BoundedList<int>.TryCreate(x, out _));
            }
        }

        [TestMethod]
        [DataRow(   3,   21, DisplayName = "Extra small")]
        [DataRow(   5,   25, DisplayName = "Small")]
        [DataRow(  64,  256, DisplayName = "Medium")]
        [DataRow( 256, 1024, DisplayName = "Large")]
        [DataRow(2048, 4096, DisplayName = "Extra large")]
        public void BasicTests(int capacity, int numElements)
        {
            Assert.IsTrue(BoundedList<int>.TryCreate(capacity, out BoundedList<int> boundedList));

            for (int i = 0; i < numElements; ++i)
            {
                boundedList.Add(i);

                int expected = (i >= capacity) ? (i - capacity + 1) : 0; 
                foreach(int actual in boundedList)
                {
                    Assert.AreEqual(expected, actual);
                    ++expected;
                }
            }
        }
    }
}