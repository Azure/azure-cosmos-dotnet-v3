// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;

    internal class DiagnosticsFilter : IDiagnosticsFilter
    {
        private readonly DiagnosticAttributes diagnosticAttributes;

        private readonly int latencyThresholdInMs = 100;
        private readonly int requestChargeThresholdInRu = 100;

        public DiagnosticsFilter(DiagnosticAttributes diagnosticAttributes)
        {
            this.diagnosticAttributes = diagnosticAttributes;
        }

        /// <summary>
        /// Allow only when either of below is True
        /// 1) Latency is not more than 100 ms
        /// 2) Request Charge is more than 100 RUs
        /// 3) HTTP status code is not Success
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsAllowed()
        {
            return this.diagnosticAttributes.IsLatencyHigh(TimeSpan.FromMilliseconds(this.latencyThresholdInMs)) || 
                   this.diagnosticAttributes.IsRequestChargeHigh(this.requestChargeThresholdInRu) ||
                   !this.diagnosticAttributes.IsSuccessHttpStatusCode();
        }
    }
}
