//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    class RunSummary
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

        public string MachineName { get; set; } = Environment.MachineName;
        public string OS { get; set; } = Environment.OSVersion.Platform.ToString();
        public string OSVersion { get; set; } = Environment.OSVersion.VersionString;
        public string RuntimeVersion { get; set; } = Environment.Version.ToString();
        public int Cores { get; set; } = Environment.ProcessorCount;

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
        public double AverageRps { get; set; }
    }
}
