//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PooledJsonSerializerTests
    {
        private class TestObject
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public string NullableProperty { get; set; }
        }

        private class NestedTestObject
        {
            public string Id { get; set; }
            public TestObject Inner { get; set; }
            public int[] Numbers { get; set; }
        }

        [TestMethod]
        public void SerializeToPooledStream_SimpleObject_ReturnsValidStream()
        {
            TestObject obj = new()
            {
                Name = "Test",
                Value = 42,
                IsActive = true
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj);

            Assert.IsNotNull(stream);
            Assert.IsTrue(stream.Length > 0);
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void SerializeToPooledStream_ThenDeserialize_RoundTripsCorrectly()
        {
            TestObject original = new()
            {
                Name = "RoundTrip",
                Value = 123,
                IsActive = false
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(original);
            TestObject deserialized = PooledJsonSerializer.DeserializeFromStream<TestObject>(stream);

            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Value, deserialized.Value);
            Assert.AreEqual(original.IsActive, deserialized.IsActive);
        }

        [TestMethod]
        public void SerializeToPooledStream_NestedObject_SerializesCorrectly()
        {
            NestedTestObject obj = new()
            {
                Id = "nested-123",
                Inner = new TestObject { Name = "Inner", Value = 99, IsActive = true },
                Numbers = new[] { 1, 2, 3, 4, 5 }
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj);
            NestedTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<NestedTestObject>(stream);

            Assert.AreEqual(obj.Id, deserialized.Id);
            Assert.AreEqual(obj.Inner.Name, deserialized.Inner.Name);
            Assert.AreEqual(obj.Inner.Value, deserialized.Inner.Value);
            CollectionAssert.AreEqual(obj.Numbers, deserialized.Numbers);
        }

        [TestMethod]
        public void SerializeToPooledStream_NullProperty_OmitsProperty()
        {
            TestObject obj = new()
            {
                Name = "Test",
                Value = 1,
                IsActive = true,
                NullableProperty = null
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj);
            string json = ReadStreamAsString(stream);

            Assert.IsFalse(json.Contains("NullableProperty"));
        }

        [TestMethod]
        public void SerializeToStream_WritesToProvidedStream()
        {
            TestObject obj = new()
            {
                Name = "StreamTest",
                Value = 42,
                IsActive = true
            };

            using MemoryStream ms = new();
            PooledJsonSerializer.SerializeToStream(ms, obj);

            Assert.IsTrue(ms.Length > 0);
            ms.Position = 0;
            TestObject deserialized = JsonSerializer.Deserialize<TestObject>(ms);
            Assert.AreEqual(obj.Name, deserialized.Name);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SerializeToStream_NullStream_ThrowsArgumentNullException()
        {
            TestObject obj = new() { Name = "Test", Value = 1, IsActive = true };

            PooledJsonSerializer.SerializeToStream<TestObject>(null, obj);
        }

        [TestMethod]
        public async Task SerializeToStreamAsync_WritesToProvidedStream()
        {
            TestObject obj = new()
            {
                Name = "AsyncStreamTest",
                Value = 99,
                IsActive = false
            };

            using MemoryStream ms = new();
            await PooledJsonSerializer.SerializeToStreamAsync(ms, obj);

            Assert.IsTrue(ms.Length > 0);
            ms.Position = 0;
            TestObject deserialized = await JsonSerializer.DeserializeAsync<TestObject>(ms);
            Assert.AreEqual(obj.Name, deserialized.Name);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SerializeToStreamAsync_NullStream_ThrowsArgumentNullException()
        {
            TestObject obj = new() { Name = "Test", Value = 1, IsActive = true };

            await PooledJsonSerializer.SerializeToStreamAsync<TestObject>(null, obj);
        }

        [TestMethod]
        public void SerializeToBufferWriter_ReturnsValidBufferWriter()
        {
            TestObject obj = new()
            {
                Name = "BufferWriterTest",
                Value = 55,
                IsActive = true
            };

            using RentArrayBufferWriter bufferWriter = PooledJsonSerializer.SerializeToBufferWriter(obj);

            Assert.IsNotNull(bufferWriter);
            (byte[] buffer, int length) = bufferWriter.WrittenBuffer;
            Assert.IsTrue(length > 0);
        }

        [TestMethod]
        public void SerializeToBufferWriter_ThenDeserialize_RoundTripsCorrectly()
        {
            TestObject original = new()
            {
                Name = "BufferRoundTrip",
                Value = 77,
                IsActive = true
            };

            using RentArrayBufferWriter bufferWriter = PooledJsonSerializer.SerializeToBufferWriter(original);
            (byte[] buffer, int length) = bufferWriter.WrittenBuffer;

            TestObject deserialized = PooledJsonSerializer.DeserializeFromSpan<TestObject>(buffer.AsSpan(0, length));

            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Value, deserialized.Value);
            Assert.AreEqual(original.IsActive, deserialized.IsActive);
        }

        [TestMethod]
        public void DeserializeFromStream_ValidJson_ReturnsObject()
        {
            TestObject original = new() { Name = "FromStream", Value = 33, IsActive = true };
            using MemoryStream ms = new();
            JsonSerializer.Serialize(ms, original);
            ms.Position = 0;

            TestObject deserialized = PooledJsonSerializer.DeserializeFromStream<TestObject>(ms);

            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Value, deserialized.Value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DeserializeFromStream_NullStream_ThrowsArgumentNullException()
        {
            PooledJsonSerializer.DeserializeFromStream<TestObject>(null);
        }

        [TestMethod]
        public async Task DeserializeFromStreamAsync_ValidJson_ReturnsObject()
        {
            TestObject original = new() { Name = "AsyncFromStream", Value = 44, IsActive = false };
            using MemoryStream ms = new();
            await JsonSerializer.SerializeAsync(ms, original);
            ms.Position = 0;

            TestObject deserialized = await PooledJsonSerializer.DeserializeFromStreamAsync<TestObject>(ms);

            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Value, deserialized.Value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeserializeFromStreamAsync_NullStream_ThrowsArgumentNullException()
        {
            await PooledJsonSerializer.DeserializeFromStreamAsync<TestObject>(null);
        }

        [TestMethod]
        public void DeserializeFromSpan_ValidJson_ReturnsObject()
        {
            string json = "{\"Name\":\"SpanTest\",\"Value\":88,\"IsActive\":true}";
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(json);

            TestObject deserialized = PooledJsonSerializer.DeserializeFromSpan<TestObject>(utf8Bytes);

            Assert.AreEqual("SpanTest", deserialized.Name);
            Assert.AreEqual(88, deserialized.Value);
            Assert.IsTrue(deserialized.IsActive);
        }

        [TestMethod]
        public void SerializeToPooledArray_ReturnsValidArray()
        {
            TestObject obj = new()
            {
                Name = "ArrayTest",
                Value = 111,
                IsActive = true
            };

            (byte[] buffer, int length) = PooledJsonSerializer.SerializeToPooledArray(obj);

            try
            {
                Assert.IsNotNull(buffer);
                Assert.IsTrue(length > 0);
                Assert.IsTrue(buffer.Length >= length);

                // Verify the content is valid JSON
                TestObject deserialized = PooledJsonSerializer.DeserializeFromSpan<TestObject>(buffer.AsSpan(0, length));
                Assert.AreEqual(obj.Name, deserialized.Name);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [TestMethod]
        public void SerializeToPooledStream_WithCustomOptions_UsesOptions()
        {
            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            TestObject obj = new()
            {
                Name = "OptionsTest",
                Value = 22,
                IsActive = true
            };

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj, options);
            string json = ReadStreamAsString(stream);

            Assert.IsTrue(json.Contains("\"name\""));
            Assert.IsTrue(json.Contains("\"value\""));
            Assert.IsTrue(json.Contains("\"isActive\""));
        }

        [TestMethod]
        public void DeserializeFromStream_WithCustomOptions_UsesOptions()
        {
            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            string json = "{\"NAME\":\"CaseInsensitive\",\"VALUE\":99,\"ISACTIVE\":true}";
            using MemoryStream ms = new(System.Text.Encoding.UTF8.GetBytes(json));

            TestObject deserialized = PooledJsonSerializer.DeserializeFromStream<TestObject>(ms, options);

            Assert.AreEqual("CaseInsensitive", deserialized.Name);
            Assert.AreEqual(99, deserialized.Value);
            Assert.IsTrue(deserialized.IsActive);
        }

        [TestMethod]
        public async Task DeserializeFromStreamAsync_WithCancellation_PropagatesCancellation()
        {
            TestObject original = new() { Name = "Cancel", Value = 1, IsActive = true };
            using MemoryStream ms = new();
            await JsonSerializer.SerializeAsync(ms, original);
            ms.Position = 0;

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await PooledJsonSerializer.DeserializeFromStreamAsync<TestObject>(ms, cancellationToken: cts.Token);
            });
        }

        [TestMethod]
        public void SerializeToPooledStream_EmptyObject_Succeeds()
        {
            TestObject obj = new();

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj);

            Assert.IsNotNull(stream);
            Assert.IsTrue(stream.Length > 0);
        }

        [TestMethod]
        public void SerializeToPooledStream_LargeObject_Succeeds()
        {
            NestedTestObject obj = new()
            {
                Id = new string('x', 10000),
                Inner = new TestObject
                {
                    Name = new string('y', 5000),
                    Value = int.MaxValue,
                    IsActive = true
                },
                Numbers = new int[1000]
            };

            for (int i = 0; i < obj.Numbers.Length; i++)
            {
                obj.Numbers[i] = i;
            }

            using PooledMemoryStream stream = PooledJsonSerializer.SerializeToPooledStream(obj);
            NestedTestObject deserialized = PooledJsonSerializer.DeserializeFromStream<NestedTestObject>(stream);

            Assert.AreEqual(obj.Id.Length, deserialized.Id.Length);
            Assert.AreEqual(obj.Inner.Name.Length, deserialized.Inner.Name.Length);
            Assert.AreEqual(obj.Numbers.Length, deserialized.Numbers.Length);
        }

        [TestMethod]
        public void DeserializeFromStream_DeeplyNestedJson_ThrowsJsonException()
        {
            // Arrange: Create JSON nested deeper than MaxDepth (64)
            // This protects against DoS attacks via deeply nested structures
            int depth = 100; // Exceeds MaxDepth of 64
            System.Text.StringBuilder sb = new();

            // Build deeply nested object: {"a":{"a":{"a":...}}}
            for (int i = 0; i < depth; i++)
            {
                sb.Append("{\"a\":");
            }
            sb.Append("1");
            for (int i = 0; i < depth; i++)
            {
                sb.Append("}");
            }

            string deeplyNestedJson = sb.ToString();
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(deeplyNestedJson);

            // Act & Assert
            using MemoryStream ms = new(jsonBytes);
            JsonException exception = Assert.ThrowsException<JsonException>(() =>
            {
                PooledJsonSerializer.DeserializeFromStream<object>(ms);
            });

            // Verify the exception message indicates max depth was exceeded
            Assert.IsTrue(
                exception.Message.Contains("max depth", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("depth", StringComparison.OrdinalIgnoreCase),
                $"Expected exception message to mention depth limit, but got: {exception.Message}");
        }

        [TestMethod]
        public async Task DeserializeFromStreamAsync_DeeplyNestedJson_ThrowsJsonException()
        {
            // Arrange: Create JSON nested deeper than MaxDepth (64)
            int depth = 100; // Exceeds MaxDepth of 64
            System.Text.StringBuilder sb = new();

            // Build deeply nested array: [[[[[...]]]]]
            for (int i = 0; i < depth; i++)
            {
                sb.Append('[');
            }
            sb.Append("1");
            for (int i = 0; i < depth; i++)
            {
                sb.Append(']');
            }

            string deeplyNestedJson = sb.ToString();
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(deeplyNestedJson);

            // Act & Assert
            using MemoryStream ms = new(jsonBytes);
            JsonException exception = await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            {
                await PooledJsonSerializer.DeserializeFromStreamAsync<object>(ms);
            });

            // Verify the exception message indicates max depth was exceeded
            Assert.IsTrue(
                exception.Message.Contains("max depth", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("depth", StringComparison.OrdinalIgnoreCase),
                $"Expected exception message to mention depth limit, but got: {exception.Message}");
        }

        [TestMethod]
        public void DeserializeFromSpan_DeeplyNestedJson_ThrowsJsonException()
        {
            // Arrange: Create JSON with mixed nesting (objects and arrays)
            int depth = 80; // Exceeds MaxDepth of 64
            System.Text.StringBuilder sb = new();

            // Build mixed nesting: {"a":[{"a":[...]]}
            for (int i = 0; i < depth; i++)
            {
                if (i % 2 == 0)
                {
                    sb.Append("{\"a\":");
                }
                else
                {
                    sb.Append('[');
                }
            }
            sb.Append("true");
            for (int i = 0; i < depth; i++)
            {
                if ((depth - 1 - i) % 2 == 0)
                {
                    sb.Append('}');
                }
                else
                {
                    sb.Append(']');
                }
            }

            string deeplyNestedJson = sb.ToString();
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(deeplyNestedJson);

            // Act & Assert
            JsonException exception = Assert.ThrowsException<JsonException>(() =>
            {
                PooledJsonSerializer.DeserializeFromSpan<object>(jsonBytes);
            });

            // Verify the exception message indicates max depth was exceeded
            Assert.IsTrue(
                exception.Message.Contains("max depth", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("depth", StringComparison.OrdinalIgnoreCase),
                $"Expected exception message to mention depth limit, but got: {exception.Message}");
        }

        private static string ReadStreamAsString(Stream stream)
        {
            stream.Position = 0;
            using StreamReader reader = new(stream, leaveOpen: true);
            string result = reader.ReadToEnd();
            stream.Position = 0;
            return result;
        }
    }
}
#endif
