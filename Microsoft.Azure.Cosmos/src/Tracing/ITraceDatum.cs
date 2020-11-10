// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    /// <summary>
    /// The interface for a single entry in the <see cref="ITrace.Data"/> dictionary.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
        interface ITraceDatum
    {
        /// <summary>
        /// Accept the visitor.
        /// </summary>
        /// <param name="traceDatumVisitor">The visitor to accept.</param>
        void Accept(ITraceDatumVisitor traceDatumVisitor);
    }
}
