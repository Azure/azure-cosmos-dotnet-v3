//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Interop.Mongo
{
    using Newtonsoft.Json;

    internal sealed class BulkInsertStoredProcedureResult
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }
    }
}
