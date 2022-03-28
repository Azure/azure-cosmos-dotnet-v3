//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Cosmos.Diagnostics;
    using Documents;

    internal class DiagnosticAttributes
    {
        public string DbSystem => "cosmosdb";

        public string DbName { get; set; }
        public string DbOperation { get; set; }

        public Uri AccountName { get; set; }
        public string ContainerName { get; set; }

        public List<string> PartitionId { get; set; }

        public HttpStatusCode? HttpStatusCode { get; set; }

        public string UserAgent { get; set; }
        public long? RequestSize { get; set; }
        public long? ResponseSize { get; set; }

        public string Region { get; set; }
        public int RetryCount { get; set; }

        public ConnectionMode ConnectionMode { get; set; }

        public List<(HttpStatusCode, int)> BackendStatusCode { get; set; }

        public int ItemCount { get; set; }

        public bool IsError { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string ExceptionMessage { get; set; }

        public double? RequestCharge { get; set; }

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
