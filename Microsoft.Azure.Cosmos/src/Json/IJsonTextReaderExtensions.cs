// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    internal interface IJsonTextReaderExtensions : IJsonReader
    {
        Utf8Memory GetBufferedJsonToken();
    }
}
