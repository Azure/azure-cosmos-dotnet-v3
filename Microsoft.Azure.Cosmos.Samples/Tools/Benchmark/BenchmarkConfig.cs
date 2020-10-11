//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime;
    using CommandLine;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Client;

    public class BenchmarkConfig
    {
        private static readonly string UserAgentSuffix = "cosmosdbdotnetbenchmark";

        [Option('w', Required = true, HelpText = "Workload type insert, read")]
        public string WorkloadType { get; set; }

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

        [Option(Required = false, HelpText = "Client consistency level to override")]
        public string ConsistencyLevel { get; set; }

        [Option(Required = false, HelpText = "Enable latency percentiles")]
        public bool EnableLatencyPercentiles { get; set; }

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

        [Option(Required = false, HelpText = "Write the task execution failure to console. Useful for debugging failures")]
        public bool TraceFailures { get; set; }

        [Option(Required = false, HelpText = "Publish run results")]
        public bool PublishResults  { get; set; }

        [Option(Required = false, HelpText = "Run ID, only for publish")]
        internal string RunId { get; set; }

        [Option(Required = false, HelpText = "Commit ID, only for publish")]
        public string CommitId { get; set; }

        [Option(Required = false, HelpText = "Commit date, only for publish")]
        public string CommitDate { get; set; }

        [Option(Required = false, HelpText = "Commit time, only for publish")]
        public string CommitTime { get; set; }

        [Option(Required = false, HelpText = "Branch name, only for publish")]
        public string BranchName { get; set; }

        [Option(Required = false, HelpText = "Partitionkey, only for publish")]
        public string ResultsPartitionKeyValue { get; set; }

        [Option(Required = false, HelpText = "Disable core SDK logging")]
        public bool DisableCoreSdkLogging { get; set; }

        [Option(Required = false, HelpText = "Container to publish results to")]
        internal string ResultsContainer { get; set; } = "runsummary";

        internal int GetTaskCount(int containerThroughput)
        {
            int taskCount = this.DegreeOfParallelism;
            if (taskCount == -1)
            {
                // set TaskCount = 10 for each 10k RUs, minimum 1, maximum { #processor * 50 }
                taskCount = Math.Max(containerThroughput / 1000, 1);
                taskCount = Math.Min(taskCount, Environment.ProcessorCount * 50);
            }

            return taskCount;
        }

        internal void Print()
        {
            using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Green))
            {
                Utility.TeeTraceInformation($"{nameof(BenchmarkConfig)} arguments");
                Utility.TeeTraceInformation($"IsServerGC: {GCSettings.IsServerGC}");
                Utility.TeeTraceInformation("--------------------------------------------------------------------- ");
                Utility.TeeTraceInformation(JsonHelper.ToString(this));
                Utility.TeeTraceInformation("--------------------------------------------------------------------- ");
                Utility.TeeTraceInformation(string.Empty);
            }
        }

        internal static BenchmarkConfig From(string[] args)
        {
            BenchmarkConfig options = null;
            Parser.Default.ParseArguments<BenchmarkConfig>(args)
                .WithParsed<BenchmarkConfig>(e => options = e)
                .WithNotParsed<BenchmarkConfig>(e => BenchmarkConfig.HandleParseError(e));

            if (options.PublishResults)
            {
                if (string.IsNullOrEmpty(options.ResultsContainer)
                    || string.IsNullOrWhiteSpace(options.ResultsPartitionKeyValue)
                    || string.IsNullOrWhiteSpace(options.CommitId)
                    || string.IsNullOrWhiteSpace(options.CommitDate)
                    || string.IsNullOrWhiteSpace(options.CommitTime))
                {
                    throw new ArgumentException($"Missing either {nameof(options.ResultsContainer)} {nameof(options.ResultsPartitionKeyValue)} {nameof(options.CommitId)} {nameof(options.CommitDate)} {nameof(options.CommitTime)}");
                }
            }

            return options;
        }

        internal CosmosClient CreateCosmosClient(string accountKey)
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ApplicationName = BenchmarkConfig.UserAgentSuffix,
                MaxRetryAttemptsOnRateLimitedRequests = 0
            };

            if (!string.IsNullOrWhiteSpace(this.ConsistencyLevel))
            {
                clientOptions.ConsistencyLevel = (Microsoft.Azure.Cosmos.ConsistencyLevel)Enum.Parse(typeof(Microsoft.Azure.Cosmos.ConsistencyLevel), this.ConsistencyLevel, ignoreCase: true);
            }

            return new CosmosClient(
                        this.EndPoint,
                        accountKey,
                        clientOptions);
        }

        internal DocumentClient CreateDocumentClient(string accountKey)
        {
            Microsoft.Azure.Documents.ConsistencyLevel? consistencyLevel = null;
            if (!string.IsNullOrWhiteSpace(this.ConsistencyLevel))
            {
                consistencyLevel = (Microsoft.Azure.Documents.ConsistencyLevel)Enum.Parse(typeof(Microsoft.Azure.Documents.ConsistencyLevel), this.ConsistencyLevel, ignoreCase: true);
            }

            return new DocumentClient(new Uri(this.EndPoint),
                            accountKey,
                            new ConnectionPolicy()
                            {
                                ConnectionMode = Microsoft.Azure.Documents.Client.ConnectionMode.Direct,
                                ConnectionProtocol = Protocol.Tcp,
                                UserAgentSuffix = BenchmarkConfig.UserAgentSuffix,
                                RetryOptions = new RetryOptions()
                                {
                                    MaxRetryAttemptsOnThrottledRequests = 0
                                }
                            },
                            desiredConsistencyLevel: consistencyLevel);
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Red))
            {
                foreach (Error e in errors)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            Environment.Exit(errors.Count());
        }
    }
}
