//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tests.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosBufferedStreamWrapperTests
    {
        private const string TempFilePath = "test_complex.json";

        [TestInitialize]
        public async Task Setup()
        {
            await JsonGenerator.CreateComplexJsonFileAsync(TempFilePath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Delete the temporary file after each test
            JsonGenerator.DeleteJsonFile(TempFilePath);
        }

        [TestMethod]
        public async Task TestStreamHandlingOfComplexJsonWithNestedObjects()
        {
            using FileStream fileStream = new FileStream(TempFilePath, FileMode.Open, FileAccess.Read);
            using CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(fileStream), true);

            // Deserialize JSON from the stream
            string jsonReadFromStream;
            using (StreamReader reader = new(bufferedStream))
            {
                jsonReadFromStream = await reader.ReadToEndAsync();
            }

            Assert.IsNotNull(jsonReadFromStream, "JSON read from the stream should not be null.");

            // Deserialize original and streamed JSON into objects
            Dictionary<string, object> originalObject = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(await File.ReadAllTextAsync(TempFilePath));
            Dictionary<string, object> streamedObject = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonReadFromStream);

            Assert.AreEqual(
                System.Text.Json.JsonSerializer.Serialize(originalObject),
                System.Text.Json.JsonSerializer.Serialize(streamedObject),
                "Serialized JSON does not match after deserialization."
            );
        }

        [TestMethod]
        public async Task TestStreamHandlingOfComplexJsonWithUniformArraysAndHierarchy()
        {
            string originalJson = await File.ReadAllTextAsync(TempFilePath);

            using FileStream fileStream = new FileStream(TempFilePath, FileMode.Open, FileAccess.Read);
            using CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(fileStream), true);

            // Deserialize JSON from the stream
            string jsonReadFromStream;
            using (StreamReader reader = new(bufferedStream))
            {
                jsonReadFromStream = await reader.ReadToEndAsync();
            }

            Assert.IsNotNull(jsonReadFromStream, "JSON read from the stream should not be null.");

            // Compare the normalized JSON for structural and content equality
            Assert.AreEqual(
                this.NormalizeJson(originalJson),
                this.NormalizeJson(jsonReadFromStream),
                "JSON does not match after processing through the wrapper."
            );
        }

        [TestMethod]
        public async Task TestStreamHandlingOfComplexJsonWithMixedDataAndRepeatingPatterns()
        {
            // Modify the utility-generated JSON for specific patterns
            string originalJson = await File.ReadAllTextAsync(TempFilePath);

            // Safely insert additional properties without breaking JSON syntax
            Dictionary<string, object> jsonObject = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson);
            jsonObject["Reference"] = "REF-1234";
            jsonObject["HexCode"] = "#FF5733";

            string updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });

            // Write the modified JSON to a new stream
            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(updatedJson));
            using CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true);

            // Deserialize JSON from the stream
            string jsonReadFromStream;
            using (StreamReader reader = new(bufferedStream))
            {
                jsonReadFromStream = await reader.ReadToEndAsync();
            }

            Assert.AreEqual(
                this.NormalizeJson(updatedJson),
                this.NormalizeJson(jsonReadFromStream),
                "JSON with mixed data and repeating patterns does not match after processing."
            );
        }


        /// <summary>
        /// Normalizes JSON strings for consistent comparison.
        /// </summary>
        private string NormalizeJson(string json)
        {
            object jsonObject = System.Text.Json.JsonSerializer.Deserialize<object>(json);
            return System.Text.Json.JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        [TestMethod]
        public async Task TestReadFirstByte()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new(data))
            using (CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true))
            {
                byte[] buffer = new byte[1];
                int bytesRead = bufferedStream.Read(buffer, 0, 1);

                Assert.AreEqual(1, bytesRead);
                Assert.AreEqual((byte)'H', buffer[0]);
            }
        }

        [TestMethod]
        public async Task TestReadAll()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new(data))
            using (CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true))
            {
                byte[] result = bufferedStream.ReadAll();

                Assert.IsNotNull(result);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public async Task TestReadAllAfterFirstByteRead()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new(data))
            using (CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true))
            {
                bufferedStream.GetJsonSerializationFormat(); // This will trigger the first byte read.
                byte[] result = bufferedStream.ReadAll();

                Assert.IsNotNull(result);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public async Task TestGetJsonSerializationFormat()
        {
            byte[] data = new byte[] { (byte)JsonSerializationFormat.Binary };
            using (MemoryStream memoryStream = new(data))
            using (CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true))
            {
                JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

                Assert.AreEqual(JsonSerializationFormat.Binary, format);
            }
        }

        [TestMethod]
        public async Task TestReadWithNonSeekableStream()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");

            using NonSeekableMemoryStream memoryStream = new(data);
            using CloneableStream clonableStream = await StreamExtension.AsClonableStreamAsync(memoryStream);
            using CosmosBufferedStreamWrapper bufferedStream = new(clonableStream, true);

            Assert.IsTrue(bufferedStream.CanSeek);
            JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

            Assert.AreEqual(JsonSerializationFormat.Text, format);

            byte[] result = new byte[bufferedStream.Length];
            int bytes = bufferedStream.Read(result, 0, (int)bufferedStream.Length);

            Assert.IsNotNull(result);
            Assert.AreEqual(bytes, result.Length);
            Assert.AreEqual(data.Length, result.Length);
            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public async Task TestReadWithNonSeekableStreamAndSmallerOffset()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World! This is a sample test.");

            using NonSeekableMemoryStream memoryStream = new(data);
            using CloneableStream clonableStream = await StreamExtension.AsClonableStreamAsync(memoryStream);
            using CosmosBufferedStreamWrapper bufferedStream = new(clonableStream, true);

            Assert.IsTrue(bufferedStream.CanSeek);
            JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

            Assert.AreEqual(JsonSerializationFormat.Text, format);

            byte[] result = new byte[bufferedStream.Length];

            int count = 0, chunk = 4, offset = 0, length = (int)bufferedStream.Length, totalBytes = 0;
            while ((count = bufferedStream.Read(result, offset, Math.Min(chunk, (int)(length - bufferedStream.Position)))) > 0)
            {
                offset += count;
                totalBytes += count;
            }

            int count2 = 0, chunk2 = 3, offset2 = 0, length2 = (int)bufferedStream.Length - 1, totalBytes2 = 0;
            byte[] result2 = new byte[bufferedStream.Length];
            while ((count2 = bufferedStream.Read(result2, offset2, chunk2)) > 0)
            {
                offset2 += Math.Min(count2, length);
                totalBytes2 += count2;
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(totalBytes, result.Length);
            Assert.AreEqual(data.Length, result.Length);
            Assert.AreEqual(0, totalBytes2);
            Assert.AreEqual(0, count2);
            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public async Task TestReadAllWithNonSeekableStream()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");

            using NonSeekableMemoryStream memoryStream = new(data);
            using CloneableStream clonableStream = await StreamExtension.AsClonableStreamAsync(memoryStream);
            using CosmosBufferedStreamWrapper bufferedStream = new(clonableStream, true);

            Assert.IsTrue(bufferedStream.CanSeek);
            JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

            Assert.AreEqual(JsonSerializationFormat.Text, format);

            byte[] result = bufferedStream.ReadAll();

            Assert.IsNotNull(result);
            Assert.AreEqual(data.Length, result.Length);
            CollectionAssert.AreEqual(data, result);
        }

        [TestMethod]
        public async Task TestWriteAndRead()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new())
            using (CosmosBufferedStreamWrapper bufferedStream = new(await StreamExtension.AsClonableStreamAsync(memoryStream), true))
            {
                bufferedStream.Write(data, 0, data.Length);
                bufferedStream.Position = 0;

                byte[] buffer = new byte[data.Length];
                int bytesRead = bufferedStream.Read(buffer, 0, buffer.Length);

                Assert.AreEqual(data.Length, bytesRead);
                CollectionAssert.AreEqual(data, buffer);
            }
        }

        internal class NonSeekableMemoryStream : Stream
        {
            private readonly byte[] buffer;
            private int position;

            public NonSeekableMemoryStream(byte[] data)
            {
                this.buffer = data;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;

            public override long Length => this.buffer.Length;

            public override long Position
            {
                get => this.position;
                set => throw new NotSupportedException("Seeking is not supported on this stream.");
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesToRead = Math.Min(count, this.buffer.Length - this.position);
                Array.Copy(this.buffer, this.position, buffer, offset, bytesToRead);
                this.position += bytesToRead;
                return bytesToRead;
            }

            public override void Flush()
            {
                // No operation needed as this stream is read-only
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("Seeking is not supported on this stream.");
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException("Setting the length is not supported on this stream.");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }
    }
}