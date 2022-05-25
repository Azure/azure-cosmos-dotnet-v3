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
        private int offset;
        private int length;

        public RntbdStreamReader(Stream stream)
        {
            this.stream = stream;
            this.buffer = ArrayPool<byte>.Shared.Rent(RntbdStreamReader.BufferSize);
            this.offset = 0;
            this.length = 0;
        }

        internal int AvailableByteCount => this.length;

        public void Dispose()
        {
            byte[] bufferToReturn = this.buffer;
            this.buffer = null;
            ArrayPool<byte>.Shared.Return(bufferToReturn);
        }

        public ValueTask<int> ReadAsync(byte[] payload, int offset, int count)
        {
            if (payload.Length < (offset + count))
            {
                throw new ArgumentException(nameof(payload));
            }

            if (this.length > 0)
            {
                return new ValueTask<int>(this.CopyFromAvailableBytes(payload, offset, count));
            }

            return this.PopulateBytesAndReadAsync(payload, offset, count);
        }

        public ValueTask<int> ReadAsync(MemoryStream payload, int count)
        {
            if (this.length > 0)
            {
                return new ValueTask<int>(this.CopyFromAvailableBytes(payload, count));
            }

            return this.PopulateBytesAndReadAsync(payload, count);
        }

        private async ValueTask<int> PopulateBytesAndReadAsync(byte[] payload, int offset, int count)
        {
            Debug.Assert(this.length == 0);

            // if the count requested is bigger than the buffer just read directly into the target payload.
            if (count >= this.buffer.Length)
            {
                return await this.stream.ReadAsync(payload, offset, count);
            }
            else
            {
                this.offset = 0;
                this.length = await this.stream.ReadAsync(this.buffer, offset: 0, this.buffer.Length);
                if (this.length == 0)
                {
                    // graceful closure.
                    return this.length;
                }

                return this.CopyFromAvailableBytes(payload, offset, count);
            }
        }

        private async ValueTask<int> PopulateBytesAndReadAsync(MemoryStream payload, int count)
        {
            Debug.Assert(this.length == 0);
            this.offset = 0;
            this.length = await this.stream.ReadAsync(this.buffer, offset: 0, this.buffer.Length);
            if (this.length == 0)
            {
                // graceful closure.
                return this.length;
            }

            return this.CopyFromAvailableBytes(payload, count);
        }

        private int CopyFromAvailableBytes(byte[] payload, int offset, int count)
        {
            // copy any in memory buffer to the target payload.
            try
            {
                if (count >= this.length)
                {
                    // if more bytes than what we've buffered is requested, copy what we have 
                    // and return. The caller can request the remaining separately.
                    Array.Copy(sourceArray: this.buffer, sourceIndex: this.offset, destinationArray: payload, destinationIndex: offset, length: this.length);
                    int bytesRead = this.length;
                    this.length = 0;
                    this.offset = 0;
                    return bytesRead;
                }
                else
                {
                    Array.Copy(sourceArray: this.buffer, sourceIndex: this.offset, destinationArray: payload, destinationIndex: offset, length: count);
                    this.length -= count;
                    this.offset += count;
                    return count;
                }
            }
            catch (Exception e)
            {
                throw new IOException("Error copying buffered bytes", e);
            }
        }

        private int CopyFromAvailableBytes(MemoryStream payload, int count)
        {
            // copy any in memory buffer to the target payload.
            try
            {
                if (count >= this.length)
                {
                    // if more bytes than what we've buffered is requested, copy what we have 
                    // and return. The caller can request the remaining separately.
                    int bytesRead = this.length;
                    payload.Write(this.buffer, this.offset, this.length);
                    this.length = 0;
                    this.offset = 0;
                    return bytesRead;
                }
                else
                {
                    payload.Write(this.buffer, this.offset, count);
                    this.length -= count;
                    this.offset += count;
                    return count;
                }
            }
            catch (Exception e)
            {
                throw new IOException("Error copying buffered bytes", e);
            }
        }
    }
}
