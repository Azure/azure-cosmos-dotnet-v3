// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom;

#if NET8_0_OR_GREATER

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// https://gist.github.com/ahsonkhan/c76a1cc4dc7107537c3fdc0079a68b35
/// Standard ArrayBufferWriter is not using pooled memory
/// </summary>
internal class RentArrayBufferWriter : IBufferWriter<byte>, IDisposable
{
    private const int MinimumBufferSize = 256;

    private byte[] rentedBuffer;
    private int written;
    private long committed;

    public RentArrayBufferWriter(int initialCapacity = MinimumBufferSize)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentException(null, nameof(initialCapacity));
        }

        this.rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        this.written = 0;
        this.committed = 0;
    }

    public (byte[], int) WrittenBuffer
    {
        get
        {
            this.CheckIfDisposed();

            return (this.rentedBuffer, this.written);
        }
    }

    public Memory<byte> WrittenMemory
    {
        get
        {
            this.CheckIfDisposed();

            return this.rentedBuffer.AsMemory(0, this.written);
        }
    }

    public Span<byte> WrittenSpan
    {
        get
        {
            this.CheckIfDisposed();

            return this.rentedBuffer.AsSpan(0, this.written);
        }
    }

    public int BytesWritten
    {
        get
        {
            this.CheckIfDisposed();

            return this.written;
        }
    }

    public long BytesCommitted
    {
        get
        {
            this.CheckIfDisposed();

            return this.committed;
        }
    }

    public void Clear()
    {
        this.CheckIfDisposed();

        this.ClearHelper();
    }

    private void ClearHelper()
    {
        this.rentedBuffer.AsSpan(0, this.written).Clear();
        this.written = 0;
    }

    public async Task CopyToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        this.CheckIfDisposed();

        ArgumentNullException.ThrowIfNull(stream);

        await stream.WriteAsync(new Memory<byte>(this.rentedBuffer, 0, this.written), cancellationToken).ConfigureAwait(false);
        this.committed += this.written;

        this.ClearHelper();
    }

    public void CopyTo(Stream stream)
    {
        this.CheckIfDisposed();

        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(this.rentedBuffer, 0, this.written);
        this.committed += this.written;

        this.ClearHelper();
    }

    public void Advance(int count)
    {
        this.CheckIfDisposed();

        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);

        if (this.written > this.rentedBuffer.Length - count)
        {
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        }

        this.written += count;
    }

    // Returns the rented buffer back to the pool
    public void Dispose()
    {
        if (this.rentedBuffer == null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(this.rentedBuffer, clearArray: true);
        this.rentedBuffer = null;
        this.written = 0;
    }

    private void CheckIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this.rentedBuffer == null, this);
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.CheckIfDisposed();

        ArgumentOutOfRangeException.ThrowIfLessThan(sizeHint, 0);

        this.CheckAndResizeBuffer(sizeHint);
        return this.rentedBuffer.AsMemory(this.written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.CheckIfDisposed();

        ArgumentOutOfRangeException.ThrowIfLessThan(sizeHint, 0);

        this.CheckAndResizeBuffer(sizeHint);
        return this.rentedBuffer.AsSpan(this.written);
    }

    private void CheckAndResizeBuffer(int sizeHint)
    {
        Debug.Assert(sizeHint >= 0);

        if (sizeHint == 0)
        {
            sizeHint = MinimumBufferSize;
        }

        int availableSpace = this.rentedBuffer.Length - this.written;

        if (sizeHint > availableSpace)
        {
            int growBy = sizeHint > this.rentedBuffer.Length ? sizeHint : this.rentedBuffer.Length;

            int newSize = checked(this.rentedBuffer.Length + growBy);

            byte[] oldBuffer = this.rentedBuffer;

            this.rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);

            Debug.Assert(oldBuffer.Length >= this.written);
            Debug.Assert(this.rentedBuffer.Length >= this.written);

            oldBuffer.AsSpan(0, this.written).CopyTo(this.rentedBuffer);
            ArrayPool<byte>.Shared.Return(oldBuffer, clearArray: true);
        }

        Debug.Assert(this.rentedBuffer.Length - this.written > 0);
        Debug.Assert(this.rentedBuffer.Length - this.written >= sizeHint);
    }
}
#endif