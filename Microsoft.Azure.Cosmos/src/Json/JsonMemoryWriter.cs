// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    internal abstract class JsonMemoryWriter
    {
        protected byte[] buffer;

        public JsonMemoryWriter(int initialCapacity = 256)
        {
            this.buffer = new byte[256];
        }

        public int Position { get; set; }

        public Memory<byte> Buffer
        {
            get
            {
                return this.buffer;
            }
        }

        public void Write(byte value)
        {
            this.EnsureRemainingBufferSpace(sizeof(byte));
            this.buffer[this.Position] = value;
            this.Position++;
        }

        public void Write(ReadOnlySpan<byte> value)
        {
            this.EnsureRemainingBufferSpace(value.Length);
            value.CopyTo(this.buffer.AsSpan<byte>().Slice(this.Position));
            this.Position += value.Length;
        }

        public void EnsureRemainingBufferSpace(int size)
        {
            if (this.Position + size > this.buffer.Length)
            {
                this.Resize();
            }
        }

        private void Resize()
        {
            Array.Resize(ref this.buffer, this.buffer.Length * 2);
        }
    }
}
