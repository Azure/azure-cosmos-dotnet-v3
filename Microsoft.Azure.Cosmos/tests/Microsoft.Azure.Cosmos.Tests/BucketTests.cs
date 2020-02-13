//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BucketTests
    {
        private static IReadOnlyList<int> oddList = Enumerable.Range(0, 5).ToList();
        private static IReadOnlyList<int> evenList = Enumerable.Range(0, 6).ToList();

        [TestMethod]
        public void Bucket_WrongSize()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => oddList.Bucket(0).ToList());
        }

        [TestMethod]
        public void Bucket_With1()
        {
            Assert.AreEqual(oddList.Count, oddList.Bucket(1).Count());
            Assert.AreEqual(evenList.Count, evenList.Bucket(1).Count());
        }

        [TestMethod]
        public void Bucket_WithListSize()
        {
            Assert.AreEqual(1, oddList.Bucket(oddList.Count).Count());
            Assert.AreEqual(1, evenList.Bucket(evenList.Count).Count());
        }

        [TestMethod]
        public void Bucket_WithLargerSize()
        {
            Assert.AreEqual(1, oddList.Bucket(oddList.Count + 1).Count());
            Assert.AreEqual(1, evenList.Bucket(evenList.Count + 1).Count());
        }

        [TestMethod]
        public void Bucket_WithRandomSize()
        {
            this.ValidateRandomSize(oddList);
            this.ValidateRandomSize(evenList);
        }

        private void ValidateRandomSize(IReadOnlyList<int> list)
        {
            int count = list.Count;
            while (count > 0)
            {
                int bucketSize = count--;
                List<IReadOnlyList<int>> newList = list.Bucket(bucketSize).ToList();
                Assert.AreEqual(list.Count, newList.SelectMany(b => b).Count());
                foreach (IReadOnlyList<int> bucket in newList)
                {
                    foreach (int i in bucket)
                    {
                        Assert.IsTrue(list.Contains(i));
                    }
                }

                foreach (int i in list)
                {
                    Assert.IsNotNull(newList.Any(b => b.Contains(i)));
                }
            }
        }
    }
}
