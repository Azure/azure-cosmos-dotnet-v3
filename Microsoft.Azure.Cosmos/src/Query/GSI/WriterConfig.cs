//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal class WriterConfig
    {   
        public int ConnectionCount { get; set; }
        public int? WorkerCount { get; set; }
        public int MaximumSimultaneousRequests { get; set; }
    }
}
