//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System.IO;

    using System.Text.Json;

    internal interface IPartitionKeyComponent
    {
        int CompareTo(IPartitionKeyComponent other);

        int GetTypeOrdinal();

        void JsonEncode(Utf8JsonWriter writer);

        object ToObject();

        void WriteForHashing(BinaryWriter binaryWriter);

        void WriteForHashingV2(BinaryWriter binaryWriter);

        void WriteForBinaryEncoding(BinaryWriter binaryWriter);

        IPartitionKeyComponent Truncate();
    }
}
