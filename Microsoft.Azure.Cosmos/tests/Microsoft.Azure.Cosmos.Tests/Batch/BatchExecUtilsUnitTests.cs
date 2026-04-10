//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchExecUtilsUnitTests
    {
        private readonly Random random = new Random();

        [TestMethod]
        [Owner("abpai")]
        public async Task StreamToBytesAsyncSeekableAsync()
        {
            const int bytesLength = 10;
            byte[] bytes = new byte[bytesLength];
            this.random.NextBytes(bytes);
            {
                Stream stream = new MemoryStream(bytes);
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes));
            }

            {
                Stream stream = new MemoryStream(bytes, 2, 5, writable: false, publiclyVisible: true);
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes.Skip(2).Take(5).ToArray()));
            }

            {
                Stream stream = new MemoryStream(bytes, 2, 5, writable: false, publiclyVisible: false);
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes.Skip(2).Take(5).ToArray()));
            }

            {
                Stream stream = new MemoryStream(bytes.Length * 2);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                stream.Position = 0;
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes));
            }

            {
                Stream stream = new TestSeekableStream(bytes, maxLengthToReturnPerRead: 3);
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes));
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task StreamToBytesAsyncNonSeekableAsync()
        {
            byte[] bytes = new byte[10];
            this.random.NextBytes(bytes);
            TestNonSeekableStream stream = new TestNonSeekableStream(bytes, maxLengthToReturnPerRead: 3);
            {
                Memory<byte> actual = await BatchExecUtils.StreamToMemoryAsync(stream, cancellationToken: CancellationToken.None);
                Assert.IsTrue(actual.Span.SequenceEqual(bytes));
            }
        }

        /// <summary>
        /// Seekable stream that is not a derived class of MemoryStream for testing.
        /// Caller controls max count actually set into the buffer during Read() 
        /// to simulate Socket like read.
        /// </summary>
        private class TestSeekableStream : Stream
        {
            private readonly int maxLengthToReturnPerRead;

            private readonly MemoryStream memoryStream;

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => true;

            public override long Length => this.memoryStream.Length;

            public override long Position
            {
                get => this.memoryStream.Position;
                set => this.memoryStream.Position = value;
            }

            public TestSeekableStream(byte[] bytes, int maxLengthToReturnPerRead)
            {
                this.memoryStream = new MemoryStream(bytes);
                this.maxLengthToReturnPerRead = maxLengthToReturnPerRead;
            }

            public override void Flush()
            {
                this.memoryStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = Math.Min(count, this.maxLengthToReturnPerRead);
                return this.memoryStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.memoryStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                this.memoryStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.memoryStream.Write(buffer, offset, count);
            }
        }

        /// <summary>
        /// Non-seekable stream to test Read() where count actually set into the buffer can be controlled to simulate Socket like read.
        /// </summary>
        private class TestNonSeekableStream : Stream
        {
            private readonly byte[] data;

            private int currentIndex;

            private readonly int maxLengthToReturnPerRead;

            public TestNonSeekableStream(byte[] data, int maxLengthToReturnPerRead)
            {
                this.data = data;
                this.maxLengthToReturnPerRead = maxLengthToReturnPerRead;
            }

            public void Reset()
            {
                this.currentIndex = 0;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int copyCount = Math.Min(count, Math.Min(this.data.Length - this.currentIndex, this.maxLengthToReturnPerRead));
                for (int i = 0; i < copyCount; i++)
                {
                    buffer[offset + i] = this.data[this.currentIndex + i];
                }

                this.currentIndex += copyCount;
                return copyCount;
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}