// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class TelemetryBenchmark
    {
        private readonly ConcurrentDictionary<string, OperationInfo> operationInfo = new ConcurrentDictionary<string, OperationInfo>();

        private readonly CosmosTraceDiagnostics diagnostics;
        public TelemetryBenchmark()
        {
            ITrace trace = NoOpTrace.Singleton;
            this.diagnostics = new Diagnostics.CosmosTraceDiagnostics(trace);
        }

        [Benchmark]
        public void CollectOriginalTest()
        {
            this.CollectOriginal(cosmosDiagnostics: this.diagnostics,
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
        public void CollectRegionContactedTest()
        {
            this.CollectRegionContacted(cosmosDiagnostics: this.diagnostics);
        }

        [Benchmark]
        public void CollectOperationAndHistogramTest()
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

        internal void CollectOriginal(CosmosDiagnostics cosmosDiagnostics,
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
    }
}