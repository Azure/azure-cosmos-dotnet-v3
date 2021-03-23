//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;

    internal class ClientTelemetryInfo
    {
        public string TimeStamp { get; set; }
        public string ClientId { get; }
        public string ProcessId { get; }
        public string UserAgent { get; }
        public ConnectionMode ConnectionMode { get; }
        public string GlobalDatabaseAccountName { get; }
        public string ApplicationRegion { get; set; }
        public string HostEnvInfo { get; set; }
        public bool? AcceleratedNetworking { get; }
        public IDictionary<ReportPayload, LongConcurrentHistogram> SystemInfoMap { get; set; }
        public IDictionary<ReportPayload, LongConcurrentHistogram> CacheRefreshInfoMap { get; set; }
        public IDictionary<ReportPayload, LongConcurrentHistogram> OperationInfoMap { get; set; }
        public ClientTelemetryInfo(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode,
                                   string globalDatabaseAccountName,
                                   bool? acceleratedNetworking)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode;
            this.GlobalDatabaseAccountName = globalDatabaseAccountName;
            this.AcceleratedNetworking = acceleratedNetworking;
            this.SystemInfoMap = new Dictionary<ReportPayload, LongConcurrentHistogram>();
            this.CacheRefreshInfoMap = new Dictionary<ReportPayload, LongConcurrentHistogram>();
            this.OperationInfoMap = new Dictionary<ReportPayload, LongConcurrentHistogram>();
        }

    }
}
