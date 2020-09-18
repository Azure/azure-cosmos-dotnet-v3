//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for "PriorityQueue" class.
    /// </summary>
    [TestClass]
    public class AsyncCollectionTest
    {
        private const int DelayInMilliSeconds = 100;
        /// <summary>
        /// Test for "AddAsync".
        /// </summary>
        [TestMethod]
        public async Task TestAddAsync()
        {
            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
            queue.Enqueue(0);
            AsyncCollection<int> asyncCollection = new AsyncCollection<int>(queue, 1);
            int item = 1;
            Task task = asyncCollection.AddAsync(item);
            await Task.Delay(DelayInMilliSeconds);
            Assert.AreEqual(false, task.IsCompleted);
            Assert.AreEqual(0, await asyncCollection.TakeAsync());
            await task;
            Assert.AreEqual(item, await asyncCollection.TakeAsync());
        }

        /// <summary>
        /// Test for "AddRangeAsync".
        /// </summary>
        [TestMethod]
        public async Task TestAddRangeAsync()
        {
            AsyncCollection<int> asyncCollection = new AsyncCollection<int>(2);
            Task task = asyncCollection.AddRangeAsync(new[] { 0, 1, 2 });
            await Task.Delay(DelayInMilliSeconds);
            Assert.AreEqual(false, task.IsCompleted);
            Assert.AreEqual(0, await asyncCollection.TakeAsync());
            await task;
            Assert.AreEqual(1, await asyncCollection.TakeAsync());
        }

        /// <summary>
        /// Test for "TakeAsync".
        /// </summary>
        [TestMethod]
        public async Task TestTakeAsync()
        {
            AsyncCollection<int> asyncCollection = new AsyncCollection<int>();
            Task<int> task = asyncCollection.TakeAsync();
            await Task.Delay(DelayInMilliSeconds);
            Assert.AreEqual(false, task.IsCompleted);
            int item = 1;
            await asyncCollection.AddAsync(item);
            Assert.AreEqual(item, await task);
        }

        /// <summary>
        /// Test for "PeekAsync".
        /// </summary>
        [TestMethod]
        public async Task TestPeekAsync()
        {
            AsyncCollection<int> asyncCollection = new AsyncCollection<int>();
            Task<int> task = asyncCollection.PeekAsync();
            await Task.Delay(DelayInMilliSeconds);
            Assert.AreEqual(false, task.IsCompleted);
            int item = 1;
            await asyncCollection.AddAsync(item);
            Assert.AreEqual(item, await task);
        }

        /// <summary>
        /// Simple Test for "AsyncCollection".
        /// </summary>
        [TestMethod]
        public async Task SimpleTest()
        {
            int size = 100;
            AsyncCollection<int> asyncCollection = new AsyncCollection<int>();
            Assert.AreEqual(0, asyncCollection.Count);
            List<int> list = new List<int>();
            for (int i = 0; i < size; ++i)
            {
                list.Add(i);
            }

            foreach (bool addOneByOne in new[] { true, false })
            {
                foreach (bool takeOneByOne in new[] { true, false })
                {
                    if (addOneByOne)
                    {
                        foreach (int i in list)
                        {
                            await asyncCollection.AddAsync(i);
                            Assert.AreEqual(i + 1, asyncCollection.Count);
                        }
                    }
                    else
                    {
                        await asyncCollection.AddRangeAsync(list);
                        Assert.AreEqual(size, asyncCollection.Count);
                    }

                    if (takeOneByOne)
                    {
                        foreach (int i in list)
                        {
                            Assert.AreEqual(i, await asyncCollection.PeekAsync());
                            Assert.AreEqual(size - i, asyncCollection.Count);
                            Assert.AreEqual(i, await asyncCollection.TakeAsync());
                            Assert.AreEqual(size - i - 1, asyncCollection.Count);
                        }
                    }
                    else
                    {
                        Assert.AreEqual(
                            string.Join(",", list),
                            string.Join(",", await asyncCollection.DrainAsync()));
                    }
                }
            }
        }

        /// <summary>
        /// Test for AsyncCollection.PeekAsync NotImplementedException
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public async Task TestNotImplmenetedPeekAsync()
        {
            await new AsyncCollection<int>(new TestProducerConsumerCollection<int>()).PeekAsync();
        }

        private class TestProducerConsumerCollection<T> : IProducerConsumerCollection<T>
        {
            public void CopyTo(T[] array, int index)
            {
                throw new NotImplementedException();
            }

            public T[] ToArray()
            {
                throw new NotImplementedException();
            }

            public bool TryAdd(T item)
            {
                throw new NotImplementedException();
            }

            public bool TryTake(out T item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public int Count => throw new NotImplementedException();

            public bool IsSynchronized => throw new NotImplementedException();

            public object SyncRoot => throw new NotImplementedException();
        }
    }
}
