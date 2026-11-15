//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class StringPartitionKeyComponent : IPartitionKeyComponent
    {
        public const int MaxStringChars = 100;
        public const int MaxStringBytesToAppend = 100;
        private readonly string value;
        private readonly byte[] utf8Value;

        public StringPartitionKeyComponent(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            this.value = value;
            this.utf8Value = Encoding.UTF8.GetBytes(value);
        }

        public void JsonEncode(JsonWriter writer)
        {
            writer.WriteValue(this.value);
        }

        public object ToObject()
        {
            return this.value;
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            StringPartitionKeyComponent otherString = other as StringPartitionKeyComponent;
            if (otherString == null)
            {
                throw new ArgumentException("other");
            }

            return string.CompareOrdinal(this.value, otherString.value);
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.String;
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public IPartitionKeyComponent Truncate()
        {
            if (this.value.Length > MaxStringChars)
            {
                return new StringPartitionKeyComponent(this.value.Substring(0, MaxStringChars));
            }

            return this;
        }

        public void WriteForHashing(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.String);
            writer.Write(this.utf8Value);
            writer.Write((byte)0);
        }

        public void WriteForHashingV2(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.String);
            writer.Write(this.utf8Value);
            writer.Write((byte)0xFF);
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.String);

            bool shortString = this.utf8Value.Length <= MaxStringBytesToAppend;

            for (int index = 0; index < (shortString ? this.utf8Value.Length : MaxStringBytesToAppend + 1); index++)
            {
                byte charByte = this.utf8Value[index];
                if (charByte < 0xFF) charByte++;
                binaryWriter.Write(charByte);
            }

            if (shortString)
            {
                binaryWriter.Write((byte)0x00);
            }
        }

        /// <summary>
        /// Constructs a StringPartitionKeyComponent from byte string. This is only for testing/debugging. Please do not use in actual product code.
        /// </summary>
        public static IPartitionKeyComponent FromHexEncodedBinaryString(byte[] byteString, ref int offset)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int index = 0; index <= MaxStringBytesToAppend; index++)
            {
                byte currentByte = byteString[offset++];
                if (currentByte == 0x00) break;
                char currentChar = (char)(currentByte - 1);

                stringBuilder.Append(currentChar);
            }

            return new StringPartitionKeyComponent(stringBuilder.ToString());
        }
    }
}
