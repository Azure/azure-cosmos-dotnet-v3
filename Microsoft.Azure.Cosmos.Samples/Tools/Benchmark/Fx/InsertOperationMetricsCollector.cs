//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.ApplicationInsights;

    internal class InsertOperationMetricsCollector : MetricsCollector
    {
        public InsertOperationMetricsCollector(TelemetryClient telemetryClient) : base(telemetryClient)
        {
        }

        protected override string AverageRpsMetricName => "InsertOperationAverageRps";

        protected override string LatencyInMsMetricName => "InsertOperationLatencyInMs";

        protected override string FailureOperationMetricName => "InsertOperationFailure";

        protected override string SuccessOperationMetricName => "InsertOperationSuccess";
    }
}
