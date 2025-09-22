// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PooledListTests
    {
        [TestMethod]
        public void AddAndIndexingWorks()
        {
            using PooledList<int> list = new(initialCapacity: 2);
            list.Add(10);
            list.Add(20);
            list.Add(30); // triggers growth
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(10, list[0]);
            Assert.AreEqual(20, list[1]);
            Assert.AreEqual(30, list[2]);

            list[1] = 25;
            Assert.AreEqual(25, list[1]);
        }

        [TestMethod]
        public void AddRangeAppends()
        {
            using PooledList<byte> list = new(initialCapacity: 1);
            list.AddRange(stackalloc byte[] { 1, 2, 3, 4 });
            list.Add(5);
            Assert.AreEqual(5, list.Count);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, list.ToArray());
        }

        [TestMethod]
        public void EnumeratorIterates()
        {
            using PooledList<string> list = new();
            list.Add("a");
            list.Add("b");
            list.Add("c");
            string concat = string.Empty;
            foreach (string s in list)
            {
                concat += s;
            }
            Assert.AreEqual("abc", concat);
        }

        [TestMethod]
        public void ClearResetsCount()
        {
            using PooledList<int> list = new();
            list.AddRange(stackalloc int[] { 1, 2, 3 });
            Assert.AreEqual(3, list.Count);
            list.Clear();
            Assert.AreEqual(0, list.Count);
            list.Add(42);
            Assert.AreEqual(42, list[0]);
        }

        [TestMethod]
        public void DisposePreventsUse()
        {
            PooledList<int> list = new();
            list.Add(1);
            list.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => list.Add(2));
        }
    }
}
#endif
