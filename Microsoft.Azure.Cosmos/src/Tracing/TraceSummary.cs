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

        /// <summary>
        ///  The increment of failed requests with thread safe for an <see cref="ITrace"/>
        /// </summary>
        public void IncrementFailedCount()
        {
            Interlocked.Increment(ref this.failedRequestCount);
        }

        /// <summary>
        ///  The return the count of failed requests for an <see cref="ITrace"/>
        /// </summary>
        /// <returns>The value of failed requests count</returns>
        public int GetFailedCount()
        {
            return this.failedRequestCount;
        }

    }
}
