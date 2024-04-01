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
        public RunSummary(
            BenchmarkConfig benchmarkConfig,
            int concurrency)
        {
            this.BenchmarkConfig = benchmarkConfig ?? throw new ArgumentNullException(nameof(benchmarkConfig));
            DateTime utcNow = DateTime.UtcNow;
            this.id = $"{utcNow:yyyy-MM-dd:HH-mm}-{benchmarkConfig.CommitId}";
            this.Date = utcNow.ToString("yyyy-MM-dd");
            this.Time = utcNow.ToString("HH-mm");
            this.Concurrency = concurrency;
        }

        public string pk => this.BenchmarkConfig.ResultsPartitionKeyValue;

        public string id { get; }
        public string Commit => this.BenchmarkConfig.CommitId;
        public string CommitDate => this.BenchmarkConfig.CommitDate;
        public string CommitTime => this.BenchmarkConfig.CommitTime;

        public string Remarks { get; set; }
        public string Date { get; }
        public string Time { get; }

        public BenchmarkConfig BenchmarkConfig { get; }
        public string WorkloadType => String.IsNullOrWhiteSpace(this.BenchmarkConfig.WorkloadName) ? this.BenchmarkConfig.WorkloadType : this.BenchmarkConfig.WorkloadName;
        public string BranchName => this.BenchmarkConfig.BranchName;
        public string AccountName => this.BenchmarkConfig.EndPoint;
        public string Database => this.BenchmarkConfig.Database;
        public string Container => this.BenchmarkConfig.Container;
        public string ConsistencyLevel { get; set; }

        public int Concurrency { get; }
        public int TotalOps => this.BenchmarkConfig.ItemCount;
        public int? MaxRequestsPerTcpConnection => this.BenchmarkConfig.MaxRequestsPerTcpConnection;
        public int? MaxTcpConnectionsPerEndpoint => this.BenchmarkConfig.MaxTcpConnectionsPerEndpoint;

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
