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
            this.Length = length;
        }

        public int Position { get; private set; }

        public int Length { get; }

        public ushort ReadUInt16()
        {
            ushort value = MemoryMarshal.Read<ushort>(this.metadata.Span.Slice(this.Position));
            this.Position += 2;
            return value;
        }

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

        public int ReadInt32()
        {
            int value = MemoryMarshal.Read<int>(this.metadata.Span.Slice(this.Position));
            this.Position += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong value = MemoryMarshal.Read<ulong>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public long ReadInt64()
        {
            long value = MemoryMarshal.Read<long>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public float ReadSingle()
        {
            float value = MemoryMarshal.Read<float>(this.metadata.Span.Slice(this.Position));
            this.Position += 4;
            return value;
        }

        public double ReadDouble()
        {
            double value = MemoryMarshal.Read<double>(this.metadata.Span.Slice(this.Position));
            this.Position += 8;
            return value;
        }

        public Guid ReadGuid()
        {
            Guid value = MemoryMarshal.Read<Guid>(this.metadata.Span.Slice(this.Position));
            this.Position += 16;
            return value;
        }

        public ReadOnlyMemory<byte> ReadBytes(int length)
        {
            ReadOnlyMemory<byte> value = this.metadata.Slice(this.Position, length);
            this.Position += length;
            return value;
        }
    }
}
