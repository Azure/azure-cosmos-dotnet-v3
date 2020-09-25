// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    internal abstract class JsonMemoryWriter
    {
        protected byte[] buffer;

        protected JsonMemoryWriter(int initialCapacity = 256)
        {
            this.buffer = new byte[initialCapacity];
        }

        public int Position
        {
            get;
            set;
        }

        public Span<byte> Cursor => this.buffer.AsSpan().Slice(this.Position);

        public ReadOnlyMemory<byte> BufferAsMemory => this.buffer.AsMemory();

        public Span<byte> BufferAsSpan => this.buffer.AsSpan();

        public Memory<byte> RawBuffer => this.buffer;

        public void Write(ReadOnlySpan<byte> value)
        {
            this.EnsureRemainingBufferSpace(value.Length);
            value.CopyTo(this.Cursor);
            this.Position += value.Length;
        }

        public void EnsureRemainingBufferSpace(int size)
        {
            if (this.Position + size >= this.buffer.Length)
            {
                this.Resize(this.Position + size);
            }
        }

        private void Resize(int minNewSize)
        {
            if (minNewSize < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            long newLength = minNewSize * 2;
            newLength = Math.Min(newLength, int.MaxValue);
            Array.Resize(ref this.buffer, (int)newLength);
        }
    }
}
