// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Buffers;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PooledBufferWriterTests
    {
        [TestMethod]
        public void BasicWriteAndAdvance()
        {
            using PooledBufferWriter<byte> writer = new(initialCapacity: 8);
            Span<byte> span = writer.GetSpan(4);
            new byte[] { 1, 2, 3, 4 }.CopyTo(span);
            writer.Advance(4);
            Assert.AreEqual(4, writer.Count);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, writer.WrittenSpan.ToArray());
        }

        [TestMethod]
        public void GrowthWorks()
        {
            using PooledBufferWriter<int> writer = new(initialCapacity: 2);
            for (int i = 0; i < 50; i++)
            {
                Span<int> span = writer.GetSpan();
                span[0] = i;
                writer.Advance(1);
            }
            Assert.AreEqual(50, writer.Count);
            int[] arr = writer.ToArray();
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(i, arr[i]);
            }
        }

        [TestMethod]
        public void ClearResetsCount()
        {
            using PooledBufferWriter<byte> writer = new(initialCapacity: 4, options: PooledBufferWriterOptions.ClearOnReset | PooledBufferWriterOptions.ClearOnDispose);
            writer.GetSpan(3).Fill(0x2A);
            writer.Advance(3);
            Assert.AreEqual(3, writer.Count);
            writer.Clear();
            Assert.AreEqual(0, writer.Count);
            // Because we requested ClearOnReset, ensure zeroed (best-effort - underlying may still contain zeros if cleared).
            Span<byte> span = writer.GetSpan(3);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, span[i]);
            }
        }

        [TestMethod]
        public void DisposePreventsFurtherUse()
        {
            PooledBufferWriter<byte> writer = new(initialCapacity: 8);
            writer.GetSpan(2)[0] = 1;
            writer.Advance(1);
            writer.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => writer.GetSpan(1));
        }
    }
}
#endif