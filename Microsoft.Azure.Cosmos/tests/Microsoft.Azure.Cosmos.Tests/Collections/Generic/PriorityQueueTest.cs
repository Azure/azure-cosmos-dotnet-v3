//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for "PriorityQueue" class.
    /// </summary>
    [TestClass]
    public class PriorityQueueTest
    {
        private static readonly int Size = 1000;

        /// <summary>
        /// Tests for "PriorityQueue" constructors.
        /// </summary>
        public void TestPriorityQueueConstuctors()
        {
            // Constructor Test 1
            Assert.AreEqual(0, new PriorityQueue<int>(Size).Count);

            // Constructor Test 2
            try
            {
                new PriorityQueue<int>(-1);
                Assert.Fail("ArgumentOutOfRangeException should have been thrown.");
            }
            catch (ArgumentOutOfRangeException)
            { }

            // Constructor Test 3
            try
            {
                new PriorityQueue<int>((IEnumerable<int>)null);
                Assert.Fail("ArgumentNullException should have been thrown.");
            }
            catch (ArgumentNullException)
            { }

            // Constructor Test 4
            try
            {
                new PriorityQueue<int>((IComparer<int>)null);
                Assert.Fail("ArgumentNullException should have been thrown.");
            }
            catch (ArgumentNullException)
            { }

            // Constructor Test 5
            {
                new PriorityQueue<string>(new string[Size]);
            }

            // Constructor Test 6
            {
                string[] array = new string[Size];
                for (int i = 0; i < array.Length - 1; ++i)
                {
                    array[i] = i.ToString(CultureInfo.InvariantCulture);
                }

                new PriorityQueue<string>(array);
            }

            // Constructor Test 7
            {
                int[] array = new int[Size];
                for (int i = 0; i < array.Length; ++i)
                {
                    array[i] = i;
                }

                PriorityQueue<int> queue = new PriorityQueue<int>(array);

                foreach (int item in array)
                {
                    Assert.AreEqual(item, queue.Dequeue());
                }
            }

            // Constructor Test 8
            {
                int[] array = new int[Size];
                for (int i = 0; i < array.Length; ++i)
                {
                    array[i] = i;
                }

                PriorityQueue<int> queue = new PriorityQueue<int>(array, Comparer<int>.Create((i1, i2) => i2.CompareTo(i1)));

                for (int i = array.Length - 1; i >= 0; --i)
                {
                    Assert.AreEqual(array[i], queue.Dequeue());
                }
            }
        }

        /// <summary>
        /// Tests for "PriorityQueue.Count".
        /// </summary>
        [TestMethod]
        public void TestCount()
        {
            this.TestCount(PopulatedPriorityQueue(Size));
        }

        private void TestCount(PriorityQueue<int> queue)
        {
            for (int i = 0; i < Size; ++i)
            {
                Assert.AreEqual(Size - i, queue.Count);
                queue.Dequeue();
            }

            for (int i = 0; i < Size; ++i)
            {
                Assert.AreEqual(i, queue.Count);
                queue.Enqueue(i);
            }
        }

        /// <summary>
        /// Tests for "PriorityQueue.Enqueue".
        /// </summary>
        [TestMethod]
        public void TestEnqueue()
        {
            this.TestEnqueue((int size) => new PriorityQueue<object>(size));
        }

        private void TestEnqueue(Func<int, PriorityQueue<object>> func)
        {
            {
                PriorityQueue<object> queue = func(1);
                queue.Enqueue(null);
                queue.Enqueue(null);
            }

            try
            {
                PriorityQueue<object> queue = func(1);
                queue.Enqueue(1);
                queue.Enqueue("1");
                Assert.Fail("ArgumentException should have been thrown.");
            }
            catch (ArgumentException)
            { }

            try
            {
                PriorityQueue<object> queue = func(1);
                queue.Enqueue(new object());
                queue.Enqueue(new object());
                Assert.Fail("ArgumentException should have been thrown.");
            }
            catch (ArgumentException)
            { }

            {
                PriorityQueue<object> queue = func(Size);
                for (int i = 0; i < Size; ++i)
                {
                    Assert.AreEqual(i, queue.Count);
                    queue.Enqueue(i);
                }
            }
        }

        /// <summary>
        /// Tests for "PriorityQueue.Dequeue".
        /// </summary>
        [TestMethod]
        public void TestDequeue()
        {
            this.TestDequeue(PopulatedPriorityQueue(Size));
        }

        private void TestDequeue(PriorityQueue<int> queue)
        {
            for (int i = 0; i < Size; ++i)
            {
                Assert.AreEqual(i, queue.Dequeue());
            }

            try
            {
                queue.Dequeue();
                Assert.Fail("InvalidOperationException should have been thrown.");
            }
            catch (InvalidOperationException)
            { }
        }


        /// <summary>
        /// Tests for "PriorityQueue.Peek".
        /// </summary>
        [TestMethod]
        public void TestPeek()
        {
            this.TestPeek(PopulatedPriorityQueue(Size));
        }

        private void TestPeek(PriorityQueue<int> queue)
        {
            for (int i = 0; i < Size; ++i)
            {
                Assert.AreEqual(i, queue.Peek());
                queue.Dequeue();
            }

            try
            {
                queue.Peek();
                Assert.Fail("InvalidOperationException should have been thrown.");
            }
            catch (InvalidOperationException)
            { }
        }

        /// <summary>
        /// Tests for "PriorityQueue.Contains".
        /// </summary>
        [TestMethod]
        public void TestContains()
        {
            this.TestContains(PopulatedPriorityQueue(Size));
        }

        private void TestContains(PriorityQueue<int> queue)
        {
            for (int i = 0; i < Size; ++i)
            {
                Assert.IsTrue(queue.Contains(i));
                queue.Dequeue();
                Assert.IsFalse(queue.Contains(i));
            }
        }

        /// <summary>
        /// Tests for "PriorityQueue.Clear".
        /// </summary>
        [TestMethod]
        public void TestClear()
        {
            this.TestClear(PopulatedPriorityQueue(Size));
        }

        private void TestClear(PriorityQueue<int> queue)
        {
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
            queue.Enqueue(0);
            Assert.IsTrue(queue.Count > 0);
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
        }

        /// <summary>
        /// Simple Test for "PriorityQueue".
        /// </summary>
        [TestMethod]
        public void SimpleTest()
        {
            this.SimpleTest(new PriorityQueue<int>(0));
        }

        private void SimpleTest(PriorityQueue<int> queue)
        {
            Assert.AreEqual(0, queue.Count);
            queue.Enqueue(8);
            queue.Enqueue(18);
            queue.Enqueue(8);
            queue.Enqueue(3);
            Assert.AreEqual(4, queue.Count);

            Assert.AreEqual(3, queue.Peek());

            Assert.AreEqual(string.Join(", ", new[] { 3, 8, 8, 18 }), string.Join(", ", queue));

            Assert.AreEqual(3, queue.Dequeue());
            Assert.AreEqual(3, queue.Count);

            Assert.AreEqual(string.Join(", ", new[] { 8, 18, 8 }), string.Join(", ", queue));

            Assert.AreEqual(8, queue.Peek());
            Assert.AreEqual(8, queue.Dequeue());
            Assert.AreEqual(2, queue.Count);

            queue.Enqueue(15);

            Assert.AreEqual(string.Join(", ", new[] { 8, 18, 15 }), string.Join(", ", queue));

            Assert.AreEqual(8, queue.Peek());
            Assert.AreEqual(8, queue.Dequeue());
            Assert.AreEqual(2, queue.Count);

            Assert.AreEqual(15, queue.Peek());
            Assert.AreEqual(15, queue.Dequeue());
            Assert.AreEqual(1, queue.Count);

            Assert.AreEqual(string.Join(", ", new[] { 18 }), string.Join(", ", queue));

            Assert.AreEqual(18, queue.Peek());
            Assert.AreEqual(18, queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }

        /// <summary>
        /// Random Test for "PriorityQueue".
        /// </summary>
        [TestMethod]
        public void RandomTest()
        {
            this.RandomTest(
                (int size, IComparer<int> comparer) => new PriorityQueue<int>(size, comparer),
                (int[] array, IComparer<int> comparer) => new PriorityQueue<int>(array, comparer));
        }

        private void RandomTest(
            Func<int, IComparer<int>, PriorityQueue<int>> func1,
            Func<int[], IComparer<int>, PriorityQueue<int>> func2)
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Random rand = new Random(seed);

            for (int trial = 0; trial < 20; ++trial)
            {
                foreach (IComparer<int> comparer in new[]
                {
                    Comparer<int>.Default,
                    Comparer<int>.Create((i1, i2) => i2.CompareTo(i1)),
                })
                {
                    foreach (bool enqueue in new[] { true, false })
                    {
                        {
                            int[] array = new int[(1 << trial) + rand.Next(2)];

                            for (int i = 0; i < array.Length; ++i)
                            {
                                array[i] = rand.Next();
                            }

                            PriorityQueue<int> queue;
                            if (enqueue)
                            {
                                queue = new PriorityQueue<int>(1, comparer);
                                foreach (int item in array)
                                {
                                    queue.Enqueue(item);
                                }
                            }
                            else
                            {
                                queue = new PriorityQueue<int>(array, comparer);
                            }

                            Array.Sort(array, comparer);

                            for (int i = 0; i < array.Length; ++i)
                            {
                                Assert.AreEqual(
                                    array.Length - i,
                                    queue.Count,
                                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
                                Assert.AreEqual(
                                    array[i],
                                    queue.Peek(),
                                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
                                Assert.AreEqual(
                                    array.Length - i,
                                    queue.Count,
                                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
                                Assert.AreEqual(
                                    array[i],
                                    queue.Dequeue(),
                                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
                                Assert.AreEqual(
                                    array.Length - i - 1,
                                    queue.Count,
                                    string.Format(CultureInfo.InvariantCulture, "seed: {0}", seed));
                            }
                        }
                    }
                }
            }
        }

        private static PriorityQueue<int> PopulatedPriorityQueue(int n)
        {
            return PopulatedPriorityQueue(new PriorityQueue<int>(n), n);
        }

        private static PriorityQueue<int> PopulatedPriorityQueue(PriorityQueue<int> queue, int n)
        {
            Assert.AreEqual(0, queue.Count);
            for (int i = n - 1; i >= 0; i -= 2)
            {
                queue.Enqueue(i);
            }
            for (int i = n & 1; i < n; i += 2)
            {
                queue.Enqueue(i);
            }

            Assert.AreEqual(n, queue.Count);
            return queue;
        }
    }
}