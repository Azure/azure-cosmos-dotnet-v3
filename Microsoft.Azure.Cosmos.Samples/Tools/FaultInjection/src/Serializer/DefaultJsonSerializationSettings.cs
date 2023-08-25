//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    internal static class DefaultJsonSerializationSettings
    {
        public static readonly JsonSerializerSettings Value = new JsonSerializerSettings()
        {
            MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
        };
    }
}
