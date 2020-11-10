// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Interface to represent a single node in a trace tree.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        interface ITrace : IDisposable
    {
        /// <summary>
        /// Gets the name of the node.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the ID of the node.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the information for what line of source code this trace was called on.
        /// </summary>
        CallerInfo CallerInfo { get; }

        /// <summary>
        /// Gets the time when the trace was started.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Gets the duration of the trace.
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Gets the level (of information) of the trace.
        /// </summary>
        TraceLevel Level { get; }

        /// <summary>
        /// Gets the component that governs this trace.
        /// </summary>
        TraceComponent Component { get; }

        /// <summary>
        /// Gets the parent node of this trace.
        /// </summary>
        ITrace Parent { get; }

        /// <summary>
        /// Gets the children of this trace.
        /// </summary>
        IReadOnlyList<ITrace> Children { get; }

        /// <summary>
        /// Gets additional datum associated with this trace.
        /// </summary>
        IReadOnlyDictionary<string, object> Data { get; }

        /// <summary>
        /// Starts a Trace and adds it as a child to this instance.
        /// </summary>
        /// <param name="name">The name of the child.</param>
        /// <param name="memberName">The member name of the child.</param>
        /// <param name="sourceFilePath">The path to the source file of the child.</param>
        /// <param name="sourceLineNumber">The line number of the child.</param>
        /// <returns>A reference to the initialized child (that needs to be disposed to stop the timing).</returns>
        ITrace StartChild(
            string name,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0);

        /// <summary>
        /// Starts a trace and adds it as a child to this instance.
        /// </summary>
        /// <param name="name">The name of the child.</param>
        /// <param name="component">The component that governs the child.</param>
        /// <param name="level">The level (of information) of the child.</param>
        /// <param name="memberName">The member name of the child.</param>
        /// <param name="sourceFilePath">The path to the source file of the child.</param>
        /// <param name="sourceLineNumber">The line number of the child.</param>
        /// <returns>A reference to the initialized child (that needs to be disposed to stop the timing).</returns>
        ITrace StartChild(
            string name,
            TraceComponent component,
            TraceLevel level,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0);

        /// <summary>
        /// Adds a datum to the this trace instance.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="traceDatum">The datum itself.</param>
        void AddDatum(string key, ITraceDatum traceDatum);

        /// <summary>
        /// Adds a datum to the this trace instance.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="value">The datum itself.</param>
        void AddDatum(string key, object value);
    }
}
