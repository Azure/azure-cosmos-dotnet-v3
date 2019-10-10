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

        public int Position { get; set; }

        public Memory<byte> Buffer
        {
            get
            {
                return this.buffer;
            }
        }

        public Memory<byte> Cursor
        {
            get
            {
                return this.Buffer.Slice(this.Position);
            }
        }

        public void Write(ReadOnlySpan<byte> value)
        {
            this.EnsureRemainingBufferSpace(value.Length);
            value.CopyTo(this.Cursor.Span);
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
