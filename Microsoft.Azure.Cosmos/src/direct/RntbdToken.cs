//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
#if COSMOSCLIENT
    using System.Buffers;
#endif
    using System.Diagnostics;
    using System.Runtime.InteropServices;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif
    using Microsoft.Azure.Documents.Rntbd;

    internal enum RntbdTokenTypes : byte
    {
        // All values are encoded as little endian byte sequences.

        // System.Byte, aka byte.
        Byte = 0x00,
        // System.UInt16, aka ushort.
        UShort = 0x01,
        // System.UInt32, aka uint.
        ULong = 0x02,
        // System.Int32, aka int.
        Long = 0x03,
        // System.UInt64, aka ulong.
        ULongLong = 0x04,
        // System.Int64, aka long.
        LongLong = 0x05,
        // GUID (128 bits) stored as a byte array.
        Guid = 0x06,
        // UTF-8 encoded string. At most 255 bytes.
        SmallString = 0x07,
        // UTF-8 encoded string. At most 64Ki-1 bytes.
        String = 0x08,
        // UTF-8 encoded string. At most 4Gi-1 bytes.
        ULongString = 0x09,
        // Byte array. At most 255 bytes.
        SmallBytes = 0x0A,
        // Byte array. At most 64Ki-1 bytes.
        Bytes = 0x0B,
        // Byte array. At most 4Gi-1 bytes.
        ULongBytes = 0x0C,
        // System.Single, aka float.
        Float = 0x0D,
        // System.Double, aka double.
        Double = 0x0E,

        Invalid = 0xFF,
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RntbdTokenValue
    {
        [FieldOffset(0)]
        public byte valueByte;
        [FieldOffset(0)]
        public ushort valueUShort;
        [FieldOffset(0)]
        public UInt32 valueULong;
        [FieldOffset(0)]
        public UInt64 valueULongLong;
        [FieldOffset(0)]
        public Int32 valueLong;
        [FieldOffset(0)]
        public float valueFloat;
        [FieldOffset(0)]
        public double valueDouble;
        [FieldOffset(0)]
        public Int64 valueLongLong;
        [FieldOffset(8)]
        public Guid valueGuid;

        [FieldOffset(24)]
#if COSMOSCLIENT
        public ReadOnlyMemory<byte> valueBytes; // used for content of all 3 byte types and also all 3 string types (since UTF-8 strings are stored as byte[] in .Net)
#else
        public byte[] valueBytes;
#endif
    }

    internal sealed class RntbdToken
    {
        private readonly Action<RntbdToken> isPresentCallBack;
        private readonly ushort identifier;
        private readonly RntbdTokenTypes type;
        private readonly bool isRequired;

        private bool isPresentHelper;
        public RntbdTokenValue value;

        public bool isPresent
        {
            get => this.isPresentHelper;
            set
            {
                if (value)
                {
                    this.isPresentCallBack?.Invoke(this);
                }
                
                this.isPresentHelper = value;
            }
        }

        public RntbdToken(
            bool isRequired,
            RntbdTokenTypes type,
            ushort identifier,
            Action<RntbdToken> isPresentCallBack)
        {
            this.isRequired = isRequired;
            this.isPresent = false;
            this.type = type;
            this.identifier = identifier;
            this.value = new RntbdTokenValue();
            this.isPresentCallBack = isPresentCallBack;
        }

        public RntbdTokenTypes GetTokenType()
        {
            return this.type;
        }

        public ushort GetTokenIdentifier()
        {
            return this.identifier;
        }

        public bool IsRequired()
        {
            return this.isRequired;
        }

        public void SerializeToBinaryWriter(ref BytesSerializer writer, out int written)
        {
            if(!this.isPresent && this.isRequired)
            {
                throw new BadRequestException();
            }

            if(this.isPresent)
            {
                writer.Write((UInt16)this.identifier);
                writer.Write((byte)this.type);

                const int tokenOverhead = sizeof(UInt16) + sizeof(byte);

                switch(this.type)
                {
                    case RntbdTokenTypes.Byte:
                        writer.Write(this.value.valueByte);
                        written = tokenOverhead + sizeof(byte);
                        break;
                    case RntbdTokenTypes.UShort:
                        writer.Write(this.value.valueUShort);
                        written = tokenOverhead + sizeof(UInt16);
                        break;
                    case RntbdTokenTypes.ULong:
                        writer.Write(this.value.valueULong);
                        written = tokenOverhead + sizeof(UInt32);
                        break;
                    case RntbdTokenTypes.Long:
                        writer.Write(this.value.valueLong);
                        written = tokenOverhead + sizeof(Int32);
                        break;
                    case RntbdTokenTypes.ULongLong:
                        writer.Write(this.value.valueULongLong);
                        written = tokenOverhead + sizeof(UInt64);
                        break;
                    case RntbdTokenTypes.LongLong:
                        writer.Write(this.value.valueLongLong);
                        written = tokenOverhead + sizeof(Int64);
                        break;
                    case RntbdTokenTypes.Float:
                        writer.Write(this.value.valueFloat);
                        written = tokenOverhead + sizeof(float);
                        break;
                    case RntbdTokenTypes.Double:
                        writer.Write(this.value.valueDouble);
                        written = tokenOverhead + sizeof(double);
                        break;
                    case RntbdTokenTypes.Guid:
                        {
                            byte[] guidBytes = this.value.valueGuid.ToByteArray();
                            writer.Write(guidBytes);
                            written = tokenOverhead + guidBytes.Length;
                            break;
                        }
                    case RntbdTokenTypes.SmallBytes:
                    case RntbdTokenTypes.SmallString:
                        if (this.value.valueBytes.Length > byte.MaxValue)
                        {
                            throw new RequestEntityTooLargeException();
                        }

                        writer.Write((byte)this.value.valueBytes.Length);
                        writer.Write(this.value.valueBytes);
                        written = tokenOverhead + sizeof(byte) + this.value.valueBytes.Length;
                        break;
                    case RntbdTokenTypes.Bytes:
                    case RntbdTokenTypes.String:
                        if (this.value.valueBytes.Length > ushort.MaxValue)
                        {
                            throw new RequestEntityTooLargeException();
                        }

                        writer.Write((UInt16)this.value.valueBytes.Length);
                        writer.Write(this.value.valueBytes);
                        written = tokenOverhead + sizeof(UInt16) + this.value.valueBytes.Length;
                        break;
                    case RntbdTokenTypes.ULongString:
                    case RntbdTokenTypes.ULongBytes:
                        writer.Write((UInt32)this.value.valueBytes.Length);
                        writer.Write(this.value.valueBytes);
                        written = tokenOverhead + sizeof(UInt32) + this.value.valueBytes.Length;
                        break;
                    default:
                        Debug.Assert(false, "Unexpected RntbdTokenType", "Unexpected RntbdTokenType to serialize: {0}", this.type);
                        throw new BadRequestException();
                }
            }
            else
            {
                written = 0;
            }
        }
    }
}
