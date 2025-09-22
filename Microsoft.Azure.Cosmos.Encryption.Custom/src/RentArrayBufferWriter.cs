// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom;

#if NET8_0_OR_GREATER
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Backwards-compatible byte-specific pooled buffer writer.
/// Now delegates to generic <see cref="PooledBufferWriter{T}"/>.
/// </summary>
internal sealed class RentArrayBufferWriter : IBufferWriter<byte>, IDisposable
{
    private readonly PooledBufferWriter<byte> inner;
    private long committed;

    public RentArrayBufferWriter(int initialCapacity = 256)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        // Always clear on dispose & expand for safety (encryption scenarios handle sensitive data)
        this.inner = new PooledBufferWriter<byte>(initialCapacity, options: PooledBufferWriterOptions.ClearOnDispose | PooledBufferWriterOptions.ClearOnExpand | PooledBufferWriterOptions.ClearOnReset);
        this.committed = 0;
    }

    public (byte[], int) WrittenBuffer => (this.inner.GetInternalArray(), this.inner.Count); // internal array + logical length

    public ReadOnlyMemory<byte> WrittenMemory => this.inner.WrittenMemory;

    public ReadOnlySpan<byte> WrittenSpan => this.inner.WrittenSpan;

    public int BytesWritten => this.inner.Count;

    public long BytesCommitted => this.committed;

    public void Clear() => this.inner.Clear();

    public async Task CopyToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await stream.WriteAsync(this.WrittenMemory, cancellationToken).ConfigureAwait(false);
        this.committed += this.BytesWritten;
        this.Clear();
    }

    public void CopyTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(this.WrittenSpan);
        this.committed += this.BytesWritten;
        this.Clear();
    }

    public void Advance(int count) => this.inner.Advance(count);

    public Memory<byte> GetMemory(int sizeHint = 0) => this.inner.GetMemory(sizeHint);

    public Span<byte> GetSpan(int sizeHint = 0) => this.inner.GetSpan(sizeHint);

    public void Dispose() => this.inner.Dispose();
}
#endif