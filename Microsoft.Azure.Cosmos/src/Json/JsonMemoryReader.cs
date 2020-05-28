// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.InteropServices;

    internal abstract class JsonMemoryReader
    {
        protected readonly byte[] buffer;
        protected int position;

        protected JsonMemoryReader(ReadOnlyMemory<byte> buffer)
        {
            if (!MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException($"Failed to get '{nameof(segment)}'");
            }

            this.buffer = segment.Array;
            this.position = segment.Offset;
        }

        public bool IsEof => this.position >= this.buffer.Length;

        public int Position => this.position;

        public byte Read()
        {
            byte value = this.position < this.buffer.Length ? (byte)this.buffer[this.position] : (byte)0;
            this.position++;
            return value;
        }

        public byte Peek()
        {
            byte value = this.position < this.buffer.Length ? (byte)this.buffer[this.position] : (byte)0;
            return value;
        }

        public ReadOnlyMemory<byte> GetBufferedRawJsonToken()
        {
            return this.buffer.AsMemory().Slice(this.position);
        }

        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(int startPosition)
        {
            return this.buffer.AsMemory().Slice(startPosition);
        }

        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(int startPosition, int endPosition)
        {
            return this.buffer.AsMemory().Slice(startPosition, endPosition - startPosition);
        }
    }
}
