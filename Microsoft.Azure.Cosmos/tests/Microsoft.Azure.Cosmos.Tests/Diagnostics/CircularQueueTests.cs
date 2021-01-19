//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CircularQueueTests
    {
        [TestMethod]
        public void BasicTests()
        {
            Assert.IsTrue(CircularQueue<int>.TryCreate(4, out CircularQueue<int> queue));

            Assert.IsTrue(queue.Empty);
            Assert.IsFalse(queue.Full);

            queue.Add(1);
            Assert.IsFalse(queue.Empty);
            Assert.IsFalse(queue.Full);

            queue.Add(2);
            queue.Add(3);

            int expected = 0;
            foreach (int actual in queue)
            {
                Assert.AreEqual(++expected, actual);
            }

            queue.Add(4);
            Assert.IsTrue(queue.Full);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, queue.ToArray());

            queue.Add(5);
            queue.Add(6);
            Assert.IsTrue(queue.Full);
            Assert.IsFalse(queue.Empty);
            CollectionAssert.AreEquivalent(new[] { 3, 4, 5, 6 }, queue.ToList());
        }
    }
}