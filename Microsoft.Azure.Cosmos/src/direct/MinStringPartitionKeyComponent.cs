//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class MinString
    { 
        public static readonly MinString Value = new MinString();

        private MinString()
        {
        }
    }

    internal sealed class MinStringPartitionKeyComponent : IPartitionKeyComponent
    {
        public static readonly MinStringPartitionKeyComponent Value = new MinStringPartitionKeyComponent();

        private MinStringPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            MinStringPartitionKeyComponent otherString = other as MinStringPartitionKeyComponent;
            if (otherString == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.MinString;
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
            return MinString.Value;
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.MinString);
        }
    }
}
