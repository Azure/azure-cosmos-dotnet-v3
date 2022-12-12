// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.ComponentModel;
    using System.Security.AccessControl;
    using System;
    using BenchmarkDotNet.Attributes;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using System.Collections.Concurrent;

    [MemoryDiagnoser]
    public class ClientTelemetryReadCollectorsBenchmark
    {
        private ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap
           = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();

        public ClientTelemetryReadCollectorsBenchmark()
        {
            for(int count = 0; count < 10000; count++)
            {
                OperationInfo payloadKey = new OperationInfo(regionsContacted: "region1, region2",
                                                          responseSizeInBytes: 29,
                                                          consistency: "Session",
                                                          databaseName: "databaseName" + count,
                                                          containerName: "containerName",
                                                          operation: Documents.OperationType.Create,
                                                          resource: Documents.ResourceType.Document,
                                                          statusCode: 200,
                                                          subStatusCode: 0);

                (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge) = this.operationInfoMap
                        .GetOrAdd(payloadKey, x => (latency: new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                            ClientTelemetryOptions.RequestLatencyMax,
                                                            ClientTelemetryOptions.RequestLatencyPrecision),
                                requestcharge: new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                            ClientTelemetryOptions.RequestChargeMax,
                                                            ClientTelemetryOptions.RequestChargePrecision)));
                try
                {
                    latency.RecordValue(TimeSpan.FromSeconds(3).Ticks);
                }
                catch { throw; }

                long requestChargeToRecord = (long)(2 * ClientTelemetryOptions.HistogramPrecisionFactor);
                try
                {
                    requestcharge.RecordValue(requestChargeToRecord);
                }
                catch { throw; }
            }
        }
        
        [Benchmark]
        [BenchmarkCategory("ClientTelemetry")]
        public void ReadOperationCollector()
        {
            ClientTelemetryHelper.ToListWithMetricsInfo(this.operationInfoMap);
        }
    }
}
