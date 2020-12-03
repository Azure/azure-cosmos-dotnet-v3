// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    /// <summary>
    /// The level (of information) for an <see cref="ITrace"/>.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        enum TraceLevel
    {
        /// <summary>
        /// Output no tracing and debugging messages.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Output error-handling messages.
        /// </summary>
        Error = 1,

        /// <summary>
        /// Output warnings and error-handling messages.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Output informational messages, warnings, and error-handling messages.
        /// </summary>
        Info = 3,

        /// <summary>
        /// Output all debugging and tracing messages.
        /// </summary>
        Verbose = 4
    }
}
