//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A MemoryStream that uses ArrayPool for its underlying buffer to reduce GC pressure.
    /// The buffer is returned to the pool when the stream is disposed.
    /// </summary>
    /// <remarks>
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>
    /// This class is NOT thread-safe. All operations must be synchronized externally if the stream
    /// is accessed from multiple threads concurrently. Concurrent reads, writes, or property access
    /// without synchronization will result in data corruption or exceptions.
    /// </para>
    /// <para><strong>Disposal Requirements:</strong></para>
    /// <para>
    /// CRITICAL: This stream MUST be disposed to return rented ArrayPool buffers. Failure to dispose
    /// will leak pooled memory and eventually exhaust the ArrayPool. Always use try-finally or using
    /// statements to ensure disposal, especially when exceptions may occur.
    /// </para>
    /// <para><strong>GetBuffer() Safety:</strong></para>
    /// <para>
    /// WARNING: GetBuffer() returns the internal ArrayPool buffer which may be larger than the stream
    /// length and may be reused after disposal. The returned buffer becomes INVALID after Dispose() is
    /// called. Never cache the buffer reference beyond the stream's lifetime. Always use the Length
    /// property to determine the valid data range (0 to Length-1).
    /// </para>
    /// <para><strong>Performance Considerations:</strong></para>
    /// <para>
    /// The clearOnReturn parameter controls whether the buffer is zeroed when returned to the pool.
    /// Set to false only when the buffer never contains sensitive data. Default is true for security.
    /// </para>
    /// </remarks>
    internal sealed class PooledMemoryStream : Stream
    {
        private const int MaxArrayLength = 0X7FFFFFC7; // From Array.MaxLength

        private readonly bool clearOnReturn;

        private byte[] buffer;
        private int position;
        private int length;
        private int capacity;
        private bool disposed;

#if NET8_0_OR_GREATER
        public PooledMemoryStream(int capacity = -1, bool clearOnReturn = true)
        {
            this.clearOnReturn = clearOnReturn;

            // Use configuration default if not specified
            if (capacity < 0)
            {
                capacity = PooledStreamConfiguration.StreamInitialCapacity;
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
            this.buffer = capacity > 0 ? ArrayPool<byte>.Shared.Rent(capacity) : Array.Empty<byte>();
            this.position = 0;
            this.length = 0;
        }
#else
        public PooledMemoryStream(int capacity = 4096, bool clearOnReturn = true)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.clearOnReturn = clearOnReturn;
            this.capacity = capacity;
            this.buffer = capacity > 0 ? ArrayPool<byte>.Shared.Rent(capacity) : Array.Empty<byte>();
            this.position = 0;
            this.length = 0;
        }
#endif

        public override bool CanRead => !this.disposed;

        public override bool CanSeek => !this.disposed;

        public override bool CanWrite => !this.disposed;

        public override long Length
        {
            get
            {
                this.EnsureNotDisposed();
                return this.length;
            }
        }

        public override long Position
        {
            get
            {
                this.EnsureNotDisposed();
                return this.position;
            }

            set
            {
                this.EnsureNotDisposed();
                if (value < 0 || value > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                this.position = (int)value;
            }
        }

        public override void Flush()
        {
            // No-op for memory stream
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.ValidateArguments(buffer, offset, count);
            this.EnsureNotDisposed();

            int availableBytes = this.length - this.position;
            if (availableBytes <= 0)
            {
                return 0;
            }

            int bytesToRead = Math.Min(availableBytes, count);
            Buffer.BlockCopy(this.buffer, this.position, buffer, offset, bytesToRead);
            this.position += bytesToRead;
            return bytesToRead;
        }

#if NET8_0_OR_GREATER
        public override int Read(Span<byte> buffer)
        {
            this.EnsureNotDisposed();

            int availableBytes = this.length - this.position;
            if (availableBytes <= 0)
            {
                return 0;
            }

            int bytesToRead = Math.Min(availableBytes, buffer.Length);
            this.buffer.AsSpan(this.position, bytesToRead).CopyTo(buffer);
            this.position += bytesToRead;
            return bytesToRead;
        }
#endif

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.ValidateArguments(buffer, offset, count);

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            try
            {
                int bytesRead = this.Read(buffer, offset, count);
                return Task.FromResult(bytesRead);
            }
            catch (Exception ex)
            {
                return Task.FromException<int>(ex);
            }
        }

#if NET8_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            try
            {
                int bytesRead = this.Read(buffer.Span);
                return ValueTask.FromResult(bytesRead);
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<int>(ex);
            }
        }
#endif

        public override long Seek(long offset, SeekOrigin origin)
        {
            this.EnsureNotDisposed();

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => this.position + offset,
                SeekOrigin.End => this.length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0 || newPosition > int.MaxValue)
            {
                throw new IOException("Seek position is out of range");
            }

            this.position = (int)newPosition;
            return this.position;
        }

        public override void SetLength(long value)
        {
            this.EnsureNotDisposed();

            if (value < 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            int newLength = (int)value;
            if (newLength > this.capacity)
            {
                this.EnsureCapacity(newLength);
            }

            this.length = newLength;
            if (this.position > this.length)
            {
                this.position = this.length;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.ValidateArguments(buffer, offset, count);
            this.EnsureNotDisposed();

            long newPositionLong = (long)this.position + count;
            if (newPositionLong > int.MaxValue)
            {
                throw new IOException("Stream too long");
            }

            int newPosition = (int)newPositionLong;
            if (newPosition > this.capacity)
            {
                this.EnsureCapacity(newPosition);
            }

            Buffer.BlockCopy(buffer, offset, this.buffer, this.position, count);
            this.position = newPosition;
            if (this.position > this.length)
            {
                this.length = this.position;
            }
        }

#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.EnsureNotDisposed();

            long newPositionLong = (long)this.position + buffer.Length;
            if (newPositionLong > int.MaxValue)
            {
                throw new IOException("Stream too long");
            }

            int newPosition = (int)newPositionLong;
            if (newPosition > this.capacity)
            {
                this.EnsureCapacity(newPosition);
            }

            buffer.CopyTo(this.buffer.AsSpan(this.position));
            this.position = newPosition;
            if (this.position > this.length)
            {
                this.length = this.position;
            }
        }
#endif

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.ValidateArguments(buffer, offset, count);

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            try
            {
                this.Write(buffer, offset, count);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

#if NET8_0_OR_GREATER
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            try
            {
                this.Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }
#endif

        /// <summary>
        /// Gets the underlying buffer. Only valid before disposal.
        /// </summary>
        public byte[] GetBuffer()
        {
            this.EnsureNotDisposed();
            return this.buffer;
        }

        /// <summary>
        /// Tries to get the written portion of the buffer without copying.
        /// </summary>
        public bool TryGetBuffer(out ArraySegment<byte> buffer)
        {
            this.EnsureNotDisposed();
            buffer = new ArraySegment<byte>(this.buffer, 0, this.length);
            return true;
        }

        /// <summary>
        /// Returns a copy of the stream contents as a byte array.
        /// </summary>
        public byte[] ToArray()
        {
            this.EnsureNotDisposed();

            if (this.length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] result = new byte[this.length];
            Buffer.BlockCopy(this.buffer, 0, result, 0, this.length);
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                if (this.buffer != null && this.buffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(this.buffer, this.clearOnReturn);
                    this.buffer = null;
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= this.capacity)
            {
                return;
            }

            int newCapacity = Math.Max(requiredCapacity, this.capacity * 2);

            // Prevent overflow
            if ((uint)newCapacity > MaxArrayLength)
            {
                newCapacity = Math.Max(requiredCapacity, MaxArrayLength);
            }

            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

            if (this.length > 0)
            {
                Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.length);
            }

            if (this.buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(this.buffer, this.clearOnReturn);
            }

            this.buffer = newBuffer;
            this.capacity = newBuffer.Length;
        }

        private void EnsureNotDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(PooledMemoryStream));
            }
        }

        private void ValidateArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Offset and length are out of bounds");
            }
        }
    }
}
