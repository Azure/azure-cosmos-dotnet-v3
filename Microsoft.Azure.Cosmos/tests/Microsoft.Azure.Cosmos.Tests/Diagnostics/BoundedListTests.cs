//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BoundedListTests
    {
        [TestMethod]
        public void CapacityValidationTests()
        {
            foreach (int x in new[] { -512, -256, -1, 0 })
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoundedList<int>(x));
            }

            foreach (int x in new[] { 1, 2, 8, 256 })
            {
                Assert.IsNotNull(new BoundedList<int>(x));
            }
        }

        [TestMethod]
        [DataRow(3, 21, DisplayName = "Extra small")]
        [DataRow(5, 25, DisplayName = "Small")]
        [DataRow(256, 1024, DisplayName = "Medium")]
        [DataRow(5120, 10240, DisplayName = "Large")]
        [DataRow(10240, 20480, DisplayName = "Large")]
        public void BasicTests(int capacity, int numElements)
        {
            BoundedList<int> boundedList = new BoundedList<int>(capacity);

            for (int i = 0; i < numElements; ++i)
            {
                boundedList.Add(i);

                int expected = (i >= capacity) ? (i - capacity + 1) : 0;
                foreach (int actual in boundedList)
                {
                    Assert.AreEqual(expected, actual);
                    ++expected;
                }
            }
        }
    }
}