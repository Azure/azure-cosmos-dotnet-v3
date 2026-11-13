//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class NumberPartitionKeyComponent : IPartitionKeyComponent
    {
        private readonly double value;
        
        public static readonly NumberPartitionKeyComponent Zero = new NumberPartitionKeyComponent(0);

        public NumberPartitionKeyComponent(double value)
        {
            this.value = value;
        }

        public double Value
        {
            get
            {
                return this.value;
            }
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            NumberPartitionKeyComponent otherNumber = other as NumberPartitionKeyComponent;
            if (otherNumber == null)
            {
                throw new ArgumentException("other");
            }

            return this.value.CompareTo(otherNumber.value);
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.Number;
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public void JsonEncode(JsonWriter writer)
        {
            writer.WriteValue(this.value);
        }

        public object ToObject()
        {
            return this.value;
        }

        public IPartitionKeyComponent Truncate()
        {
            return this;
        }

        public void WriteForHashing(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Number);
            writer.Write(this.value);
        }

        public void WriteForHashingV2(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Number);
            writer.Write(this.value);
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.Number);

            UInt64 payload = NumberPartitionKeyComponent.EncodeDoubleAsUInt64(this.value);

            // Encode first chunk with 8-bits of payload
            binaryWriter.Write((byte)(payload >> (64 - 8)));
            payload <<= 8;

            // Encode remaining chunks with 7 bits of payload followed by single "1" bit each.
            byte byteToWrite = 0;
            bool firstIteration = true;
            do
            {
                if (!firstIteration)
                {
                    binaryWriter.Write(byteToWrite);
                }
                else
                {
                    firstIteration = false;
                }

                byteToWrite = (byte)((payload >> (64 - 8)) | 0x01);
                payload <<= 7;
            } while (payload != 0);

            // Except for last chunk that ends with "0" bit.
            binaryWriter.Write((byte)(byteToWrite & 0xFE));
        }

        /// <summary>
        /// Constructs a NumberPartitionKeyComponent from byte string. This is only for testing/debugging. Please do not use in actual product code.
        /// </summary>
        public static IPartitionKeyComponent FromHexEncodedBinaryString(byte[] byteString, ref int byteStringOffset)
        {
            int offset = 64;
            UInt64 payload = 0;

            // Decode first 8-bit chunk
            offset -= 8;
            payload |= (Convert.ToUInt64(byteString[byteStringOffset++])) << offset;

            // Decode remaining 7-bit chunks
            while (true)
            {
                if (byteStringOffset >= byteString.Length)
                {
                    throw new InvalidDataException("Incorrect byte string without termination");
                }

                byte currentByte = byteString[byteStringOffset++];

                offset -= 7;
                payload |= (Convert.ToUInt64(currentByte >> 1)) << offset;

                if ((currentByte & 0x01) == 0)
                {
                    break;
                }
            }

            double dblValue = NumberPartitionKeyComponent.DecodeDoubleFromUInt64(payload);

            return new NumberPartitionKeyComponent(dblValue);
        }

        // Helpers
        private static UInt64 EncodeDoubleAsUInt64(double value)
        {
            UInt64 valueInUInt64 = (UInt64)BitConverter.DoubleToInt64Bits(value);
            UInt64 mask = 0x8000000000000000;
            return (valueInUInt64 < mask) ? valueInUInt64 ^ mask : (~valueInUInt64) + 1;
        }

        private static double DecodeDoubleFromUInt64(UInt64 value)
        {
            UInt64 mask = 0x8000000000000000;
            value = (value < mask) ? ~(value - 1) : value ^ mask;
            return BitConverter.Int64BitsToDouble((Int64)value);
        }
    }
}
