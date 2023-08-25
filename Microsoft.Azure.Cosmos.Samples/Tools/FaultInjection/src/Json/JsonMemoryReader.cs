// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.CompilerServices;

    internal abstract class JsonMemoryReader
    {
        protected readonly ReadOnlyMemory<byte> buffer;
        protected int position;

        protected JsonMemoryReader(ReadOnlyMemory<byte> buffer)
        {
            this.buffer = buffer;
        }

        public bool IsEof => this.position >= this.buffer.Length;

        public int Position => this.position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read()
        {
            byte value = this.position < this.buffer.Length ? this.buffer.Span[this.position] : (byte)0;
            this.position++;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Peek()
        {
            return this.position < this.buffer.Length ? this.buffer.Span[this.position] : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken()
        {
            return this.buffer.Slice(this.position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(
            int startPosition)
        {
            return this.buffer.Slice(startPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(
            int startPosition,
            int endPosition)
        {
            return this.buffer.Slice(startPosition, endPosition - startPosition);
        }
    }
}