//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;

    // Small wrapper to make pooled ownership explicit and efficient via Memory/Span
    internal sealed class PooledByteOwner : IMemoryOwner<byte>, IDisposable
    {
        private readonly ArrayPoolManager pool;

        internal byte[] Buffer { get; private set; }

        internal int Length { get; private set; }

        private bool disposed;

        public PooledByteOwner(ArrayPoolManager pool, byte[] buffer, int length)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.Length = length;
        }

        public Memory<byte> Memory => new Memory<byte>(this.Buffer, 0, this.Length);

        internal byte[] Array => this.Buffer;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.pool.Return(this.Buffer);
            this.Buffer = System.Array.Empty<byte>();
            this.Length = 0;
        }
    }
}
