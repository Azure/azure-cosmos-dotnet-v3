// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    internal interface IJsonBinaryWriterExtensions : IJsonWriter
    {
        void WriteRawJsonValue(
            ReadOnlyMemory<byte> rootBuffer,
            ReadOnlyMemory<byte> rawJsonValue,
            bool isRootNode,
            bool isFieldName);
    }
}
