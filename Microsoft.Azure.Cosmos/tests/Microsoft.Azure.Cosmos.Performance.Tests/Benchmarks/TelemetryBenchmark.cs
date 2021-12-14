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
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class TelemetryBenchmark
    {
        private readonly ConcurrentDictionary<string, OperationInfo> operationInfo;
        private readonly CosmosTraceDiagnostics diagnostics;
        private readonly ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap;

        public TelemetryBenchmark()
        {
            ITrace trace = NoOpTrace.Singleton;
            this.diagnostics = new Diagnostics.CosmosTraceDiagnostics(trace);
            this.operationInfo = new ConcurrentDictionary<string, OperationInfo>();
            this.operationInfoMap = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();
        }

        [Benchmark]
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
        }

        [Benchmark]
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

        }

        [Benchmark]
        public void CollectRegionContactedTest()
        {
            this.CollectRegionContacted(cosmosDiagnostics: this.diagnostics);
        }

        [Benchmark]
        public void CollectFullWithDictionaryStringKeyTest()
        {
            this.CollectFullWithDictionaryStringKey(cosmosDiagnostics: this.diagnostics,
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
        public void CollectOperationAndHistogramWithDictionaryStringKeyTest()
        {
            this.CollectOperationAndHistogram(cosmosDiagnostics: this.diagnostics,
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
        public void CollectOperationAndHistogramWithDictionaryStringFor1000RequestsTest()
        {
            for(int i = 0; i< 1000; i++)
            {
                this.CollectOperationAndHistogram(cosmosDiagnostics: this.diagnostics,
                    statusCode: HttpStatusCode.OK,
                    responseSizeInBytes: 10 * i,
                    containerId: "containerid",
                    databaseId: "databaseid",
                    operationType: OperationType.Read,
                    resourceType: ResourceType.Document,
                    consistencyLevel: "eventual",
                    requestCharge: 10d);
            }

        }

        internal void CollectOperationAndHistogram(CosmosDiagnostics cosmosDiagnostics,
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
            OperationInfo payloadKey = new OperationInfo(regionsContacted: "Region",
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel,
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode);

            payloadKey = this.operationInfo.GetOrAdd(payloadKey.Key, payloadKey);

            try
            {
                payloadKey.latency.RecordValue(cosmosDiagnostics.GetClientElapsedTime().Ticks);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Latency Recording Failed by Telemetry. Exception : {0}", ex.Message);
            }

            long requestChargeToRecord = (long)(requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                payloadKey.requestcharge.RecordValue(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request Charge Recording Failed by Telemetry. Request Charge Value : {0}  Exception : {1} ", requestChargeToRecord, ex.Message);
            }

        }

        internal void CollectRegionContacted(CosmosDiagnostics cosmosDiagnostics)
        {
            if (cosmosDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnostics));
            }

             ClientTelemetryHelper.GetContactedRegions(cosmosDiagnostics);

        }

        internal void CollectFullWithDictionaryStringKey(CosmosDiagnostics cosmosDiagnostics,
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

            payloadKey = this.operationInfo.GetOrAdd(payloadKey.Key, payloadKey);

            try
            {
                payloadKey.latency.RecordValue(cosmosDiagnostics.GetClientElapsedTime().Ticks);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Latency Recording Failed by Telemetry. Exception : {0}", ex.Message);
            }

            long requestChargeToRecord = (long)(requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                payloadKey.requestcharge.RecordValue(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request Charge Recording Failed by Telemetry. Request Charge Value : {0}  Exception : {1} ", requestChargeToRecord, ex.Message);
            }

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