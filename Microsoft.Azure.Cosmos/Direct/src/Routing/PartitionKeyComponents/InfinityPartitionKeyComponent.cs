//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class InfinityPartitionKeyComponent : IPartitionKeyComponent
    {
        public int CompareTo(IPartitionKeyComponent other)
        {
            InfinityPartitionKeyComponent otherInfinity = other as InfinityPartitionKeyComponent;
            if (otherInfinity == null)
            {
                throw new ArgumentException("other");
            }

            return 0;
        }

        public int GetTypeOrdinal()
        {
            return (int)PartitionKeyComponentType.Infinity;
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

        public override int GetHashCode()
        {
            return 0;
        }

        public void JsonEncode(JsonWriter writer)
        {
            throw new NotImplementedException();
        }

        public object ToObject()
        {
            throw new NotImplementedException();
        }

        public void WriteForBinaryEncoding(BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)PartitionKeyComponentType.Infinity);
        }
    }
}
