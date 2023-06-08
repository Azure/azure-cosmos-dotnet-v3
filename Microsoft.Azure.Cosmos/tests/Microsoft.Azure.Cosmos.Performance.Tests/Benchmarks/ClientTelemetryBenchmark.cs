// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    /// <summary>
    /// Generates Benchmarks for different components of client telemetry.
    /// </summary>
    [MemoryDiagnoser]
    public class ClientTelemetryBenchmark
    {
        private readonly ITrace trace = null;
        private readonly CosmosDiagnostics diagnostics = null;

        public ClientTelemetryBenchmark()
        {
            this.trace = this.CreateTestTraceTree();
            this.diagnostics = new Diagnostics.CosmosTraceDiagnostics(this.CreateTestTraceTree());
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
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Trace.GetRootTrace(nameof(RegionContactedInDiagnosticsBenchmark)));
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
        [BenchmarkCategory("ClientTelemetry")]
        public void RecordOperationTelemetryTest()
        {
            ClientTelemetry telemetry = new ClientTelemetry();

            Console.WriteLine(this.diagnostics.ToString());
            telemetry.CollectOperationInfo(
                this.diagnostics, 
                System.Net.HttpStatusCode.OK, 
                10, 
                "ContainerId", 
                "DatabaseId", 
                Documents.OperationType.Read, 
                Documents.ResourceType.Document, 
                ConsistencyLevel.Session.ToString(), 10, 
                Documents.SubStatusCodes.Unknown, 
                this.trace);
        }


        [Benchmark]
        [BenchmarkCategory("ClientTelemetry")]
        public void RunningTelemetryJobTest()
        {
            ClientTelemetry.CreateAndStartBackgroundTelemetry("clientid", CosmosHttpClientCore.CreateHttpClientHandler(1, null, null), "userAgent", ConnectionMode.Direct, null, DiagnosticsHandlerHelper.Instance, new List<string>(), null);
            ClientTelemetry telemetry = new ClientTelemetry();

            Console.WriteLine(this.diagnostics.ToString());
            telemetry.CollectOperationInfo(
                this.diagnostics,
                System.Net.HttpStatusCode.OK,
                10,
                "ContainerId",
                "DatabaseId",
                Documents.OperationType.Read,
                Documents.ResourceType.Document,
                ConsistencyLevel.Session.ToString(), 10,
                Documents.SubStatusCodes.Unknown,
                this.trace);
        }

        [Benchmark]
        [BenchmarkCategory("ClientTelemetry")]
        public void ClientTelemetryProcessorTest()
        {
            ClientTelemetry.CreateAndStartBackgroundTelemetry("clientid", CosmosHttpClientCore.CreateHttpClientHandler(1, null, null), "userAgent", ConnectionMode.Direct, null, DiagnosticsHandlerHelper.Instance, new List<string>(), null);
            ClientTelemetry telemetry = new ClientTelemetry();

            Console.WriteLine(this.diagnostics.ToString());
            telemetry.CollectOperationInfo(
                this.diagnostics,
                System.Net.HttpStatusCode.OK,
                10,
                "ContainerId",
                "DatabaseId",
                Documents.OperationType.Read,
                Documents.ResourceType.Document,
                ConsistencyLevel.Session.ToString(), 10,
                Documents.SubStatusCodes.Unknown,
                this.trace);
        }

        [Benchmark]
        [BenchmarkCategory("ClientTelemetry")]
        public void HistogramRecordTest()
        {

        }

        [Benchmark]
        [BenchmarkCategory("ClientTelemetry")]
        public void HistogramRecordFetchTest()
        {

        }
    }
}
