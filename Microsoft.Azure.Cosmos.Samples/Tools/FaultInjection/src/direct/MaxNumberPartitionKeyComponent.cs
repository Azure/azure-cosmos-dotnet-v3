//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class MaxNumber
    { 
        public static readonly MaxNumber Value = new MaxNumber();

        private MaxNumber()
        {
        }
    }

    internal sealed class MaxNumberPartitionKeyComponent : IPartitionKeyComponent
    {
        public static readonly MaxNumberPartitionKeyComponent Value = new MaxNumberPartitionKeyComponent();

        private MaxNumberPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            MaxNumberPartitionKeyComponent otherNumber = other as MaxNumberPartitionKeyComponent;
            if (otherNumber == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.MaxNumber;
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
            return MaxNumber.Value;
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.MaxNumber);
        }
    }
}
