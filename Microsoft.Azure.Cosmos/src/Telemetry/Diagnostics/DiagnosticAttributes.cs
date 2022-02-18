//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Cosmos.Diagnostics;
    using Documents;

#if INTERNAL
    public
#else
    internal
#endif 
        class DiagnosticAttributes
    {
        public bool Error { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string DbSystem => "cosmosdb";
        public Uri AccountName { get; set; }
        public string UserAgent { get; set; }
        public string DbName { get; set; }
        public OperationType DbOperation { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; }
        public string ContainerName { get; set; }
        public double RequestCharge { get; set; }
        public CosmosTraceDiagnostics RequestDiagnostics { get; set; }

        public bool IsRequestChargeHigh(double thresholdRequestCharge)
        {
            return this.RequestCharge > thresholdRequestCharge;
        }

        public bool IsLatencyHigh(TimeSpan thresholdLatency)
        {
            return this.RequestDiagnostics.GetClientElapsedTime() > thresholdLatency;
        }

        public bool IsSuccessHttpStatusCode()
        {
            return this.HttpStatusCode.IsSuccess();
        }
    }
}
