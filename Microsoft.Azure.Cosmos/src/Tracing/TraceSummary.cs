// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

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
        private int failedRequestCount = 0;

        public void IncrementFailedCount()
        {
            Interlocked.Increment(ref this.failedRequestCount);
        }

        public int GetFailedCount()
        {
            return this.failedRequestCount;
        }

    }
}
