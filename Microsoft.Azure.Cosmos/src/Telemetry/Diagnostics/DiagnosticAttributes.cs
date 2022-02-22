//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Cosmos.Diagnostics;
    using Documents;

    internal class DiagnosticAttributes
    {
        public bool Error { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string DbSystem => "cosmosdb";
        public Uri AccountName { get; set; }
        public string UserAgent { get; set; }
        public string DbName { get; set; }
        public string DbOperation { get; set; }
        public HttpStatusCode? HttpStatusCode { get; set; }
        public string ContainerName { get; set; }
        public double? RequestCharge { get; set; }
        public string QueryText { get; set; }

        public ConnectionMode ConnectionMode { get; set; }
        public CosmosDiagnostics RequestDiagnostics { get; set; }

        public bool IsRequestChargeHigh(double thresholdRequestCharge)
        {
            return this.RequestCharge > thresholdRequestCharge;
        }

        public bool IsLatencyHigh(TimeSpan thresholdLatency)
        {
            return this.RequestDiagnostics != null && 
                this.RequestDiagnostics.GetClientElapsedTime() > thresholdLatency;
        }

        public bool IsSuccessHttpStatusCode()
        {
            return this.HttpStatusCode.HasValue && this.HttpStatusCode.Value.IsSuccess();
        }
    }
}
