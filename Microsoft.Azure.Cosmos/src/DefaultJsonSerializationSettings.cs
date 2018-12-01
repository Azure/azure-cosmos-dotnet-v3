//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    internal static class DefaultJsonSerializationSettings
    {
        public static readonly JsonSerializerSettings Value = new JsonSerializerSettings();
    }
}
