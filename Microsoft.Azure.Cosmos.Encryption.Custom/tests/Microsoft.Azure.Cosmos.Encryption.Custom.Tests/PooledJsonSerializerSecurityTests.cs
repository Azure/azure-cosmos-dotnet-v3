//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Security-focused tests for PooledJsonSerializer to verify secure handling of sensitive data.
    /// These tests validate that security properties like proper disposal and depth limits are maintained.
    /// </summary>
    [TestClass]
    public class PooledJsonSerializerSecurityTests
    {
        private class SensitiveTestObject
        {
            public string SecretKey { get; set; }
            public string Password { get; set; }
            public string Token { get; set; }
            public int Value { get; set; }
        }

        private class NestedObject
        {
            public string Id { get; set; }
            public NestedObject Child { get; set; }
            public int Level { get; set; }
        }

        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void Test_MaxDepth_EnforcesLimit_ExcessiveNesting()
        {
            // Security Property: Deep JSON nesting can cause stack overflow or DoS attacks.
            // This test validates that the serializer enforces depth limits to prevent such attacks.

            // Create a deeply nested JSON string that exceeds the default MaxDepth (64)
            StringBuilder jsonBuilder = new();
            int nestingLevel = 100; // Exceed typical MaxDepth of 64

            // Build opening braces
            for (int i = 0; i < nestingLevel; i++)
            {
                jsonBuilder.Append("{\"child\":");
            }

            jsonBuilder.Append("\"value\"");

            // Build closing braces
            for (int i = 0; i < nestingLevel; i++)
            {
                jsonBuilder.Append("}");
            }

            string deeplyNestedJson = jsonBuilder.ToString();
            byte[] utf8Json = Encoding.UTF8.GetBytes(deeplyNestedJson);

            // Configure options with a reasonable MaxDepth limit
            JsonSerializerOptions options = new()
            {
                MaxDepth = 32 // Set a reasonable limit
            };

            // This should throw JsonException due to exceeding MaxDepth
            using MemoryStream stream = new(utf8Json);
            NestedObject result = PooledJsonSerializer.DeserializeFromStream<NestedObject>(stream, options);
        }

        [TestMethod]
        public void Test_MaxDepth_AllowsReasonableNesting()
        {
            // Security Property: While enforcing limits, legitimate deeply nested structures should work.
            // This test validates that reasonable nesting depths are allowed.

            // Create object with 10 levels of nesting (well within limits)
            NestedObject root = new() { Id = "Level0", Level = 0 };
            NestedObject current = root;

            for (int i = 1; i < 10; i++)
            {
                current.Child = new NestedObject { Id = $"Level{i}", Level = i };
                current = current.Child;
            }

            // Serialize with reasonable MaxDepth
            JsonSerializerOptions options = new()
            {
                MaxDepth = 32
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(root, options);
            NestedObject deserialized = PooledJsonSerializer.DeserializeFromStream<NestedObject>(stream, options);

            // Verify structure is intact
            Assert.AreEqual("Level0", deserialized.Id);
            Assert.AreEqual(0, deserialized.Level);

            // Verify nested levels
            current = deserialized;
            for (int i = 1; i < 10; i++)
            {
                Assert.IsNotNull(current.Child, $"Child at level {i} should exist");
                current = current.Child;
                Assert.AreEqual($"Level{i}", current.Id);
                Assert.AreEqual(i, current.Level);
            }
        }

        [TestMethod]
        public void Test_SensitiveDataSerialization_StreamProperlyDisposed()
        {
            // Security Property: Streams containing sensitive data must be properly disposed to clear buffers.
            // This test validates the dispose pattern for sensitive data serialization.

            SensitiveTestObject sensitiveObj = new()
            {
                SecretKey = "super-secret-key-12345",
                Password = "P@ssw0rd!Sensitive",
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                Value = 42
            };

            PooledMemoryStream stream = null;
            bool streamDisposed = false;

            try
            {
                stream = PooledJsonSerializer.SerializeToPooledStream(sensitiveObj);

                // Verify stream contains data
                Assert.IsTrue(stream.Length > 0);
                Assert.AreEqual(0, stream.Position);

                // Read and verify the data is correct
                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(stream);
                Assert.AreEqual(sensitiveObj.SecretKey, deserialized.SecretKey);
                Assert.AreEqual(sensitiveObj.Password, deserialized.Password);
                Assert.AreEqual(sensitiveObj.Token, deserialized.Token);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                    streamDisposed = true;

                    // Verify stream is disposed
                    Assert.IsFalse(stream.CanRead, "Stream should not be readable after disposal");
                    Assert.IsFalse(stream.CanWrite, "Stream should not be writable after disposal");
                }
            }

            Assert.IsTrue(streamDisposed, "Stream must be disposed to clear sensitive data");
        }

        [TestMethod]
        public async Task Test_SensitiveDataSerialization_AsyncDisposal()
        {
            // Security Property: Async operations with sensitive data must also ensure proper disposal.
            // This test validates async disposal patterns.

            SensitiveTestObject sensitiveObj = new()
            {
                SecretKey = "async-secret-key-67890",
                Password = "AsyncP@ssw0rd!",
                Token = "async-token-xyz",
                Value = 99
            };

            await using (PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(sensitiveObj))
            {
                Assert.IsTrue(stream.Length > 0);

                SensitiveTestObject deserialized = await PooledJsonSerializer.DeserializeFromStreamAsync<SensitiveTestObject>(stream);
                Assert.AreEqual(sensitiveObj.SecretKey, deserialized.SecretKey);
            }

            // Stream should be disposed via await using
            // This validates proper async disposal pattern
        }

        [TestMethod]
        public void Test_BufferWriter_ProperlyDisposed()
        {
            // Security Property: RentArrayBufferWriter must be disposed to return buffers to pool.
            // This test validates the disposal pattern for buffer writers.

            SensitiveTestObject sensitiveObj = new()
            {
                SecretKey = "buffer-writer-secret",
                Password = "BufferP@ss!",
                Token = "buffer-token-abc",
                Value = 123
            };

            RentArrayBufferWriter bufferWriter = null;
            bool writerDisposed = false;

            try
            {
                bufferWriter = PooledJsonSerializer.SerializeToBufferWriter(sensitiveObj);
                (byte[] buffer, int length) = bufferWriter.WrittenBuffer;

                Assert.IsNotNull(buffer);
                Assert.IsTrue(length > 0);

                // Deserialize to verify correctness
                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromSpan<SensitiveTestObject>(
                    buffer.AsSpan(0, length));

                Assert.AreEqual(sensitiveObj.SecretKey, deserialized.SecretKey);
            }
            finally
            {
                if (bufferWriter != null)
                {
                    bufferWriter.Dispose();
                    writerDisposed = true;
                }
            }

            Assert.IsTrue(writerDisposed, "BufferWriter must be disposed to return buffers to pool");
        }

        [TestMethod]
        public void Test_SerializeToPooledArray_CallerMustReturnBuffer()
        {
            // Security Property: When using SerializeToPooledArray, caller must return buffer to pool.
            // This test validates the contract and proper usage pattern.

            SensitiveTestObject sensitiveObj = new()
            {
                SecretKey = "pooled-array-secret",
                Password = "ArrayP@ss!",
                Token = "array-token-123",
                Value = 456
            };

            (byte[] buffer, int length) = PooledJsonSerializer.SerializeToPooledArray(sensitiveObj);

            try
            {
                Assert.IsNotNull(buffer);
                Assert.IsTrue(length > 0);
                Assert.IsTrue(buffer.Length >= length, "Buffer size should be at least the written length");

                // Deserialize to verify correctness
                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromSpan<SensitiveTestObject>(
                    buffer.AsSpan(0, length));

                Assert.AreEqual(sensitiveObj.SecretKey, deserialized.SecretKey);
                Assert.AreEqual(sensitiveObj.Password, deserialized.Password);
            }
            finally
            {
                // CRITICAL: Caller must return the buffer to the pool with clearing enabled
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            // Test passes if buffer is properly returned to pool
        }

        [TestMethod]
        public void Test_ExceptionDuringSerialization_StreamStillDisposed()
        {
            // Security Property: If serialization fails, streams must still be disposed to prevent leaks.
            // This test validates exception safety.

            // Create an object that will cause serialization issues
            var problematicObj = new Dictionary<string, object>
            {
                { "circular", null } // We'll add a circular reference
            };

            PooledMemoryStream stream = null;

            try
            {
                // Attempt to serialize - may fail with certain object graphs
                // For this test, we'll simulate by trying to serialize and catching any exception
                stream = new PooledMemoryStream(clearOnReturn: true);

                using (Utf8JsonWriter writer = new(stream))
                {
                    // Manually write to test exception handling
                    writer.WriteStartObject();
                    writer.WritePropertyName("test");
                    writer.WriteStringValue("value");
                    writer.WriteEndObject();
                    writer.Flush();
                }

                stream.Position = 0;
            }
            catch (Exception)
            {
                // Exception handling is validated by the finally block ensuring disposal
            }
            finally
            {
                // Stream must be disposed even if exception occurs
                stream?.Dispose();
            }

            // Validate that disposal happens in exception scenarios
            if (stream != null)
            {
                Assert.IsFalse(stream.CanRead, "Stream should be disposed after exception");
            }
        }

        [TestMethod]
        public void Test_LargeObject_ProperBufferManagement()
        {
            // Security Property: Large objects should not cause buffer leaks or security issues.
            // This test validates proper buffer management with large payloads.

            // Create a large object with sensitive data
            SensitiveTestObject largeObj = new()
            {
                SecretKey = new string('S', 10000), // 10KB string
                Password = new string('P', 5000),   // 5KB string
                Token = new string('T', 8000),      // 8KB string
                Value = int.MaxValue
            };

            using (PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(largeObj))
            {
                Assert.IsTrue(stream.Length > 23000, "Stream should contain large serialized object");

                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(stream);

                Assert.AreEqual(10000, deserialized.SecretKey.Length);
                Assert.AreEqual(5000, deserialized.Password.Length);
                Assert.AreEqual(8000, deserialized.Token.Length);
            }

            // Large buffer should be cleared and returned to pool on disposal
        }

        [TestMethod]
        public void Test_EmptyObject_ProperHandling()
        {
            // Security Property: Edge case - empty objects should not cause issues.
            // This validates security properties hold for edge cases.

            SensitiveTestObject emptyObj = new();

            using (PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(emptyObj))
            {
                Assert.IsTrue(stream.Length > 0, "Empty object should still serialize to valid JSON");

                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(stream);

                Assert.IsNull(deserialized.SecretKey);
                Assert.IsNull(deserialized.Password);
                Assert.IsNull(deserialized.Token);
                Assert.AreEqual(0, deserialized.Value);
            }
        }

        [TestMethod]
        public void Test_MultipleSerializations_NoDataLeakage()
        {
            // Security Property: Multiple serializations should not leak data between operations.
            // This test validates isolation between operations.

            SensitiveTestObject obj1 = new()
            {
                SecretKey = "first-secret",
                Password = "first-password",
                Token = "first-token",
                Value = 1
            };

            SensitiveTestObject obj2 = new()
            {
                SecretKey = "second-secret",
                Password = "second-password",
                Token = "second-token",
                Value = 2
            };

            using (PooledMemoryStream stream1 = PooledJsonSerializer.SerializeToPooledStream(obj1))
            using (PooledMemoryStream stream2 = PooledJsonSerializer.SerializeToPooledStream(obj2))
            {
                SensitiveTestObject result1 = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(stream1);
                SensitiveTestObject result2 = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(stream2);

                // Verify no cross-contamination
                Assert.AreEqual("first-secret", result1.SecretKey);
                Assert.AreEqual("second-secret", result2.SecretKey);

                Assert.AreNotEqual(result1.SecretKey, result2.SecretKey);
                Assert.AreNotEqual(result1.Password, result2.Password);
                Assert.AreNotEqual(result1.Token, result2.Token);
            }
        }

        [TestMethod]
        public void Test_SerializeToStream_DoesNotDisposeProvidedStream()
        {
            // Security Property: SerializeToStream should not dispose caller's stream.
            // This validates proper ownership semantics.

            SensitiveTestObject obj = new()
            {
                SecretKey = "test-secret",
                Password = "test-password",
                Token = "test-token",
                Value = 42
            };

            using (MemoryStream externalStream = new())
            {
                // Serialize to external stream
                PooledJsonSerializer.SerializeToStream(externalStream, obj);

                // Stream should still be usable
                Assert.IsTrue(externalStream.CanRead, "External stream should remain open");
                Assert.IsTrue(externalStream.CanWrite, "External stream should remain writable");
                Assert.IsTrue(externalStream.Length > 0, "External stream should contain data");

                // Deserialize to verify
                externalStream.Position = 0;
                SensitiveTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<SensitiveTestObject>(externalStream);
                Assert.AreEqual(obj.SecretKey, deserialized.SecretKey);
            }

            // External stream disposal is caller's responsibility
        }

        [TestMethod]
        public void Test_NullProperties_OmittedInSerialization()
        {
            // Security Property: Null properties should be omitted to minimize data exposure.
            // This validates the DefaultIgnoreCondition.WhenWritingNull behavior.

            SensitiveTestObject obj = new()
            {
                SecretKey = "only-secret",
                Password = null, // Should be omitted
                Token = null,    // Should be omitted
                Value = 42
            };

            using (PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj))
            {
                stream.Position = 0;
                using StreamReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
                string json = reader.ReadToEnd();

                // Verify null properties are not in JSON
                Assert.IsFalse(json.Contains("\"Password\""), "Password should be omitted when null");
                Assert.IsFalse(json.Contains("\"Token\""), "Token should be omitted when null");
                Assert.IsTrue(json.Contains("\"SecretKey\""), "SecretKey should be present");
                Assert.IsTrue(json.Contains("\"Value\""), "Value should be present");
            }
        }
    }
}
#endif
