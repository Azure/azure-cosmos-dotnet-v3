namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
    using System;
    using System.Buffers;
    using System.IO;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StreamChunkedBufferWriterTests
    {
        private const int DefaultChunk = 128;

        [TestMethod]
        public void Constructor_NullStream_Throws()
        {
            using ArrayPoolManager pool = new();
            Assert.ThrowsException<ArgumentNullException>(() => new StreamChunkedBufferWriter(null, pool, 16));
        }

        [TestMethod]
        public void Constructor_NullPool_Throws()
        {
            using MemoryStream ms = new();
            Assert.ThrowsException<ArgumentNullException>(() => new StreamChunkedBufferWriter(ms, (ArrayPoolManager)null, 16));
        }

        [TestMethod]
        public void Constructor_InvalidChunkSize_Throws()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new StreamChunkedBufferWriter(ms, pool, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new StreamChunkedBufferWriter(ms, pool, -5));
        }

        [TestMethod]
        public void InitialGetSpanAllocatesAtLeastChunk()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, DefaultChunk);

            // Request with no hint -> should allocate chunk
            Span<byte> span = writer.GetSpan();
            Assert.IsTrue(span.Length >= 1); // contract: at least 1

            // Write less than chunk, ensure nothing flushed yet
            span[0] = 42;
            writer.Advance(1);
            Assert.AreEqual(0, ms.Length, "Should not flush until buffer full or final flush");
            Assert.AreEqual(1, writer.BytesWritten);
            Assert.AreEqual(0, writer.Flushes);
        }

        [TestMethod]
        public void SizeHintLargerThanChunkAllocatesLarger()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, DefaultChunk);

            int big = DefaultChunk * 3;
            Span<byte> span = writer.GetSpan(big);
            Assert.IsTrue(span.Length >= big);
            span[..big].Fill(1);
            writer.Advance(big);
            Assert.AreEqual(big, writer.BytesWritten);
            // Flush may or may not have occurred depending on actual rented size.
            writer.Dispose();
            Assert.AreEqual(big, ms.Length);
        }

        [TestMethod]
        public void MultipleSmallWritesTriggerFlushesOnFull()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 16);

            // Fill two full chunks via small writes
            for (int i = 0; i < 32; i++)
            {
                Span<byte> span = writer.GetSpan(1);
                span[0] = (byte)i;
                writer.Advance(1);
            }

            // Two full chunks should have been flushed
            Assert.AreEqual(32, ms.Length);
            Assert.AreEqual(32, writer.BytesWritten);
            Assert.AreEqual(2, writer.Flushes);

            // Add one more byte (new buffer)
            writer.GetSpan(1)[0] = 99;
            writer.Advance(1);
            Assert.AreEqual(32, ms.Length, "Third buffer not flushed yet");
            Assert.AreEqual(33, writer.BytesWritten);
            Assert.AreEqual(2, writer.Flushes);

            writer.Dispose(); // final flush
            Assert.AreEqual(33, ms.Length);
            Assert.AreEqual(3, writer.Flushes);
        }

        [TestMethod]
        public void FinalFlushWritesPartial()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            StreamChunkedBufferWriter writer = new(ms, pool, 8);

            writer.GetSpan(5)[..5].Fill(7);
            writer.Advance(5);
            Assert.AreEqual(0, ms.Length);
            writer.FinalFlush();
            Assert.AreEqual(5, ms.Length);
            Assert.AreEqual(1, writer.Flushes);

            // Idempotent
            writer.FinalFlush();
            Assert.AreEqual(5, ms.Length);
            Assert.AreEqual(1, writer.Flushes);

            writer.Dispose();
        }

        [TestMethod]
        public void DisposeIsIdempotent()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            StreamChunkedBufferWriter writer = new(ms, pool, 8);
            writer.GetSpan(3)[..3].Fill(9);
            writer.Advance(3);
            writer.Dispose();
            long afterFirst = ms.Length;
            int flushes = writer.Flushes;
            writer.Dispose();
            Assert.AreEqual(afterFirst, ms.Length);
            Assert.AreEqual(flushes, writer.Flushes);
        }

        [TestMethod]
        public void AdvanceThrowsWhenExceedingAvailable()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 16);
            Span<byte> span = writer.GetSpan(4);
            int available = span.Length; // remaining capacity in current buffer
            // Write half of available
            int half = Math.Min(available / 2, 4);
            span[..half].Fill(1);
            writer.Advance(half);
            // Now attempt to advance beyond remaining capacity
            int invalidAdvance = available - half + 1;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => writer.Advance(invalidAdvance));
        }

        [TestMethod]
        public void GetMemoryReturnsWritableMemory()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 32);

            Memory<byte> mem = writer.GetMemory(10);
            Assert.IsTrue(mem.Length >= 10);
            mem.Span[..10].Fill(3);
            writer.Advance(10);
            writer.Dispose();
            Assert.AreEqual(10, ms.Length);
        }

        [TestMethod]
        public void BytesWrittenTracksAcrossFlushBoundaries()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 4);

            // Write 10 bytes in varying sizes
            int total = 0;
            int[] sizes = new[] { 1, 2, 1, 4, 2 }; // sums to 10
            foreach (int s in sizes)
            {
                writer.GetSpan(s)[..s].Fill(1);
                writer.Advance(s);
                total += s;
                Assert.AreEqual(total, writer.BytesWritten);
            }

            writer.Dispose();
            Assert.AreEqual(10, ms.Length);
            Assert.AreEqual(total, writer.BytesWritten);
        }

        [TestMethod]
        public void EnsureCapacityAfterFlushAllocatesNewBuffer()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 4);

            // Fill entire first buffer (which could be >= chunk size)
            Span<byte> first = writer.GetSpan(4);
            int firstCapacity = first.Length;
            first[..firstCapacity].Fill(2);
            writer.Advance(firstCapacity);
            Assert.AreEqual(firstCapacity, ms.Length);
            Assert.AreEqual(1, writer.Flushes);
            // Next request should allocate a new buffer independent of previous
            writer.GetSpan(2)[..2].Fill(5);
            writer.Advance(2);
            Assert.AreEqual(firstCapacity, ms.Length); // Not flushed yet
            writer.Dispose();
            Assert.AreEqual(firstCapacity + 2, ms.Length);
            Assert.AreEqual(2, writer.Flushes);
        }

        [TestMethod]
        public void PatternIntegrity_MixedWrites()
        {
            const int chunkSize = 128;
            int total = (chunkSize * 5) + 37; // cross multiple flush boundaries with tail
            byte[] source = new byte[total];
            for (int i = 0; i < total; i++)
            {
                source[i] = (byte)(i % 251); // non-trivial repeating pattern
            }

            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, chunkSize);

            int[] sizesPattern = new[] { 1, 3, 7, 11, 64, 5, 13, 29, chunkSize - 1, 2 };
            int sizeIndex = 0;
            int written = 0;
            while (written < total)
            {
                int next = sizesPattern[sizeIndex++ % sizesPattern.Length];
                if (next > total - written)
                {
                    next = total - written;
                }

                Span<byte> span = writer.GetSpan(next);
                source.AsSpan(written, next).CopyTo(span);
                writer.Advance(next);
                written += next;
                Assert.AreEqual(written, writer.BytesWritten);
            }

            writer.Dispose();
            Assert.AreEqual(total, ms.Length);
            Assert.AreEqual(total, writer.BytesWritten);
            byte[] result = ms.ToArray();
            CollectionAssert.AreEqual(source, result);
        }

        [TestMethod]
        public void NegativeSizeHintThrows()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 16);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => writer.GetSpan(-5));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => writer.GetMemory(-10));
        }

        [TestMethod]
        public void FinalFlushWithNoData_NoFlushes()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            using StreamChunkedBufferWriter writer = new(ms, pool, 32);
            writer.FinalFlush();
            Assert.AreEqual(0, ms.Length);
            Assert.AreEqual(0, writer.Flushes);
        }

        [TestMethod]
        public void PostDispose_GetSpan_Throws()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            StreamChunkedBufferWriter writer = new(ms, pool, 8);
            writer.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => writer.GetSpan(1));
        }

        [TestMethod]
        public void PostDispose_GetMemory_Throws()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            StreamChunkedBufferWriter writer = new(ms, pool, 8);
            writer.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => writer.GetMemory(1));
        }

        [TestMethod]
        public void PostDispose_Advance_Throws()
        {
            using MemoryStream ms = new();
            using ArrayPoolManager pool = new();
            StreamChunkedBufferWriter writer = new(ms, pool, 8);
            writer.GetSpan(4)[..4].Fill(1);
            writer.Advance(4); // flush happens
            writer.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => writer.Advance(0));
        }
    }
#endif
}
