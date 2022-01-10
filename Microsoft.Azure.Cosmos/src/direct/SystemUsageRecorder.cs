//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Documents.Rntbd.SystemUsageMonitor;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal sealed class SystemUsageRecorder
    {
        internal readonly string identifier;
        private readonly int historyLength;
        internal readonly TimeSpan refreshInterval;

        private readonly Queue<SystemUsageLoad> historyQueue;

        private TimeSpan nextTimeStamp;

        public SystemUsageHistory Data { private set; get; }

        internal SystemUsageRecorder(string identifier, 
            int historyLength, 
            TimeSpan refreshInterval)
        {
            this.identifier = String.IsNullOrEmpty(identifier) ? throw new ArgumentException("Identifier can not be null.") : identifier;
            this.historyLength = (historyLength <= 0) ? throw new ArgumentOutOfRangeException("historyLength can not be less than or equal to zero.") : historyLength;
            this.refreshInterval = (refreshInterval <= TimeSpan.Zero) ? throw new ArgumentException("refreshInterval timespan can not be zero.") : refreshInterval;

            this.historyQueue = new Queue<SystemUsageLoad>(this.historyLength);
        }

        /// <summary>
        /// Record System usage (i.e. CPU, Memory, Thread Availability) 
        /// for UnSupported Reader CPU usage is NaN and Memory Usage will be null.
        /// Thread Information will be calculated for both kind of readers.
        /// Although for netstandard 1.5 and 1.6 It will just give Thread Starvation information and for other frameworks/environments it gives Available Thread/Minimum Thread/Maximum Thread
        /// </summary>
        /// <param name="systemUsageLoad"></param>
        /// <param name="watch"></param>
        internal void RecordUsage(SystemUsageLoad systemUsageLoad, Stopwatch watch)
        {
            this.nextTimeStamp = watch.Elapsed.Add(refreshInterval);
            this.Data = new SystemUsageHistory(this.Collect(systemUsageLoad), this.refreshInterval);
        }

        private ReadOnlyCollection<SystemUsageLoad> Collect(SystemUsageLoad loadData)
        {
            if(historyQueue.Count == historyLength)
            {
                historyQueue.Dequeue();
            }
            historyQueue.Enqueue(loadData);

            return new ReadOnlyCollection<SystemUsageLoad>(historyQueue.ToList<SystemUsageLoad>());
        }

        internal bool IsEligibleForRecording(Stopwatch watch) => TimeSpan.Compare(watch.Elapsed, nextTimeStamp) >= 0;
    }
}