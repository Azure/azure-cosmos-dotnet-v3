//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class RunSummary
    {
        public string pk { get; set; } 

        public string id { get; set; }
        public string Commit { get; set; }
        public string CommitDate { get; set; }
        public string CommitTime { get; set; }

        public string Remarks { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }

        public string WorkloadType { get; set; }
        public string BranchName { get; set; }
        public string AccountName { get; set; }
        public string Database { get; set; }
        public string Container { get; set; }
        public string ConsistencyLevel { get; set; }

        public int Concurrency { get; set; }
        public int TotalOps { get; set; }
        public int? MaxRequestsPerTcpConnection { get; set; }
        public int? MaxTcpConnectionsPerEndpoint { get; set; }
        [JsonProperty]
        public static string MachineName { get; set; } = Environment.MachineName;
        [JsonProperty]
        public static string OS { get; set; } = Environment.OSVersion.Platform.ToString();
        [JsonProperty]
        public static string OSVersion { get; set; } = Environment.OSVersion.VersionString;
        [JsonProperty]
        public static string RuntimeVersion { get; set; } = Environment.Version.ToString();
        [JsonProperty]
        public static int Cores { get; set; } = Environment.ProcessorCount;
        [JsonProperty]
        public static string Location { get; set; }
        [JsonProperty]
        public static JObject AzureVmInfo { get; set; }

        public double Top10PercentAverageRps { get; set; }
        public double Top20PercentAverageRps { get; set; }
        public double Top30PercentAverageRps { get; set; }
        public double Top40PercentAverageRps { get; set; }
        public double Top50PercentAverageRps { get; set; }
        public double Top60PercentAverageRps { get; set; }
        public double Top70PercentAverageRps { get; set; }
        public double Top80PercentAverageRps { get; set; }
        public double Top90PercentAverageRps { get; set; }
        public double Top95PercentAverageRps { get; set; }
        public double Top99PercentAverageRps { get; set; }

        public double? Top50PercentLatencyInMs { get; set; }
        public double? Top75PercentLatencyInMs { get; set; }
        public double? Top90PercentLatencyInMs { get; set; }
        public double? Top95PercentLatencyInMs { get; set; }
        public double? Top98PercentLatencyInMs { get; set; }
        public double? Top99PercentLatencyInMs { get; set; }
        public double? MaxLatencyInMs { get; set; }

        public double AverageRps { get; set; }

        public JArray Diagnostics { get; set; }
    }
}
