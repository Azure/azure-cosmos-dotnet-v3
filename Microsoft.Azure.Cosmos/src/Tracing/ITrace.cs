// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    /// <summary>
    /// Interface to represent a single node in a trace tree.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        abstract class ITrace : IDisposable
    {
        /// <summary>
        /// Gets the name of the node.
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// Gets the ID of the node.
        /// </summary>
        internal Guid Id { get; set; }

        /// <summary>
        /// Gets the information for what line of source code this trace was called on.
        /// </summary>
        internal CallerInfo CallerInfo { get; set; }

        /// <summary>
        /// Gets the time when the trace was started.
        /// </summary>
        internal DateTime StartTime { get; set; }

        /// <summary>
        /// Gets the duration of the trace.
        /// </summary>
        internal TimeSpan Duration { get; }

        /// <summary>
        /// Gets the level (of information) of the trace.
        /// </summary>
        internal TraceLevel Level { get; set; }

        /// <summary>
        /// Gets the component that governs this trace.
        /// </summary>
        internal TraceComponent Component { get; set; }

        /// <summary>
        /// Gets the parent node of this trace.
        /// </summary>
        internal ITrace Parent { get; set; }

        /// <summary>
        /// Gets the children of this trace.
        /// </summary>
        internal IReadOnlyList<ITrace> Children { get; }

        internal ISet<(string, Uri)> RegionsContactedTemporary { get; set; }

        /// <summary>
        /// Consolidated Region contacted Information of this and children nodes
        /// </summary>
        internal ISet<(string, Uri)> RegionsContacted
        {
            get => this.RegionsContactedTemporary;
            set
            {
                if (this.RegionsContactedTemporary == null)
                {
                    this.RegionsContactedTemporary = value;
                } 
                else
                {
                    this.RegionsContactedTemporary.UnionWith(value);
                }
                
                if (this.Parent != null)
                {
                    this.Parent.RegionsContacted = value;
                }
            }
        }

        /// <summary>
        /// Gets additional datum associated with this trace.
        /// </summary>
        internal IReadOnlyDictionary<string, object> Data { get; }

        /// <summary>
        /// Starts a Trace and adds it as a child to this instance.
        /// </summary>
        /// <param name="name">The name of the child.</param>
        /// <param name="memberName">The member name of the child.</param>
        /// <param name="sourceFilePath">The path to the source file of the child.</param>
        /// <param name="sourceLineNumber">The line number of the child.</param>
        /// <returns>A reference to the initialized child (that needs to be disposed to stop the timing).</returns>
        internal abstract ITrace StartChild(
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
        internal abstract ITrace StartChild(
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
        internal abstract void AddDatum(string key, TraceDatum traceDatum);

        /// <summary>
        /// Adds a datum to the this trace instance.
        /// </summary>
        /// <param name="key">The key to associate the datum.</param>
        /// <param name="value">The datum itself.</param>
        internal abstract void AddDatum(string key, object value);

        /// <summary>
        /// Adds a trace children that is already completed.
        /// </summary>
        /// <param name="trace">Existing trace.</param>
        internal abstract void AddChild(ITrace trace);

        /// <summary>
        /// Update region contacted information to the parent Itrace
        /// </summary>
        /// <param name="traceDatum"></param>
        internal void UpdateRegionContacted(TraceDatum traceDatum)
        {
            if (traceDatum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                if (clientSideRequestStatisticsTraceDatum.RegionsContacted == null || clientSideRequestStatisticsTraceDatum.RegionsContacted.Count == 0)
                {
                    return;
                }
                this.RegionsContacted = clientSideRequestStatisticsTraceDatum.RegionsContacted;
            }
        }

        public abstract void Dispose();
    }
}
