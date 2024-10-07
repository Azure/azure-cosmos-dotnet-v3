//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// A wrapper for a stream that buffers the first byte.
    /// </summary>
    internal class CosmosBufferedStreamWrapper : Stream
    {
        /// <summary>
        /// The inner stream being wrapped.
        /// </summary>
        private readonly Stream innerStream;

        /// <summary>
        /// Indicates whether the inner stream should be disposed.
        /// </summary>
        private readonly bool shouldDisposeInnerStream;

        /// <summary>
        /// Buffer to hold the first byte read from the stream.
        /// </summary>
        private readonly byte[] firstByteBuffer = new byte[1];

        /// <summary>
        /// Indicates whether the first byte has been read.
        /// </summary>
        private bool hasReadFirstByte;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosBufferedStreamWrapper"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to wrap.</param>
        /// <param name="shouldDisposeInnerStream">Indicates whether the inner stream should be disposed.</param>
        public CosmosBufferedStreamWrapper(
            Stream inputStream,
            bool shouldDisposeInnerStream)
        {
            Debug.Assert(
                inputStream is CloneableStream || inputStream is MemoryStream,
                "The inner stream is neither a memory stream nor a cloneable stream.");

            this.innerStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
            this.shouldDisposeInnerStream = shouldDisposeInnerStream;
        }

        /// <inheritdoc />
        public override bool CanRead => this.innerStream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => this.innerStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => this.innerStream.CanWrite;

        /// <inheritdoc />
        public override long Length => this.innerStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => this.innerStream.Position;
            set => this.innerStream.Position = value;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            this.innerStream.Flush();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.innerStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            this.innerStream.SetLength(value);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || (buffer.Length - offset) < count)
            {
                throw new ArgumentOutOfRangeException();
            }

            int bytesRead = 0;
            if (this.hasReadFirstByte
                && buffer[0] == 0
                && offset == 0 && count > 0)
            {
                buffer[0] = this.firstByteBuffer[0];
                bytesRead = 1;
                offset++;
                count--;
            }

            if (count > 0)
            {
                int innerBytesRead = this.innerStream.Read(buffer, offset, count);
                bytesRead += innerBytesRead;
            }

            return bytesRead;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.innerStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Flush();
                if (this.shouldDisposeInnerStream)
                {
                    this.innerStream.Dispose();
                }
                else
                {
                    if (this.innerStream.CanSeek)
                    {
                        this.innerStream.Position = 0;
                    }
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Reads all bytes from the current position to the end of the stream.
        /// </summary>
        /// <returns>
        /// A byte array containing all the bytes read from the stream, or <c>null</c> if no bytes were read.
        /// </returns>
        public byte[] ReadAll()
        {
            int count, totalBytes = 0, offset = (int)this.Position, length = (int)this.Length;
            byte[] bytes = new byte[length];

            while ((count = this.innerStream.Read(bytes, offset, length - offset)) > 0)
            {
                offset += count;
                totalBytes += count;
            }

            if (this.hasReadFirstByte)
            {
                bytes[0] = this.firstByteBuffer[0];
                totalBytes += 1;
            }

            return totalBytes > 0 ? bytes : default;
        }

        /// <summary>
        /// Asynchronously reads all bytes from the current position to the end of the stream.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous read operation. The value of the TResult parameter contains a byte array with all the bytes read from the stream, or <c>null</c> if no bytes were read.
        /// </returns>
        public async Task<byte[]> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            int count, totalBytes = 0, offset = (int)this.Position, length = (int)this.Length;
            byte[] bytes = new byte[length];

            while ((count = await this.innerStream.ReadAsync(bytes, offset, length - offset, cancellationToken)) > 0)
            {
                offset += count;
                totalBytes += count;
            }

            if (this.hasReadFirstByte)
            {
                bytes[0] = this.firstByteBuffer[0];
                totalBytes += 1;
            }

            return totalBytes > 0 ? bytes : default;
        }

        /// <summary>
        /// Determines the JSON serialization format of the stream based on the first byte.
        /// </summary>
        /// <returns>
        /// The <see cref="JsonSerializationFormat"/> of the stream, which can be Binary, HybridRow, or Text.
        /// </returns>
        public JsonSerializationFormat GetJsonSerializationFormat()
        {
            this.ReadFirstByte();
            if (this.firstByteBuffer[0] == (byte)JsonSerializationFormat.Binary)
            {
                return JsonSerializationFormat.Binary;
            }
            else
            {
                return this.firstByteBuffer[0] == (byte)JsonSerializationFormat.HybridRow
                    ? JsonSerializationFormat.HybridRow
                    : JsonSerializationFormat.Text;
            }
        }

        /// <summary>
        /// Reads the first byte from the inner stream and stores it in the buffer.
        /// </summary>
        /// <remarks>
        /// This method sets the <see cref="hasReadFirstByte"/> flag to true if the first byte is successfully read.
        /// </remarks>
        private void ReadFirstByte()
        {
            if (!this.hasReadFirstByte
                && this.innerStream.Read(this.firstByteBuffer, 0, 1) > 0)
            {
                this.hasReadFirstByte = true;
            }
        }
    }
}
