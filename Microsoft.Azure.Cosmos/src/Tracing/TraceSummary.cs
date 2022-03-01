// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

#if INTERNAL
    public
#else
    internal
#endif  
    class TraceSummary
    {
        public int failedRequestCount { get; set; }
    }
}
