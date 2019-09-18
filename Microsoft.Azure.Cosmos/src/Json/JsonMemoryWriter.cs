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
