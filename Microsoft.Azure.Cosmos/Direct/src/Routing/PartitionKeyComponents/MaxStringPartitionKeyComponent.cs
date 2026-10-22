//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class MaxString
    { 
        public static readonly MaxString Value = new MaxString();

        private MaxString()
        {
        }
    }

    internal sealed class MaxStringPartitionKeyComponent : IPartitionKeyComponent
    {
        public static readonly MaxStringPartitionKeyComponent Value = new MaxStringPartitionKeyComponent();

        private MaxStringPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            MaxStringPartitionKeyComponent otherString = other as MaxStringPartitionKeyComponent;
            if (otherString == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.MaxString;
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
            return MaxString.Value;
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.MaxString);
        }
    }
}
