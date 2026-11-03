//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class UndefinedPartitionKeyComponent: IPartitionKeyComponent
    {
        public static readonly UndefinedPartitionKeyComponent Value = new UndefinedPartitionKeyComponent();

        internal UndefinedPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            UndefinedPartitionKeyComponent otherUndefined = other as UndefinedPartitionKeyComponent;
            if (otherUndefined == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.Undefined;
        }

        public double GetHashValue()
        {
            return 0;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public void JsonEncode(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }

        public object ToObject()
        {
            return Undefined.Value;
        }

        public IPartitionKeyComponent Truncate()
        {
            return this;
        }

        public void WriteForHashing(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Undefined);
        }

        public void WriteForHashingV2(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Undefined);
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.Undefined);
        }
    }
}
