// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal interface IJsonTextWriterExtensions : IJsonWriter
    {
        void WriteRawJsonValue(
            ReadOnlyMemory<byte> buffer,
            bool isFieldName);
    }
}
