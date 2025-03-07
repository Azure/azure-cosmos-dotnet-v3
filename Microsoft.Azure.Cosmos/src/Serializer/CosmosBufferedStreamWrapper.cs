//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using System.IO;
    using System.Linq;
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
        private readonly CloneableStream innerStream;

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
            CloneableStream inputStream,
            bool shouldDisposeInnerStream)
        {
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

            return this.innerStream.Read(buffer, offset, count);
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
                    this.ResetStreamPosition();
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
            ArraySegment<byte> byteSegment = this.innerStream.GetBuffer();

            return byteSegment.Array.Length == byteSegment.Count
                ? byteSegment.Array
                : byteSegment.ToArray();
        }

        /// <summary>
        /// Determines the JSON serialization format of the stream based on the first byte.
        /// </summary>
        /// <returns>
        /// The <see cref="JsonSerializationFormat"/> of the stream, which can be Binary, HybridRow, or Text.
        /// </returns>
        public JsonSerializationFormat GetJsonSerializationFormat()
        {
            this.ReadFirstByteAndResetStream();

            return this.firstByteBuffer[0] switch
            {
                (byte)JsonSerializationFormat.Binary => JsonSerializationFormat.Binary,
                (byte)JsonSerializationFormat.HybridRow => JsonSerializationFormat.HybridRow,
                _ => JsonSerializationFormat.Text,
            };
        }

        /// <summary>
        /// Reads the first byte from the inner stream and stores it in the buffer. It also resets the stream position to zero.
        /// </summary>
        /// <remarks>
        /// This method sets the <see cref="hasReadFirstByte"/> flag to true if the first byte is successfully read.
        /// </remarks>
        private void ReadFirstByteAndResetStream()
        {
            if (!this.hasReadFirstByte
                && this.innerStream.Read(this.firstByteBuffer, 0, 1) > 0)
            {
                this.hasReadFirstByte = true;
                this.ResetStreamPosition();
            }
        }

        /// <summary>
        /// Resets the inner stream position to zero.
        /// </summary>
        private void ResetStreamPosition()
        {
            if (this.innerStream.CanSeek)
            {
                this.innerStream.Position = 0;
            }
        }
    }
}
