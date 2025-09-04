//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;

    // Chunked IBufferWriter that streams directly to the output Stream to avoid large contiguous growth.
    internal sealed class StreamChunkedBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private readonly Stream outputStream;
        private readonly ArrayPoolManager poolManager;
        private readonly int chunkSize;
        private byte[] currentBuffer;
        private int index;
        private bool disposed;
        private long totalBytes;

        internal StreamChunkedBufferWriter(Stream outputStream, ArrayPoolManager poolManager, int chunkSize)
        {
            ArgumentNullException.ThrowIfNull(outputStream);
            ArgumentNullException.ThrowIfNull(poolManager);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

            this.outputStream = outputStream;
            this.poolManager = poolManager;
            this.chunkSize = chunkSize;
        }

        internal long BytesWritten => this.totalBytes + this.index;

        internal int Flushes { get; private set; }

        public void Advance(int count)
        {
            if (count < 0 || this.currentBuffer == null || this.index + count > this.currentBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            this.index += count;
            if (this.index == this.currentBuffer.Length)
            {
                this.FlushCurrent();
            }
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            this.EnsureCapacity(sizeHint);
            return this.currentBuffer.AsMemory(this.index);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            this.EnsureCapacity(sizeHint);
            return this.currentBuffer.AsSpan(this.index);
        }

        private void EnsureCapacity(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

            int required = sizeHint == 0 ? 1 : sizeHint;
            if (this.currentBuffer == null)
            {
                int alloc = Math.Max(this.chunkSize, required);
                this.currentBuffer = this.poolManager.Rent(alloc);
                this.index = 0;
                return;
            }

            int remaining = this.currentBuffer.Length - this.index;
            if (remaining >= required)
            {
                return;
            }

            this.FlushCurrent();
            int newSize = Math.Max(this.chunkSize, required);
            this.currentBuffer = this.poolManager.Rent(newSize);
            this.index = 0;
        }

        private void FlushCurrent()
        {
            if (this.currentBuffer != null && this.index > 0)
            {
                this.outputStream.Write(this.currentBuffer, 0, this.index);
                this.totalBytes += this.index;
                this.index = 0;
                this.Flushes++;
            }

            if (this.currentBuffer != null)
            {
                this.poolManager.Return(this.currentBuffer);
                this.currentBuffer = null;
            }
        }

        internal void FinalFlush()
        {
            if (this.disposed)
            {
                return;
            }

            if (this.currentBuffer != null && this.index > 0)
            {
                this.outputStream.Write(this.currentBuffer, 0, this.index);
                this.totalBytes += this.index;
                this.index = 0;
                this.Flushes++;
            }

            if (this.currentBuffer != null)
            {
                this.poolManager.Return(this.currentBuffer);
                this.currentBuffer = null;
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.FinalFlush();
            this.disposed = true;
        }
    }
}
#endif
