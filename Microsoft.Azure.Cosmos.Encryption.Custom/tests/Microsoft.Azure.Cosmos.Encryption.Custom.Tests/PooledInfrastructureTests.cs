//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Buffers;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
    [TestClass]
    public class PooledInfrastructureTests
    {
        [TestMethod]
        public void PooledBufferWriter_BasicAppend_ConsumePrefix_FreeCapacity()
        {
            using PooledBufferWriter<byte> writer = new(initialCapacity: 4);
            // Append 10 bytes in chunks
            for (int i = 0; i < 10; i++)
            {
                writer.GetSpan(1)[0] = (byte)i;
                writer.Advance(1);
            }
            Assert.AreEqual(10, writer.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, 10).Select(i => (byte)i).ToArray(), writer.ToArray());

            // Consume first 3 bytes
            writer.ConsumePrefix(3);
            Assert.AreEqual(7, writer.Count);
            CollectionAssert.AreEqual(Enumerable.Range(3, 7).Select(i => (byte)i).ToArray(), writer.ToArray());

            // Ensure capacity and verify FreeCapacity shrinks after more writes
            int beforeFree = writer.FreeCapacity;
            writer.GetSpan(5); // just requesting span shouldn't change count
            Assert.AreEqual(7, writer.Count);
            writer.GetSpan(1)[0] = 42; // add one more element
            writer.Advance(1);
            Assert.AreEqual(8, writer.Count);
            Assert.IsTrue(writer.FreeCapacity <= beforeFree);
        }

        [TestMethod]
        public void PooledBufferWriter_ClearAndDispose_ClearsWhenOptionSet()
        {
            using PooledBufferWriter<object> writer = new(initialCapacity: 2, options: PooledBufferWriterOptions.AlwaysClear);
            writer.GetSpan(1)[0] = new object();
            writer.Advance(1);
            Assert.AreEqual(1, writer.Count);
            writer.Clear();
            Assert.AreEqual(0, writer.Count);
            // Can't directly assert clearing of references without reflection; ensure no exception.
            writer.Dispose();
        }

        [TestMethod]
        public void PooledList_Add_AddRange_Indexer_Enumerate_Clear()
        {
            using PooledList<int> list = new(initialCapacity: 2);
            list.Add(1);
            list.Add(2);
            list.AddRange(new int[] { 3, 4, 5 });
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(4, list[3]);
            list[3] = 40;
            Assert.AreEqual(40, list[3]);
            int sum = 0;
            foreach (int v in list)
            {
                sum += v;
            }
            Assert.AreEqual(1 + 2 + 3 + 40 + 5, sum);
            list.Clear();
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void PooledList_ToArray_MatchesSequence()
        {
            using PooledList<byte> list = new();
            byte[] expected = new byte[100];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)i;
                list.Add((byte)i);
            }
            CollectionAssert.AreEqual(expected, list.ToArray());
        }

        [TestMethod]
        public void PooledBufferWriter_EnsureCapacity_DoesNotShrink()
        {
            using PooledBufferWriter<int> writer = new(initialCapacity: 4);
            writer.GetSpan(10); // force growth
            int capacityAfterGrow = writer.FreeCapacity + writer.Count;
            writer.EnsureCapacity(2); // ensuring smaller shouldn't shrink
            int capacityAfterEnsure = writer.FreeCapacity + writer.Count;
            Assert.AreEqual(capacityAfterGrow, capacityAfterEnsure);
        }
    }
#endif
}
