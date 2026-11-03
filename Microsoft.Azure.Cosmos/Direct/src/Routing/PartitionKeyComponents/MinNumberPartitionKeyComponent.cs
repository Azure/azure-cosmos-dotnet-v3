//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class MinNumber
    { 
        public static readonly MinNumber Value = new MinNumber();

        private MinNumber()
        {
        }
    }

    internal sealed class MinNumberPartitionKeyComponent : IPartitionKeyComponent
    {
        public static readonly MinNumberPartitionKeyComponent Value = new MinNumberPartitionKeyComponent();

        private MinNumberPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            MinNumberPartitionKeyComponent otherNumber = other as MinNumberPartitionKeyComponent;
            if (otherNumber == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.MinNumber;
        }

        public override int GetHashCode()
        {
            return 0;
        }
        public IPartitionKeyComponent Truncate()
        {
            throw new InvalidOperationException();
        }

        public void WriteForHashing(BinaryWriter writer)
        {
            throw new InvalidOperationException();
        }

        public void WriteForHashingV2(BinaryWriter writer)
        {
            throw new InvalidOperationException();
        }

        public void JsonEncode(JsonWriter writer)
        {
            PartitionKeyInternalJsonConverter.JsonEncode(this, writer);
        }

        public object ToObject()
        {
            return MinNumber.Value;
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.MinNumber);
        }
    }
}
