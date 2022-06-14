// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    [MemoryDiagnoser]
    public class RegionContactedInDiagnosticsBenchmark
    {
        private readonly CosmosTraceDiagnostics noOpTracediagnostics;
        private readonly CosmosTraceDiagnostics diagnosticsWithData;

        public RegionContactedInDiagnosticsBenchmark()
        {
            ITrace trace = NoOpTrace.Singleton;
            this.noOpTracediagnostics = new Diagnostics.CosmosTraceDiagnostics(trace);
            this.diagnosticsWithData = new Diagnostics.CosmosTraceDiagnostics(this.CreateTestTraceTree());
        }

        private ITrace CreateTestTraceTree()
        {
            ITrace trace;
            using (trace = Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, TraceLevel.Info))
            using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
            using (ITrace secondLevel = firstLevel.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
            using (ITrace thirdLevel = secondLevel.StartChild("Third level Node", TraceComponent.Unknown, TraceLevel.Info))
                thirdLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralUS));
            using (ITrace secondLevel = trace.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                secondLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralIndia, Regions.EastUS2));
            using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                firstLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.FranceCentral));

            return trace;
        }

        private TraceDatum GetDatumObject(string regionName1, string regionName2 = null)
        {
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, new TraceSummary());
            Uri uri1 = new Uri("http://someUri1.com");
            datum.RegionsContacted.Add((regionName1, uri1));
            if (regionName2 != null)
            {
                Uri uri2 = new Uri("http://someUri2.com");
                datum.RegionsContacted.Add((regionName2, uri2));
            }

            return datum;
        }

        [Benchmark]
        public void CollectRegionContactedWithMultiLevelTraceTest()
        {
            this.CollectRegionContacted(cosmosDiagnostics: this.diagnosticsWithData);
        }

        [Benchmark]
        public void CollectRegionContactedWithNoOpsTraceTest()
        {
            this.CollectRegionContacted(cosmosDiagnostics: this.noOpTracediagnostics);
        }

        internal void CollectRegionContacted(CosmosDiagnostics cosmosDiagnostics)
        {
            if (cosmosDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnostics));
            }

            ClientTelemetryHelper.GetContactedRegions(cosmosDiagnostics);
        }
    }
}
