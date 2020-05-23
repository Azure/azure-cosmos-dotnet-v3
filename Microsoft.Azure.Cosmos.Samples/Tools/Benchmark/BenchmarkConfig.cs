//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using CommandLine;

    public class BenchmarkConfig
    {
        [Option('e', Required = true, HelpText = "Cosmos account end point")]
        public string EndPoint { get; set; }

        [Option('k', Required = true, HelpText = "Cosmos account master key")]
        public string Key { get; set; }

        [Option(Required = false, HelpText = "Database to use")]
        public string Database { get; set; } = "db";

        [Option(Required = false, HelpText = "Collection to use")]
        public string Container { get; set; } = "data";

        [Option('t', Required = false, HelpText = "Collection throughput use")]
        public int Throughput { get; set; } = 100000;

        [Option('n', Required = false, HelpText = "Number of documents to insert")]
        public int ItemCount { get; set; } = 200000;

        [Option(Required = false, HelpText = "Start with new collection")]
        public bool CleanupOnStart { get; set; } = false;

        [Option(Required = false, HelpText = "Clean-up after run")]
        public bool CleanupOnFinish { get; set; } = false;

        [Option(Required = false, HelpText = "Container partition key path")]
        public string PartitionKeyPath { get; set; } = "/partitionKey";

        [Option("pl", Required = false, HelpText = "Degree of parallism")]
        public int DegreeOfParallelism { get; set; } = -1;

        [Option(Required = false, HelpText = "Item template")]
        public string ItemTemplateFile { get; set; } = "Player.json";

        [Option(Required = false, HelpText = "Min thread pool size")]
        public int MinThreadPoolSize { get; set; } = 100;
    }
}
