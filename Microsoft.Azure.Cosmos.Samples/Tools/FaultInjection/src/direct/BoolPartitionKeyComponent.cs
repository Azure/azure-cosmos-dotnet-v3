//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class BoolPartitionKeyComponent : IPartitionKeyComponent
    {
        private readonly bool value;

        public BoolPartitionKeyComponent(bool value)
        {
            this.value = value;
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            BoolPartitionKeyComponent otherBool = other as BoolPartitionKeyComponent;
            if (otherBool == null)
            {
                throw new ArgumentException("other");
            }

            return Math.Sign((this.value ? 1 : 0) - (otherBool.value ? 1 : 0));
        }

        public int GetTypeOrdinal()
        {
            return (int)(this.value ? PartitionKeyComponentType.True : PartitionKeyComponentType.False);
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

        public void WriteForHashing(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)(this.value ? PartitionKeyComponentType.True : PartitionKeyComponentType.False));
        }

        public void WriteForHashingV2(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)(this.value ? PartitionKeyComponentType.True : PartitionKeyComponentType.False));
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)(this.value ? PartitionKeyComponentType.True : PartitionKeyComponentType.False));
        }
    }
}
