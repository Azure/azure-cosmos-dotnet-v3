//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Buffers;

    internal sealed class BufferProvider
    {
        private readonly ArrayPool<byte> arrayPool;

        public BufferProvider()
        {
            this.arrayPool = ArrayPool<byte>.Create();
        }

        public DisposableBuffer GetBuffer(int desiredLength)
        {
            return new DisposableBuffer(this, desiredLength);
        }

        public struct DisposableBuffer : IDisposable
        {
            private readonly BufferProvider provider;

            public DisposableBuffer(byte[] buffer)
            {
                this.provider = null;
                this.Buffer = new ArraySegment<byte>(buffer, 0, buffer.Length);
            }

            public DisposableBuffer(BufferProvider provider, int desiredLength)
            {
                this.provider = provider;
                this.Buffer = new ArraySegment<byte>(provider.arrayPool.Rent(desiredLength), 0, desiredLength);
            }

            public ArraySegment<byte> Buffer { get; private set; }

            public void Dispose()
            {
                if (this.Buffer.Array != null)
                {
                    this.provider?.arrayPool.Return(this.Buffer.Array);
                    this.Buffer = default;
                }
            }
        }
    }
}
