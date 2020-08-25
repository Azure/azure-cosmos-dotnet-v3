//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.IO;

    using Newtonsoft.Json;

    internal interface IPartitionKeyComponent
    {
        int CompareTo(IPartitionKeyComponent other);

        int GetTypeOrdinal();

        void JsonEncode(JsonWriter writer);

        object ToObject();

        void WriteForHashing(BinaryWriter binaryWriter);

        void WriteForHashingV2(BinaryWriter binaryWriter);

        void WriteForBinaryEncoding(BinaryWriter binaryWriter);

        IPartitionKeyComponent Truncate();
    }
}
