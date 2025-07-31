// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    public interface IJsonTextWriterExtensions : IJsonWriter
    {
        void WriteRawJsonValue(
            ReadOnlyMemory<byte> buffer,
            bool isFieldName);
    }
}
