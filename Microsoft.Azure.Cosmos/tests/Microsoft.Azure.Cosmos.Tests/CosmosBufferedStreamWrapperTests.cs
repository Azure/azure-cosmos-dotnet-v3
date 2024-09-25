//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosBufferedStreamWrapperTests
    {
        [TestMethod]
        public void TestReadFirstByte()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                byte[] buffer = new byte[1];
                int bytesRead = bufferedStream.Read(buffer, 0, 1);

                Assert.AreEqual(1, bytesRead);
                Assert.AreEqual((byte)'H', buffer[0]);
            }
        }

        [TestMethod]
        public void TestReadAll()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                byte[] result = bufferedStream.ReadAll();

                Assert.IsNotNull(result);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public async Task TestReadAllAsync()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                byte[] result = await bufferedStream.ReadAllAsync();

                Assert.IsNotNull(result);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public void TestGetJsonSerializationFormat()
        {
            byte[] data = new byte[] { (byte)JsonSerializationFormat.Binary };
            using (MemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

                Assert.AreEqual(JsonSerializationFormat.Binary, format);
            }
        }

        [TestMethod]
        public void TestReadWithNonSeekableStream()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (NonSeekableMemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                Assert.IsFalse(bufferedStream.CanSeek);
                JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

                Assert.AreEqual(JsonSerializationFormat.Text, format);

                byte[] result = new byte[bufferedStream.Length];
                int bytes = bufferedStream.Read(result, 0, (int)bufferedStream.Length);

                Assert.IsNotNull(result);
                Assert.AreEqual(bytes, result.Length);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public async Task TestReadAllAsyncWithNonSeekableStream()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (NonSeekableMemoryStream memoryStream = new (data))
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                Assert.IsFalse(bufferedStream.CanSeek);
                JsonSerializationFormat format = bufferedStream.GetJsonSerializationFormat();

                Assert.AreEqual(JsonSerializationFormat.Text, format);

                byte[] result = await bufferedStream.ReadAllAsync();

                Assert.IsNotNull(result);
                Assert.AreEqual(data.Length, result.Length);
                CollectionAssert.AreEqual(data, result);
            }
        }

        [TestMethod]
        public void TestWriteAndRead()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            using (MemoryStream memoryStream = new ())
            using (CosmosBufferedStreamWrapper bufferedStream = new (memoryStream, true))
            {
                bufferedStream.Write(data, 0, data.Length);
                bufferedStream.Position = 0;

                byte[] buffer = new byte[data.Length];
                int bytesRead = bufferedStream.Read(buffer, 0, buffer.Length);

                Assert.AreEqual(data.Length, bytesRead);
                CollectionAssert.AreEqual(data, buffer);
            }
        }

        class NonSeekableMemoryStream : Stream
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
