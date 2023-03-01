//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    internal sealed class ResolveFabricPerformanceActivity : PerformanceActivity
    {
        public ResolveFabricPerformanceActivity()
            : base(null, null, PerfCounters.Counters.FabricResolveServiceFailures, PerfCounters.Counters.FabricResolveServiceAverageLatency, PerfCounters.Counters.FabricResolveServiceAverageLatencyBase, "ResolveFabric")
        { }
    }

    internal sealed class OpenConnectionPerformanceActivity : PerformanceActivity
    {
        public OpenConnectionPerformanceActivity()
            : base(null, null, null, PerfCounters.Counters.BackendConnectionOpenAverageLatency, PerfCounters.Counters.BackendConnectionOpenAverageLatencyBase, "OpenConnection")
        { }
    }

    internal sealed class QueryRequestPerformanceActivity : PerformanceActivity
    {
        public QueryRequestPerformanceActivity()
            : base(PerfCounters.Counters.QueryRequestsPerSec, null, null, PerfCounters.Counters.AverageQueryRequestsDuration, PerfCounters.Counters.AverageQueryRequestsDurationBase, null)
        { }
    }

    internal sealed class ProcedureRequestPerformanceActivity : PerformanceActivity
    {
        public ProcedureRequestPerformanceActivity()
            : base(PerfCounters.Counters.ProcedureRequestsPerSec, null, null, PerfCounters.Counters.AverageProcedureRequestsDuration, PerfCounters.Counters.AverageProcedureRequestsDurationBase, null)
        { }
    }    
}
