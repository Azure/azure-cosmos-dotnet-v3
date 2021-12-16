// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using BenchmarkDotNet.Attributes;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class TelemetryBenchmark
    {
        private readonly ConcurrentDictionary<string, OperationInfo> operationInfo;
        private readonly CosmosTraceDiagnostics noOpTracediagnostics;
        private readonly CosmosTraceDiagnostics diagnosticsWithData;
        private readonly ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap;

        public TelemetryBenchmark()
        {
            ITrace trace = NoOpTrace.Singleton;
            this.noOpTracediagnostics = new Diagnostics.CosmosTraceDiagnostics(trace);
            this.diagnosticsWithData = new Diagnostics.CosmosTraceDiagnostics(this.CreateTestTraceTree());
            this.operationInfo = new ConcurrentDictionary<string, OperationInfo>();
            this.operationInfoMap = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();
        }

        private ITrace CreateTestTraceTree()
        {
            ITrace trace;
            using (trace = Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, TraceLevel.Info))
            {
                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    using (ITrace secondLevel = trace.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                    {
                        using (ITrace thirdLevel = trace.StartChild("Third level Node", TraceComponent.Unknown, TraceLevel.Info))
                        {
                            thirdLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralUS));
                        }
                    }

                    using (ITrace secondLevel = trace.StartChild("Second level Node", TraceComponent.Unknown, TraceLevel.Info))
                    {
                        secondLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.CentralIndia, Regions.EastUS2));
                    }
                }

                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, TraceLevel.Info))
                {
                    firstLevel.AddDatum("Client Side Request Stats", this.GetDatumObject(Regions.FranceCentral));
                }
            }

            return trace;
        }

        private TraceDatum GetDatumObject(string regionName1, string regionName2 = null)
        {
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);
            Uri uri1 = new Uri("http://someUri1.com");
            datum.RegionsContacted.Add((regionName1, uri1));
            if (regionName2 != null)
            {
                Uri uri2 = new Uri("http://someUri2.com");
                datum.RegionsContacted.Add((regionName2, uri2));
            }

            return datum;
        }

        /* [Benchmark]
         public void CollectMasterTest()
         {
             this.CollectMaster(cosmosDiagnostics: this.diagnostics,
                 statusCode: HttpStatusCode.OK,
                 responseSizeInBytes: 1000,
                 containerId: "containerid",
                 databaseId: "databaseid",
                 operationType: OperationType.Read,
                 resourceType: ResourceType.Document,
                 consistencyLevel: "eventual",
                 requestCharge: 10d);
         }

         [Benchmark]
         public void CollectMasterOperationAndHistogramTest()
         {
             this.CollectMasterOperationAndHistogram(cosmosDiagnostics: this.diagnostics,
                 statusCode: HttpStatusCode.OK,
                 responseSizeInBytes: 1000,
                 containerId: "containerid",
                 databaseId: "databaseid",
                 operationType: OperationType.Read,
                 resourceType: ResourceType.Document,
                 consistencyLevel: "eventual",
                 requestCharge: 10d);
         }*/

        /*     [Benchmark]
             public void CollectMasterOperationAndHistogramFor1000RequestsTest()
             {
                 for (int i = 0; i < 1000; i++)
                 {
                     this.CollectMasterOperationAndHistogram(cosmosDiagnostics: this.diagnostics,
                         statusCode: HttpStatusCode.OK,
                         responseSizeInBytes: 10 * i,
                         containerId: "containerid",
                         databaseId: "databaseid",
                         operationType: OperationType.Read,
                         resourceType: ResourceType.Document,
                         consistencyLevel: "eventual",
                         requestCharge: 10d);
                 }

             }*/

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

        internal void CollectMaster(CosmosDiagnostics cosmosDiagnostics,
                            HttpStatusCode statusCode,
                            long responseSizeInBytes,
                            string containerId,
                            string databaseId,
                            OperationType operationType,
                            ResourceType resourceType,
                            string consistencyLevel,
                            double requestCharge)
        {
            if (cosmosDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnostics));
            }

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(cosmosDiagnostics);

            // Recording Request Latency and Request Charge
            OperationInfo payloadKey = new OperationInfo(regionsContacted: regionsContacted?.ToString(),
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel,
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode);

            (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge) = this.operationInfoMap
                    .GetOrAdd(payloadKey, x => (latency: new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                        ClientTelemetryOptions.RequestLatencyMax,
                                                        ClientTelemetryOptions.RequestLatencyPrecision),
                            requestcharge: new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                        ClientTelemetryOptions.RequestChargeMax,
                                                        ClientTelemetryOptions.RequestChargePrecision)));
            try
            {
                latency.RecordValue(cosmosDiagnostics.GetClientElapsedTime().Ticks);
            }
            catch (Exception ex)
            {
                //  DefaultTrace.TraceError("Latency Recording Failed by Telemetry. Exception : {0}", ex.Message);
            }

            long requestChargeToRecord = (long)(requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                requestcharge.RecordValue(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                //  DefaultTrace.TraceError("Request Charge Recording Failed by Telemetry. Request Charge Value : {0}  Exception : {1} ", requestChargeToRecord, ex.Message);
            }
        }

        internal void CollectMasterOperationAndHistogram(CosmosDiagnostics cosmosDiagnostics,
                          HttpStatusCode statusCode,
                          long responseSizeInBytes,
                          string containerId,
                          string databaseId,
                          OperationType operationType,
                          ResourceType resourceType,
                          string consistencyLevel,
                          double requestCharge)
        {
            // Recording Request Latency and Request Charge
            OperationInfo payloadKey = new OperationInfo(regionsContacted: "region",
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel,
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode);

            (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge) = this.operationInfoMap
                    .GetOrAdd(payloadKey, x => (latency: new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                        ClientTelemetryOptions.RequestLatencyMax,
                                                        ClientTelemetryOptions.RequestLatencyPrecision),
                            requestcharge: new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                        ClientTelemetryOptions.RequestChargeMax,
                                                        ClientTelemetryOptions.RequestChargePrecision)));
            try
            {
                latency.RecordValue(cosmosDiagnostics.GetClientElapsedTime().Ticks);
            }
            catch (Exception ex)
            {
                //DefaultTrace.TraceError("Latency Recording Failed by Telemetry. Exception : {0}", ex.Message);
            }

            long requestChargeToRecord = (long)(requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                requestcharge.RecordValue(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                //DefaultTrace.TraceError("Request Charge Recording Failed by Telemetry. Request Charge Value : {0}  Exception : {1} ", requestChargeToRecord, ex.Message);
            }
        }
    }
}