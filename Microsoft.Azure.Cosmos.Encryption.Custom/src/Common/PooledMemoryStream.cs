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
    /// <para><strong>Security Considerations:</strong></para>
    /// <para>
    /// GetBuffer() and TryGetBuffer() are internal-only to prevent external code from accessing pooled
    /// buffers that may contain sensitive cryptographic material. External callers must use ToArray()
    /// which returns a safe copy. Buffers are always cleared (zeroed) before returning to the pool when
    /// used for encryption operations, which is enforced by using the default clearOnReturn: true parameter.
    /// This defense-in-depth approach ensures sensitive data never remains in pooled memory.
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
            int oldLength = this.length;

            if (newLength > this.capacity)
            {
                this.EnsureCapacity(newLength);
            }

            // SECURITY FIX: Zero the newly exposed region when expanding length
            // to prevent leaking pool garbage data
            if (newLength > oldLength && this.clearOnReturn)
            {
                Array.Clear(this.buffer, oldLength, newLength - oldLength);
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

            // SECURITY FIX: Zero any gap between current length and write position
            // to prevent leaking pool garbage when writing beyond the current length
            if (this.position > this.length && this.clearOnReturn)
            {
                Array.Clear(this.buffer, this.length, this.position - this.length);
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

            // SECURITY FIX: Zero any gap between current length and write position
            // to prevent leaking pool garbage when writing beyond the current length
            if (this.position > this.length && this.clearOnReturn)
            {
                Array.Clear(this.buffer, this.length, this.position - this.length);
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
        /// <remarks>
        /// SECURITY: This method is internal to prevent external code from accessing pooled buffers
        /// that may contain sensitive cryptographic material. External callers should use ToArray()
        /// which returns a safe copy. This method is only exposed internally for testing buffer
        /// clearing behavior.
        /// </remarks>
        internal byte[] GetBuffer()
        {
            this.EnsureNotDisposed();
            return this.buffer;
        }

        /// <summary>
        /// Tries to get the written portion of the buffer without copying.
        /// </summary>
        /// <remarks>
        /// SECURITY: This method is internal to prevent external code from accessing pooled buffers
        /// that may contain sensitive cryptographic material. External callers should use ToArray()
        /// which returns a safe copy. This method is only exposed internally for testing buffer
        /// clearing behavior.
        /// </remarks>
        internal bool TryGetBuffer(out ArraySegment<byte> buffer)
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
                    // Defense-in-depth: Explicitly clear sensitive data before returning to pool.
                    // ArrayPool.Return's clearArray parameter only clears when the pool decides to retain
                    // the buffer for reuse. If the pool releases the buffer instead, clearArray is ignored.
                    // By clearing explicitly here, we guarantee sensitive encryption data is zeroed regardless
                    // of ArrayPool's internal retention policy.
                    //
                    // SECURITY FIX: Clear up to capacity (not length) to handle SetLength(0) scenario.
                    // When SetLength(0) is called, this.length becomes 0, but sensitive data may still
                    // exist in the buffer beyond position 0. We must clear the entire capacity to ensure
                    // no sensitive data leaks back to the pool.
                    if (this.clearOnReturn && this.capacity > 0)
                    {
                        Array.Clear(this.buffer, 0, this.capacity);
                    }

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

            int newCapacity;
            try
            {
                // Use checked arithmetic to detect integer overflow when doubling capacity
                newCapacity = checked(this.capacity * 2);
                newCapacity = Math.Max(requiredCapacity, newCapacity);

                // Ensure we don't exceed the maximum array length
                if (newCapacity > MaxArrayLength)
                {
                    newCapacity = Math.Max(requiredCapacity, MaxArrayLength);
                }
            }
            catch (OverflowException)
            {
                // If doubling capacity overflows, cap at MaxArrayLength
                newCapacity = Math.Max(requiredCapacity, MaxArrayLength);
            }

            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

            // SECURITY FIX: Clear the new buffer to prevent exposing pool garbage.
            // Rented buffers from ArrayPool may contain data from previous usage.
            if (this.clearOnReturn)
            {
                Array.Clear(newBuffer, 0, newBuffer.Length);
            }

            if (this.length > 0)
            {
                Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.length);
            }

            // SECURITY FIX: Clear the entire old buffer (up to capacity, not just length)
            // to ensure sensitive data beyond the current length is also cleared before
            // returning to the pool. This handles the case where sensitive data exists
            // beyond the current length due to SetLength shrinking.
            if (this.buffer.Length > 0)
            {
                if (this.clearOnReturn)
                {
                    Array.Clear(this.buffer, 0, this.capacity);
                }

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
