//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
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
    internal abstract class SystemUsageMonitorBase : IDisposable
    {
        protected static SystemUtilizationReaderBase systemUtilizationReader;

        public static SystemUsageMonitorBase Create(IReadOnlyList<CpuAndMemoryUsageRecorder> usageRecorders)
        {
            systemUtilizationReader = SystemUtilizationReaderBase.SingletonInstance;

            if (systemUtilizationReader is UnsupportedSystemUtilizationReader)
            {
                return new SystemUsageMonitorNoOps();
            }

            return new SystemUsageMonitor(usageRecorders);
        }
         
        public abstract void Start();
        public abstract void Stop();
        public abstract CpuAndMemoryUsageRecorder GetRecorder(string recorderKey);
        public abstract void Dispose();
        internal abstract int PollDelayInMs { get; }
        public abstract bool IsRunning();
        internal abstract bool TryGetBackgroundTaskException(out AggregateException aggregateException);
    }
}