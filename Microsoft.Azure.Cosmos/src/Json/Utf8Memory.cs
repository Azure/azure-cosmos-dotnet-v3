// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Utf8;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    readonly struct Utf8Memory
    {
        public static readonly Utf8Memory Empty = new Utf8Memory(ReadOnlyMemory<byte>.Empty);

        private Utf8Memory(ReadOnlyMemory<byte> utf8Bytes)
        {
            this.Memory = utf8Bytes;
        }

        public ReadOnlyMemory<byte> Memory { get; }

        public Utf8Span Span => Utf8Span.UnsafeFromUtf8BytesNoValidation(this.Memory.Span);

        public Utf8Memory Slice(int start) => new Utf8Memory(this.Memory.Slice(start));
        public Utf8Memory Slice(int start, int length) => new Utf8Memory(this.Memory.Slice(start, length));

        public bool IsEmpty => this.Memory.IsEmpty;
        public int Length => this.Memory.Length;

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(Utf8Memory utf8Memory)
        {
            return this.Memory.Equals(utf8Memory);
        }

        public override int GetHashCode()
        {
            return this.Memory.GetHashCode();
        }

        public override string ToString()
        {
            return this.Span.ToString();
        }

        public static Utf8Memory Create(ReadOnlyMemory<byte> utf8Bytes)
        {
            if (!Utf8Memory.TryCreate(utf8Bytes, out Utf8Memory utf8Memory))
            {
                throw new ArgumentException($"{nameof(utf8Bytes)} did not contain a valid UTF-8 byte sequence.");
            }

            return utf8Memory;
        }

        public static Utf8Memory UnsafeCreateNoValidation(ReadOnlyMemory<byte> utf8Bytes)
        {
            return new Utf8Memory(utf8Bytes);
        }

        public static bool TryCreate(ReadOnlyMemory<byte> utf8Bytes, out Utf8Memory utf8Memory)
        {
            if (!Utf8Span.TryParseUtf8Bytes(utf8Bytes.Span, out _))
            {
                utf8Memory = default;
                return false;
            }

            utf8Memory = new Utf8Memory(utf8Bytes);
            return true;
        }
    }
}
