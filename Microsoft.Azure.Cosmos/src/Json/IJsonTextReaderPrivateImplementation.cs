// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    /// <summary>
    /// This is meant more as a friends class. Do not use unless you have a strong reason to.
    /// </summary>
    internal interface IJsonTextReaderPrivateImplementation : IJsonReader
    {
        Utf8Memory GetBufferedJsonToken();
    }
}
