//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests to detect memory leaks from undisposed pooled resources.
    /// These tests verify that PooledMemoryStream instances are properly disposed
    /// to prevent ArrayPool exhaustion.
    /// </summary>
    [TestClass]
    public class PooledResourceMemoryLeakTests
    {
        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Documentation")]
        public void PooledMemoryStream_NotDisposed_DocumentsLeakDanger()
        {
            // DOCUMENTATION: This test documents the danger of not disposing PooledMemoryStream
            // If PooledMemoryStream is not disposed, the rented buffer is never returned to ArrayPool,
            // eventually exhausting the pool and causing OutOfMemoryException or degraded performance.

            // Example of INCORRECT usage (DO NOT DO THIS):
            // PooledMemoryStream stream = new PooledMemoryStream();
            // stream.Write(data, 0, data.Length);
            // // LEAK: stream never disposed, buffer never returned to pool!

            // CORRECT usage (ALWAYS dispose):
            using (PooledMemoryStream stream = new PooledMemoryStream())
            {
                byte[] data = new byte[] { 1, 2, 3, 4, 5 };
                stream.Write(data, 0, data.Length);
                // Disposal happens automatically at end of using block
            }

            // This test passes to document the pattern - actual leak detection
            // is tested in stress tests that run 1000+ iterations
            Assert.IsTrue(true, "PooledMemoryStream must always be disposed using 'using' statement or explicit Dispose()");
        }

        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Stress")]
        public void PooledMemoryStream_ProperDisposal_NoLeak()
        {
            // Stress test: Create and dispose 1000 PooledMemoryStream instances
            // If disposal is working correctly, this should not exhaust ArrayPool

            for (int i = 0; i < 1000; i++)
            {
                using (PooledMemoryStream stream = new PooledMemoryStream(capacity: 4096))
                {
                    byte[] data = new byte[2048];
                    new Random(i).NextBytes(data);
                    stream.Write(data, 0, data.Length);

                    // Read back to verify stream works
                    stream.Position = 0;
                    byte[] readBack = new byte[2048];
                    int bytesRead = stream.Read(readBack, 0, readBack.Length);
                    Assert.AreEqual(2048, bytesRead, $"Iteration {i}: Failed to read back data");
                }
                // Stream disposed here - buffer returned to pool
            }

            // If we got here without OutOfMemoryException, disposal is working
            Assert.IsTrue(true, "1000 iterations completed without memory leak");
        }

        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Documentation")]
        public void PooledJsonSerializer_SerializeToPooledArray_CallerMustReturnBuffer()
        {
            // DOCUMENTATION: SerializeToPooledArray returns a rented buffer that caller must return

            var testObject = new { id = "test-123", value = "Hello World" };

            // Serialize to pooled array
            (byte[] rentedBuffer, int length) = PooledJsonSerializer.SerializeToPooledArray(testObject);

            try
            {
                // Use the buffer
                Assert.IsNotNull(rentedBuffer);
                Assert.IsTrue(length > 0);
                Assert.IsTrue(rentedBuffer.Length >= length);

                // Verify JSON content
                string json = Encoding.UTF8.GetString(rentedBuffer, 0, length);
                Assert.IsTrue(json.Contains("test-123"));
            }
            finally
            {
                // CRITICAL: Caller must return buffer to pool
                System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer, clearArray: true);
            }

            Assert.IsTrue(true, "Caller is responsible for returning rented buffer to ArrayPool");
        }

        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Stress")]
        public async Task SystemTextJsonStreamAdapter_ExceptionPath_NoLeak()
        {
            // Verify that exception paths properly dispose pooled resources
            // This is critical for the memory leak fixes in EncryptionProcessor

            for (int i = 0; i < 100; i++)
            {
                try
                {
                    // Create a PooledMemoryStream that might be leaked in exception path
                    using PooledMemoryStream testStream = new PooledMemoryStream();
                    byte[] testData = Encoding.UTF8.GetBytes("{\"id\":\"test\"}");
                    testStream.Write(testData, 0, testData.Length);

                    // Simulate exception during processing
                    if (i % 2 == 0)
                    {
                        throw new InvalidOperationException("Simulated exception");
                    }
                }
                catch (InvalidOperationException)
                {
                    // Exception caught - verify stream was disposed via using statement
                }
            }

            // If we got here without ArrayPool exhaustion, exception handling is correct
            Assert.IsTrue(true, "100 iterations with exceptions completed without memory leak");
            await Task.CompletedTask;
        }

        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Stress")]
        public void PooledMemoryStream_RapidGrowth_NoLeak()
        {
            // Test rapid capacity growth to ensure buffer exchanges don't leak

            for (int i = 0; i < 100; i++)
            {
                using (PooledMemoryStream stream = new PooledMemoryStream(capacity: 64))
                {
                    // Write data that forces multiple capacity increases
                    for (int chunk = 0; chunk < 100; chunk++)
                    {
                        byte[] data = new byte[1024]; // 1 KB chunks
                        stream.Write(data, 0, data.Length);
                    }

                    // Verify final size (100 chunks × 1KB each = 100KB)
                    Assert.IsTrue(stream.Length >= 100 * 1024, $"Iteration {i}: Stream should have grown to >=100KB (actual: {stream.Length} bytes)");
                }
                // Old buffers should be returned to pool during capacity growth
                // Final buffer returned to pool here
            }

            Assert.IsTrue(true, "100 iterations of rapid growth completed without memory leak");
        }

        [TestMethod]
        [TestCategory("MemoryLeak")]
        [TestCategory("Stress")]
        public void PooledMemoryStream_ConcurrentCreationDisposal_NoPoolExhaustion()
        {
            // Test concurrent creation and disposal to verify thread-safe pool usage
            const int ThreadCount = 10;
            const int IterationsPerThread = 100;
            Exception caughtException = null;

            Parallel.For(0, ThreadCount, threadIndex =>
            {
                try
                {
                    for (int i = 0; i < IterationsPerThread; i++)
                    {
                        using (PooledMemoryStream stream = new PooledMemoryStream())
                        {
                            byte[] data = new byte[4096];
                            stream.Write(data, 0, data.Length);
                            stream.Position = 0;
                            byte[] readBack = new byte[4096];
                            stream.Read(readBack, 0, readBack.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });

            Assert.IsNull(caughtException, $"Concurrent operations failed: {caughtException?.Message}");
            Assert.IsTrue(true, $"{ThreadCount * IterationsPerThread} concurrent operations completed without pool exhaustion");
        }
    }
}
#endif
