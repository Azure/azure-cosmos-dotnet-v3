//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.ApplicationInsights;

    internal class ReadOperationMetricsCollector : MetricsCollector
    {
        public ReadOperationMetricsCollector(TelemetryClient telemetryClient) : base(telemetryClient)
        {
        }

        protected override string AverageRpsMetricName => "ReadOperationAverageRps";

        protected override string LatencyInMsMetricName => "ReadOperationLatencyInMs";

        protected override string FailureOperationMetricName => "ReadOperationFailure";

        protected override string SuccessOperationMetricName => "ReadOperationFailure";
    }
}
