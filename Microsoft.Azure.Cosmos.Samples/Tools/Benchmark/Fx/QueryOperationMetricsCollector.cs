//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.ApplicationInsights;

    internal class QueryOperationMetricsCollector : MetricsCollector
    {
        public QueryOperationMetricsCollector(TelemetryClient telemetryClient) : base(telemetryClient)
        {
        }

        protected override string AverageRpsMetricName => "QueryOperationAverageRps";

        protected override string LatencyInMsMetricName => "QueryOperationLatencyInMs";

        protected override string FailureOperationMetricName => "QueryOperationFailure";

        protected override string SuccessOperationMetricName => "QueryOperationSuccess";
    }
}
