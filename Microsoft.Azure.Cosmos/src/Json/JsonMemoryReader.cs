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
            byte value = this.position < this.buffer.Length ? (byte)this.buffer.Span[this.position] : (byte)0;
            this.position++;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Peek() => this.position < this.buffer.Length ? (byte)this.buffer.Span[this.position] : (byte)0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken() => this.buffer.Slice(this.position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(
            int startPosition) => this.buffer.Slice(startPosition);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetBufferedRawJsonToken(
            int startPosition,
            int endPosition) => this.buffer.Slice(startPosition, endPosition - startPosition);
    }
}