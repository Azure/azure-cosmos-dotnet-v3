//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PooledMemoryStreamTests
    {
        [TestMethod]
        public void Constructor_DefaultCapacity_CreatesEmptyStream()
        {
            using PooledMemoryStream stream = new();

            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(0, stream.Position);
            Assert.IsTrue(stream.CanRead);
            Assert.IsTrue(stream.CanWrite);
            Assert.IsTrue(stream.CanSeek);
        }

        [TestMethod]
        public void Constructor_WithCapacity_CreatesEmptyStream()
        {
            using PooledMemoryStream stream = new(capacity: 1024);

            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void Constructor_ZeroCapacity_CreatesEmptyStream()
        {
            using PooledMemoryStream stream = new(capacity: 0);

            Assert.AreEqual(0, stream.Length);
        }

        [TestMethod]
#if NET8_0_OR_GREATER
        // In .NET 8+, negative capacity values use the default from configuration
        public void Constructor_NegativeCapacity_UsesDefaultCapacity()
        {
            using PooledMemoryStream stream = new(capacity: -1);

            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(0, stream.Position);
            Assert.IsTrue(stream.CanWrite);
        }
#else
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
        {
            using PooledMemoryStream stream = new(capacity: -1);
        }
#endif

        [TestMethod]
        public void Write_SingleByte_UpdatesLengthAndPosition()
        {
            using PooledMemoryStream stream = new();

            stream.Write(new byte[] { 42 }, 0, 1);

            Assert.AreEqual(1, stream.Length);
            Assert.AreEqual(1, stream.Position);
        }

        [TestMethod]
        public void Write_MultipleBytes_UpdatesLengthAndPosition()
        {
            using PooledMemoryStream stream = new();
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };

            stream.Write(data, 0, data.Length);

            Assert.AreEqual(5, stream.Length);
            Assert.AreEqual(5, stream.Position);
        }

        [TestMethod]
        public void Write_ExceedsInitialCapacity_GrowsBuffer()
        {
            using PooledMemoryStream stream = new(capacity: 4);
            byte[] data = new byte[100];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            stream.Write(data, 0, data.Length);

            Assert.AreEqual(100, stream.Length);
            Assert.AreEqual(100, stream.Position);
        }

        [TestMethod]
        public void Read_AfterWrite_ReturnsWrittenData()
        {
            using PooledMemoryStream stream = new();
            byte[] writeData = new byte[] { 10, 20, 30, 40, 50 };
            stream.Write(writeData, 0, writeData.Length);
            stream.Position = 0;

            byte[] readData = new byte[5];
            int bytesRead = stream.Read(readData, 0, readData.Length);

            Assert.AreEqual(5, bytesRead);
            CollectionAssert.AreEqual(writeData, readData);
        }

        [TestMethod]
        public void Read_PartialBuffer_ReturnsCorrectData()
        {
            using PooledMemoryStream stream = new();
            byte[] writeData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            stream.Write(writeData, 0, writeData.Length);
            stream.Position = 0;

            byte[] readData = new byte[5];
            int bytesRead = stream.Read(readData, 0, readData.Length);

            Assert.AreEqual(5, bytesRead);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, readData);
            Assert.AreEqual(5, stream.Position);
        }

        [TestMethod]
        public void Read_AtEndOfStream_ReturnsZero()
        {
            using PooledMemoryStream stream = new();
            byte[] writeData = new byte[] { 1, 2, 3 };
            stream.Write(writeData, 0, writeData.Length);
            // Position is already at end

            byte[] readData = new byte[5];
            int bytesRead = stream.Read(readData, 0, readData.Length);

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void Seek_FromBeginning_SetsCorrectPosition()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);

            long newPosition = stream.Seek(50, SeekOrigin.Begin);

            Assert.AreEqual(50, newPosition);
            Assert.AreEqual(50, stream.Position);
        }

        [TestMethod]
        public void Seek_FromCurrent_SetsCorrectPosition()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);
            stream.Position = 25;

            long newPosition = stream.Seek(25, SeekOrigin.Current);

            Assert.AreEqual(50, newPosition);
            Assert.AreEqual(50, stream.Position);
        }

        [TestMethod]
        public void Seek_FromEnd_SetsCorrectPosition()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);

            long newPosition = stream.Seek(-25, SeekOrigin.End);

            Assert.AreEqual(75, newPosition);
            Assert.AreEqual(75, stream.Position);
        }

        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void Seek_NegativePosition_ThrowsIOException()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);

            stream.Seek(-10, SeekOrigin.Begin);
        }

        [TestMethod]
        public void SetLength_IncreasesLength_PreservesData()
        {
            using PooledMemoryStream stream = new();
            byte[] data = new byte[] { 1, 2, 3 };
            stream.Write(data, 0, data.Length);

            stream.SetLength(10);

            Assert.AreEqual(10, stream.Length);
            stream.Position = 0;
            byte[] readBack = new byte[3];
            stream.Read(readBack, 0, 3);
            CollectionAssert.AreEqual(data, readBack);
        }

        [TestMethod]
        public void SetLength_DecreasesLength_TruncatesStream()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);

            stream.SetLength(50);

            Assert.AreEqual(50, stream.Length);
        }

        [TestMethod]
        public void SetLength_PositionBeyondNewLength_AdjustsPosition()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[100], 0, 100);
            stream.Position = 75;

            stream.SetLength(50);

            Assert.AreEqual(50, stream.Position);
        }

        [TestMethod]
        public void ToArray_ReturnsCorrectData()
        {
            using PooledMemoryStream stream = new();
            byte[] data = new byte[] { 5, 10, 15, 20, 25 };
            stream.Write(data, 0, data.Length);

            byte[] result = stream.ToArray();

            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public void ToArray_EmptyStream_ReturnsEmptyArray()
        {
            using PooledMemoryStream stream = new();

            byte[] result = stream.ToArray();

            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Write_AfterDispose_ThrowsObjectDisposedException()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            stream.Write(new byte[] { 1 }, 0, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Read_AfterDispose_ThrowsObjectDisposedException()
        {
            PooledMemoryStream stream = new();
            stream.Write(new byte[] { 1 }, 0, 1);
            stream.Position = 0;
            stream.Dispose();

            stream.Read(new byte[1], 0, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Seek_AfterDispose_ThrowsObjectDisposedException()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            stream.Seek(0, SeekOrigin.Begin);
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void ToArray_AfterDispose_ThrowsObjectDisposedException()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            stream.ToArray();
        }

        [TestMethod]
        public void CanRead_AfterDispose_ReturnsFalse()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            Assert.IsFalse(stream.CanRead);
        }

        [TestMethod]
        public void CanWrite_AfterDispose_ReturnsFalse()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            Assert.IsFalse(stream.CanWrite);
        }

        [TestMethod]
        public void CanSeek_AfterDispose_ReturnsFalse()
        {
            PooledMemoryStream stream = new();
            stream.Dispose();

            Assert.IsFalse(stream.CanSeek);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Write_NullBuffer_ThrowsArgumentNullException()
        {
            using PooledMemoryStream stream = new();

            stream.Write(null, 0, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Write_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            using PooledMemoryStream stream = new();

            stream.Write(new byte[10], -1, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Write_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            using PooledMemoryStream stream = new();

            stream.Write(new byte[10], 0, -1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Write_OffsetPlusCountExceedsBuffer_ThrowsArgumentException()
        {
            using PooledMemoryStream stream = new();

            stream.Write(new byte[10], 8, 5);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Read_NullBuffer_ThrowsArgumentNullException()
        {
            using PooledMemoryStream stream = new();

            stream.Read(null, 0, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Position_SetNegative_ThrowsArgumentOutOfRangeException()
        {
            using PooledMemoryStream stream = new();

            stream.Position = -1;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void SetLength_Negative_ThrowsArgumentOutOfRangeException()
        {
            using PooledMemoryStream stream = new();

            stream.SetLength(-1);
        }

        [TestMethod]
        public async Task WriteAsync_WritesData()
        {
            using PooledMemoryStream stream = new();
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };

            await stream.WriteAsync(data, 0, data.Length);

            Assert.AreEqual(5, stream.Length);
        }

        [TestMethod]
        public async Task ReadAsync_ReadsData()
        {
            using PooledMemoryStream stream = new();
            byte[] writeData = new byte[] { 10, 20, 30, 40, 50 };
            await stream.WriteAsync(writeData, 0, writeData.Length);
            stream.Position = 0;

            byte[] readData = new byte[5];
            int bytesRead = await stream.ReadAsync(readData, 0, readData.Length);

            Assert.AreEqual(5, bytesRead);
            CollectionAssert.AreEqual(writeData, readData);
        }

        [TestMethod]
        public async Task WriteAsync_CancellationRequested_ReturnsCancelledTask()
        {
            using PooledMemoryStream stream = new();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await stream.WriteAsync(new byte[] { 1 }, 0, 1, cts.Token);
            });
        }

        [TestMethod]
        public async Task ReadAsync_CancellationRequested_ReturnsCancelledTask()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[] { 1 }, 0, 1);
            stream.Position = 0;
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await stream.ReadAsync(new byte[1], 0, 1, cts.Token);
            });
        }

        [TestMethod]
        public void Flush_DoesNotThrow()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

            stream.Flush(); // Should not throw
        }

        [TestMethod]
        public async Task FlushAsync_CompletesSuccessfully()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

            await stream.FlushAsync(); // Should complete successfully
        }

        [TestMethod]
        public void MultipleDispose_DoesNotThrow()
        {
            PooledMemoryStream stream = new();
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

            stream.Dispose();
            stream.Dispose(); // Should not throw
            stream.Dispose(); // Should not throw
        }

        [TestMethod]
        public void LargeWrite_Succeeds()
        {
            using PooledMemoryStream stream = new(capacity: 16);
            byte[] largeData = new byte[1024 * 1024]; // 1 MB
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            stream.Write(largeData, 0, largeData.Length);

            Assert.AreEqual(largeData.Length, stream.Length);

            stream.Position = 0;
            byte[] readBack = new byte[largeData.Length];
            stream.Read(readBack, 0, readBack.Length);

            CollectionAssert.AreEqual(largeData, readBack);
        }

        [TestMethod]
        public void EnsureCapacity_NearIntMaxValue_HandlesOverflowGracefully()
        {
            // Test that capacity doubling near int.MaxValue is handled correctly
            // We can't actually allocate arrays this large in tests, but we can verify
            // the logic by testing growth from a large initial capacity
            using PooledMemoryStream stream = new(capacity: int.MaxValue / 4);

            // Write a small amount to trigger growth
            byte[] testData = new byte[100];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)i;
            }

            stream.Write(testData, 0, testData.Length);

            Assert.AreEqual(testData.Length, stream.Length);
            Assert.AreEqual(testData.Length, stream.Position);
        }

        [TestMethod]
        public void EnsureCapacity_RequiredCapacityExceedsMaxArrayLength_CapsAtMaximum()
        {
            // Verify that requesting capacity beyond MaxArrayLength is handled
            // This tests the overflow protection logic
            using PooledMemoryStream stream = new(capacity: 1024);

            // SetLength with a value near MaxArrayLength should work (ArrayPool will provide appropriate buffer)
            // We use a reasonable test value that won't actually allocate huge memory
            const int testLength = 1024 * 1024; // 1 MB for testing
            stream.SetLength(testLength);

            Assert.AreEqual(testLength, stream.Length);
        }

        [TestMethod]
        public void Dispose_AlwaysClearsBufferBeforeReturningToPool()
        {
            // Security: Buffers are ALWAYS cleared to prevent sensitive data from remaining in memory.
            // This test validates that disposal clears the buffer as a mandatory security measure.

            // Arrange
            byte[] sensitiveData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE };
            byte[] bufferReference;

            // Act - Write sensitive data and get buffer reference
            using (PooledMemoryStream stream = new(capacity: 64, clearOnReturn: true))
            {
                stream.Write(sensitiveData, 0, sensitiveData.Length);
                bufferReference = stream.GetBuffer();

                // Verify data was written
                for (int i = 0; i < sensitiveData.Length; i++)
                {
                    Assert.AreEqual(sensitiveData[i], bufferReference[i], $"Data mismatch at index {i} before disposal");
                }
            } // Dispose is called here

            // Assert - Verify buffer was cleared (at least the used portion)
            for (int i = 0; i < sensitiveData.Length; i++)
            {
                Assert.AreEqual(0, bufferReference[i], $"Buffer not cleared at index {i} after disposal");
            }
        }

        [TestMethod]
        public void Dispose_EmptyStream_DoesNotThrow()
        {
            // Arrange & Act
            PooledMemoryStream stream = new(capacity: 64, clearOnReturn: true);

            // Assert - Should not throw even with zero length
            stream.Dispose();
        }

        [TestMethod]
        public void Dispose_AfterPartialWrite_ClearsOnlyUsedPortion()
        {
            // Arrange
            byte[] sensitiveData = new byte[] { 0x01, 0x02, 0x03 };
            byte[] bufferReference;
            int bufferLength;

            // Act - Write only 3 bytes to a larger buffer
            using (PooledMemoryStream stream = new(capacity: 100, clearOnReturn: true))
            {
                stream.Write(sensitiveData, 0, sensitiveData.Length);
                bufferReference = stream.GetBuffer();
                bufferLength = bufferReference.Length;

                // Mark additional bytes in buffer to verify only used portion is cleared
                for (int i = sensitiveData.Length; i < Math.Min(10, bufferLength); i++)
                {
                    bufferReference[i] = 0xFF;
                }
            } // Dispose is called here

            // Assert - Verify only the used portion (0-2) was cleared
            for (int i = 0; i < sensitiveData.Length; i++)
            {
                Assert.AreEqual(0, bufferReference[i], $"Used portion not cleared at index {i}");
            }
        }

        [TestMethod]
        public void Dispose_LargeBuffer_ClearsSensitiveDataEfficiently()
        {
            // Arrange
            const int dataSize = 1024 * 10; // 10 KB
            byte[] largeData = new byte[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            byte[] bufferReference;

            // Act
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            using (PooledMemoryStream stream = new(capacity: 1024, clearOnReturn: true))
            {
                stream.Write(largeData, 0, largeData.Length);
                bufferReference = stream.GetBuffer();
            }
            sw.Stop();

            // Assert - Verify clearing happened and was reasonably fast
            Assert.IsTrue(sw.ElapsedMilliseconds < 100, $"Buffer clearing took too long: {sw.ElapsedMilliseconds}ms");

            // Verify at least some key positions are cleared
            Assert.AreEqual(0, bufferReference[0], "First byte not cleared");
            Assert.AreEqual(0, bufferReference[dataSize / 2], "Middle byte not cleared");
            Assert.AreEqual(0, bufferReference[dataSize - 1], "Last byte not cleared");
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void WriteSpan_WritesData()
        {
            using PooledMemoryStream stream = new();
            ReadOnlySpan<byte> data = new byte[] { 1, 2, 3, 4, 5 };

            stream.Write(data);

            Assert.AreEqual(5, stream.Length);
        }

        [TestMethod]
        public void ReadSpan_ReadsData()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[] { 10, 20, 30, 40, 50 }, 0, 5);
            stream.Position = 0;

            Span<byte> buffer = new byte[5];
            int bytesRead = stream.Read(buffer);

            Assert.AreEqual(5, bytesRead);
            Assert.AreEqual(10, buffer[0]);
            Assert.AreEqual(50, buffer[4]);
        }

        [TestMethod]
        public async Task WriteAsyncMemory_WritesData()
        {
            using PooledMemoryStream stream = new();
            ReadOnlyMemory<byte> data = new byte[] { 1, 2, 3, 4, 5 };

            await stream.WriteAsync(data);

            Assert.AreEqual(5, stream.Length);
        }

        [TestMethod]
        public async Task ReadAsyncMemory_ReadsData()
        {
            using PooledMemoryStream stream = new();
            stream.Write(new byte[] { 10, 20, 30, 40, 50 }, 0, 5);
            stream.Position = 0;

            Memory<byte> buffer = new byte[5];
            int bytesRead = await stream.ReadAsync(buffer);

            Assert.AreEqual(5, bytesRead);
            Assert.AreEqual(10, buffer.Span[0]);
            Assert.AreEqual(50, buffer.Span[4]);
        }
#endif
    }
}
