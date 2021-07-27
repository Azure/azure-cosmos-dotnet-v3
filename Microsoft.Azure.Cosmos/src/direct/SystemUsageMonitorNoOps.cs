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

    internal sealed class SystemUsageMonitorNoOps : SystemUsageMonitorBase
    {
        internal override int PollDelayInMs => -1;

        public override bool IsRunning() => false;

        public override void Dispose()
        {
            DefaultTrace.TraceInformation(nameof(SystemUsageMonitorNoOps) + " Disposed is called");
        }

        public override CpuAndMemoryUsageRecorder GetRecorder(string recorderKey)
        {
            return null;
        }

        public override void Start()
        {
            DefaultTrace.TraceInformation(nameof(SystemUsageMonitorNoOps) + " Start is called");
        }

        public override void Stop()
        {
            DefaultTrace.TraceInformation(nameof(SystemUsageMonitorNoOps) + " Stop is called");
        }

        internal override bool TryGetBackgroundTaskException(out AggregateException aggregateException)
        {
            aggregateException = null;
            return false;
        }
    }
}