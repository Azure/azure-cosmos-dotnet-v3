// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Rntbd
{
    using System;
    using System.Runtime.InteropServices;

    internal struct BytesDeserializer
    {
        private readonly Memory<byte> metadata;

        public BytesDeserializer(byte[] metadata, int length) : this()
        {
            this.metadata = new Memory<byte>(metadata, 0, length);
            this.Position = 0;
        }

        public int Position { get; private set; }

        public int Length => this.metadata.Length;

        public ushort ReadUInt16()
        {
            ushort value = MemoryMarshal.Read<ushort>(this.metadata.Span.Slice(this.Position));
            this.Position += 2;
            return value;
        }

        public void AdvancePositionByUInt16() => this.Position += 2;

        public byte ReadByte()
        {
            byte value = this.metadata.Span[this.Position];
            this.Position++;
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = MemoryMarshal.Read<uint>(this.metadata.Span.Slice(this.Position));
            this.Position += 4;
            return value;
        }

        public void AdvancePositionByUInt32() => this.Position += 4;

        public int ReadInt32()
        {
            int value = MemoryMarshal.Read<int>(this.metadata.Span.Slice(this.Position));
            this.Position += 4;
            return value;
        }

        public void AdvancePositionByInt32() => this.Position += 4;

        public ulong ReadUInt64()
        {
            ulong value = MemoryMarshal.Read<ulong>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public void AdvancePositionByUInt64() => this.Position += 8;

        public long ReadInt64()
        {
            long value = MemoryMarshal.Read<long>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public void AdvancePositionByInt64() => this.Position += 8;

        public float ReadSingle()
        {
            float value = MemoryMarshal.Read<float>(this.metadata.Span.Slice(this.Position));
            this.Position += 4;
            return value;
        }

        public void AdvancePositionBySingle() => this.Position += 4;

        public double ReadDouble()
        {
            double value = MemoryMarshal.Read<double>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public void AdvancePositionByDouble() => this.Position += 8;

        public Guid ReadGuid()
        {
            Guid value = MemoryMarshal.Read<Guid>(this.metadata.Span.Slice(this.Position));
            this.Position += 16;
            return value;
        }

        public void AdvancePositionByGuid() => this.Position += 16;

        public ReadOnlyMemory<byte> ReadBytes(int length)
        {
            ReadOnlyMemory<byte> value = this.metadata.Slice(this.Position, length);
            this.Position += length;
            return value;
        }

        public void AdvancePositionByBytes(int count) => this.Position += count;
    }
}
