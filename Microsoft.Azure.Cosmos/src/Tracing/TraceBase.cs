// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal abstract class TraceBase : ITrace
    {
        private ISet<(string, Uri)> RegionsContactedTemporary { get; set; }

        /// <summary>
        /// Consolidated Region contacted Information of this and children nodes
        /// </summary>
        public ISet<(string, Uri)> RegionsContacted
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

        public abstract string Name { get; }

        public abstract Guid Id { get; }

        public abstract CallerInfo CallerInfo { get; }

        public abstract DateTime StartTime { get; }

        public abstract TimeSpan Duration { get; }

        public abstract TraceLevel Level { get; }

        public abstract TraceComponent Component { get; }

        public abstract ITrace Parent { get; }

        public abstract IReadOnlyList<ITrace> Children { get; }

        public abstract IReadOnlyDictionary<string, object> Data { get; }

        /// <summary>
        /// Update region contacted information to the parent Itrace
        /// </summary>
        /// <param name="traceDatum"></param>
        public void UpdateRegionContacted(TraceDatum traceDatum)
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

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        public abstract void Dispose();

        public abstract ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

        public abstract ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

        public abstract void AddDatum(string key, TraceDatum traceDatum);

        public abstract void AddDatum(string key, object value);

        public abstract void AddChild(ITrace trace);
    }
}
