//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Newtonsoft.Json;

    internal sealed class NullPartitionKeyComponent : IPartitionKeyComponent
    {
        public static readonly NullPartitionKeyComponent Value = new NullPartitionKeyComponent();

        private NullPartitionKeyComponent()
        {
        }

        public int CompareTo(IPartitionKeyComponent other)
        {
            NullPartitionKeyComponent otherBool = other as NullPartitionKeyComponent;
            if (otherBool == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.Null;
        }

        public IPartitionKeyComponent Truncate()
        {
            return this;
        }

        public void WriteForHashing(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Null);
        }

        public void WriteForHashingV2(BinaryWriter writer)
        {
            writer.Write((byte)PartitionKeyComponentType.Null);
        }

        public void JsonEncode(JsonWriter writer)
        {
            writer.WriteValue((object)null);
        }

        public object ToObject()
        {
            return null;
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.Null);
        }
    }
}
