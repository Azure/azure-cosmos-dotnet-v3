// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Cosmos Instrumentation Interface
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        interface ICosmosInstrumentation : IDisposable
    {
         /// <summary>
         /// Recording Attributes
         /// </summary>
         /// <param name="attributeKey"></param>
         /// <param name="attributeValue"></param>
        public void Record(string attributeKey, object attributeValue);

        /// <summary>
        /// Recording Request Diagnostics
        /// </summary>
        /// <param name="trace"></param>
        public void Record(ITrace trace);

        /// <summary>
        /// Mark Scope as failed and add exceptions in attribute
        /// </summary>
        /// <param name="exception"></param>
        public void MarkFailed(Exception exception);
    }
}
