// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Rntbd
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Encapsulates the reading from the network on a TCP connection.
    /// </summary>
    /// <remarks>
    /// RntbdStreamReader does not own the stream that it reads from.
    /// It is the callers responsibility to close the stream.
    /// </remarks>
    internal sealed class RntbdStreamReader : IDisposable
    {
        /// <summary>
        /// The buffer size is picked to be a large enough value that common documents and responses
        /// fit in memory, but small enough to avoid LOH. 16k was picked for now; wthis can be updated with more data
        /// as needed.
        /// </summary>
        private const int BufferSize = 16384;

        private readonly Stream stream;
        private byte[] buffer;
        private Memory<byte> availableBytes;

        public RntbdStreamReader(Stream stream)
        {
            this.stream = stream;
            this.buffer = ArrayPool<byte>.Shared.Rent(RntbdStreamReader.BufferSize);
            this.availableBytes = default;
        }

        internal int AvailableByteCount => this.availableBytes.Length;

        public void Dispose()
        {
            byte[] bufferToReturn = this.buffer;
            this.buffer = null;
            this.availableBytes = default;
            ArrayPool<byte>.Shared.Return(bufferToReturn);
        }

        public ValueTask<int> ReadAsync(byte[] payload, int offset, int count)
        {
            if (payload.Length < (offset + count))
            {
                throw new ArgumentException(nameof(payload));
            }

            if (!this.availableBytes.IsEmpty)
            {
                return new ValueTask<int>(this.CopyFromAvailableBytes(payload, offset, count));
            }

            return this.PopulateBytesAndReadAsync(payload, offset, count);
        }

        private async ValueTask<int> PopulateBytesAndReadAsync(byte[] payload, int offset, int count)
        {
            Debug.Assert(this.availableBytes.IsEmpty);

            // if the count requested is bigger than the buffer just read directly into the target payload.
            if (count >= this.buffer.Length)
            {
                return await this.stream.ReadAsync(payload, offset, count);
            }
            else
            {
                int bytesRead = await this.stream.ReadAsync(this.buffer, 0, this.buffer.Length);
                if (bytesRead == 0)
                {
                    // graceful closure.
                    return bytesRead;
                }

                this.availableBytes = new Memory<byte>(this.buffer, 0, bytesRead);
                return this.CopyFromAvailableBytes(payload, offset, count);
            }
        }

        private int CopyFromAvailableBytes(byte[] payload, int offset, int count)
        {
            // copy any in memory buffer to the target payload.
            try
            {
                Span<byte> sourceSpan;
                if (count >= this.availableBytes.Length)
                {
                    // if more bytes than wwhat we've buffered is requested, copy what we have 
                    // and return. The caller can request the remaining separately.
                    sourceSpan = this.availableBytes.Span;
                    this.availableBytes = default;
                }
                else
                {
                    sourceSpan = this.availableBytes.Span.Slice(0, count);
                    this.availableBytes = this.availableBytes.Slice(count);
                }

                Span<byte> targetSpan = new Span<byte>(payload, offset, count);
                sourceSpan.CopyTo(targetSpan);

                return sourceSpan.Length;
            }
            catch (Exception e)
            {
                throw new IOException("Error copying buffered bytes", e);
            }
        }

    }
}
