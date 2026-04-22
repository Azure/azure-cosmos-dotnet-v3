//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only, seekable Stream view over a <see cref="RentArrayBufferWriter"/>. The wrapper
    /// takes ownership of the supplied <see cref="RentArrayBufferWriter"/> and returns its
    /// rented buffer to the shared ArrayPool on <see cref="Dispose(bool)"/>.
    /// </summary>
    /// <remarks>
    /// <para><strong>Why this exists:</strong></para>
    /// <para>
    /// The stream-processor decrypt path previously wrote through a
    /// <c>System.Text.Json.Utf8JsonWriter</c> constructed over a <see cref="PooledMemoryStream"/>.
    /// On .NET 8 the <c>Utf8JsonWriter(Stream)</c> constructor eagerly creates an internal
    /// <c>ArrayBufferWriter&lt;byte&gt;</c> that is GC-heap backed (<c>Array.Resize</c>
    /// doubling from 256 bytes), producing hundreds of kilobytes of per-operation garbage
    /// for medium documents. Routing the writer through an <c>IBufferWriter&lt;byte&gt;</c>
    /// (our pooled <see cref="RentArrayBufferWriter"/>) eliminates that internal buffer
    /// entirely. This wrapper exposes the written bytes as a read-only <see cref="Stream"/>
    /// so the decrypt adapter can return a <see cref="Stream"/> to the caller without
    /// materializing a separate copy.
    /// </para>
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>
    /// This class is NOT thread-safe. All operations must be synchronized externally.
    /// </para>
    /// <para><strong>Disposal Requirements:</strong></para>
    /// <para>
    /// CRITICAL: This stream MUST be disposed to return the underlying rented buffer to
    /// the ArrayPool. Failure to dispose will leak pooled memory.
    /// </para>
    /// <para><strong>Security Considerations:</strong></para>
    /// <para>
    /// The underlying <see cref="RentArrayBufferWriter"/> clears its rented buffer before
    /// returning it to the pool, so decrypted plaintext is never left in pool memory after
    /// disposal.
    /// </para>
    /// </remarks>
    internal sealed class ReadOnlyBufferWriterStream : Stream
    {
        private RentArrayBufferWriter bufferWriter;
        private int position;
        private bool disposed;

        /// <summary>
        /// Takes ownership of <paramref name="bufferWriter"/> and exposes its written portion
        /// as a read-only, seekable <see cref="Stream"/>. The writer must have been flushed by
        /// the producer before any read.
        /// </summary>
        public ReadOnlyBufferWriterStream(RentArrayBufferWriter bufferWriter)
        {
            this.bufferWriter = bufferWriter ?? throw new ArgumentNullException(nameof(bufferWriter));
        }

        public override bool CanRead => !this.disposed;

        public override bool CanSeek => !this.disposed;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                this.EnsureNotDisposed();
                return this.bufferWriter.BytesWritten;
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
            this.EnsureNotDisposed();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            this.EnsureNotDisposed();
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.EnsureNotDisposed();
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

            int written = this.bufferWriter.BytesWritten;
            int remaining = written - this.position;
            if (remaining <= 0)
            {
                return 0;
            }

            int toCopy = Math.Min(remaining, count);
            this.bufferWriter.WrittenSpan.Slice(this.position, toCopy).CopyTo(buffer.AsSpan(offset, toCopy));
            this.position += toCopy;
            return toCopy;
        }

        public override int Read(Span<byte> buffer)
        {
            this.EnsureNotDisposed();

            int written = this.bufferWriter.BytesWritten;
            int remaining = written - this.position;
            if (remaining <= 0)
            {
                return 0;
            }

            int toCopy = Math.Min(remaining, buffer.Length);
            this.bufferWriter.WrittenSpan.Slice(this.position, toCopy).CopyTo(buffer);
            this.position += toCopy;
            return toCopy;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            try
            {
                int read = this.Read(buffer, offset, count);
                return Task.FromResult(read);
            }
            catch (Exception ex)
            {
                return Task.FromException<int>(ex);
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            try
            {
                int read = this.Read(buffer.Span);
                return ValueTask.FromResult(read);
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<int>(ex);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            this.EnsureNotDisposed();

            long length = this.bufferWriter.BytesWritten;
            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => this.position + offset,
                SeekOrigin.End => length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin)),
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
            throw new NotSupportedException("Stream is read-only.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Stream is read-only.");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException("Stream is read-only.");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromException(new NotSupportedException("Stream is read-only."));
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(new NotSupportedException("Stream is read-only."));
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            this.EnsureNotDisposed();
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            int written = this.bufferWriter.BytesWritten;
            int remaining = written - this.position;
            if (remaining > 0)
            {
                destination.Write(this.bufferWriter.WrittenSpan.Slice(this.position, remaining));
                this.position = written;
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            this.EnsureNotDisposed();
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            cancellationToken.ThrowIfCancellationRequested();

            int written = this.bufferWriter.BytesWritten;
            int remaining = written - this.position;
            if (remaining > 0)
            {
                await destination.WriteAsync(this.bufferWriter.WrittenMemory.Slice(this.position, remaining), cancellationToken).ConfigureAwait(false);
                this.position = written;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                this.bufferWriter?.Dispose();
                this.bufferWriter = null;
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void EnsureNotDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ReadOnlyBufferWriterStream));
            }
        }
    }
}
#endif
