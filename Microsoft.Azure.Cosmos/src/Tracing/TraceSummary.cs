// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The total count of failed requests for an <see cref="ITrace"/>.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif  
    class TraceSummary
    {
        /// <summary>
        ///  The total count of failed requests for an <see cref="ITrace"/>
        /// </summary>
        public int failedRequestCount { get; set; }
    }
}
