//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Concurrent;
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
        public string GlobalDatabaseAccountName { get; set;  }
        public string ApplicationRegion { get; set; }
        public string HostEnvInfo { get; set; }
        public bool? AcceleratedNetworking { get; }

        public ConcurrentDictionary<ReportPayload, LongConcurrentHistogram> SystemInfoMap { get; set; }
        public ConcurrentDictionary<ReportPayload, LongConcurrentHistogram> CacheRefreshInfoMap { get; set; }
        public ConcurrentDictionary<ReportPayload, LongConcurrentHistogram> OperationInfoMap { get; set; }
        public ClientTelemetryInfo(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode,
                                   bool? acceleratedNetworking)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode;
            this.AcceleratedNetworking = acceleratedNetworking;
            this.SystemInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
            this.CacheRefreshInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
            this.OperationInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
        }

    }
}
