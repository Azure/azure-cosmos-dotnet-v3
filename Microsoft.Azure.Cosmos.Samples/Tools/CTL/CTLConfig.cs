//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    public class CTLConfig
    {
        private static readonly string UserAgentSuffix = "cosmosdbdotnetctl";

        [Option("ctl_endpoint", Required = true, HelpText = "Cosmos account end point")]
        public string EndPoint { get; set; }

        [Option("ctl_key", Required = true, HelpText = "Cosmos account master key")]
        [JsonIgnore]
        public string Key { get; set; }

        [Option("ctl_database", Required = false, HelpText = "Database name")]
        public string Database { get; set; } = "CTLDatabase";

        [Option("ctl_collection", Required = false, HelpText = "Collection name")]
        public string Collection { get; set; } = "CTLCollection";

        [Option("ctl_operation", Required = false, HelpText = "Workload type")]
        public WorkloadType WorkloadType { get; set; } = WorkloadType.ReadWriteQuery;

        [Option("ctl_consistency_level", Required = false, HelpText = "Client consistency level to override")]
        public string ConsistencyLevel { get; set; }

        [Option("ctl_concurrency", Required = false, HelpText = "Client concurrency")]
        public int Concurrency { get; set; } = 50;

        [Option("ctl_throughput", Required = false, HelpText = "Provisioned throughput to use")]
        public int Throughput { get; set; } = 100000;

        [Option("ctl_read_write_query_pct", Required = false, HelpText = "Distribution of read, writes, and queries")]
        public string ReadWriteQueryPercentage { get; set; } = "90,9,1";

        [Option("ctl_number_of_operations", Required = false, HelpText = "Number of documents to insert")]
        public long Operations { get; set; } = -1;

        [Option("ctl_max_running_time_duration", Required = false, HelpText = "Running time.")]
        public string RunningTimeDuration
        {
            get => this.RunningTimeDurationAsTimespan.ToString();
            set => this.RunningTimeDurationAsTimespan = System.Xml.XmlConvert.ToTimeSpan(value);

        }

        [Option("ctl_number_Of_collection", Required = false, HelpText = "Number of collections to use")]
        public int CollectionCount { get; set; } = 4;

        [Option("ctl_diagnostics_threshold_duration", Required = false, HelpText = "Threshold to log diagnostics")]
        public string DiagnosticsThresholdDuration
        {
            get => this.DiagnosticsThresholdDurationAsTimespan.ToString();
            set => this.DiagnosticsThresholdDurationAsTimespan = System.Xml.XmlConvert.ToTimeSpan(value);

        }

        [Option("ctl_content_response_on_write", Required = false, HelpText = "Should return content response on writes")]
        public bool IsContentResponseOnWriteEnabled { get; set; } = true;

        [Option("ctl_output_event_traces", Required = false, HelpText = "Should return content response on writes")]
        public bool OutputEventTraces { get; set; } = true;

        [Option("ctl_reporting_interval", Required = false, HelpText = "Reporting interval")]
        public int ReportingIntervalInSeconds { get; set; } = 10;

        [Option("ctl_graphite_endpoint", Required = false, HelpText = "Graphite endpoint to report metrics")]
        public string GraphiteEndpoint { get; set; }

        [Option("ctl_graphite_port", Required = false, HelpText = "Graphite port to report metrics")]
        public string GraphitePort { get; set; }

        internal TimeSpan RunningTimeDurationAsTimespan { get; private set; } = TimeSpan.FromHours(10);
        internal TimeSpan DiagnosticsThresholdDurationAsTimespan { get; private set; } = TimeSpan.FromSeconds(60);

        internal static CTLConfig From(string[] args)
        {
            CTLConfig options = null;
            Parser parser = new Parser((settings) => settings.CaseSensitive = false);
            ParserResult<CTLConfig> parserResult = parser.ParseArguments<CTLConfig>(args);

            parserResult.WithParsed<CTLConfig>(e => options = e)
                .WithNotParsed<CTLConfig>(e => CTLConfig.HandleParseError(e, parserResult));

            return options;
        }

        internal CosmosClient CreateCosmosClient()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ApplicationName = CTLConfig.UserAgentSuffix,
                MaxRetryAttemptsOnRateLimitedRequests = 0
            };

            if (!string.IsNullOrWhiteSpace(this.ConsistencyLevel))
            {
                if (Enum.TryParse(this.ConsistencyLevel, out ConsistencyLevel consistencyLevel))
                {
                    clientOptions.ConsistencyLevel = consistencyLevel;
                }
                else
                {
                    throw new ArgumentException($"Cannot parse consistency {this.ConsistencyLevel}", nameof(this.ConsistencyLevel));
                }
            }

            return new CosmosClient(
                        this.EndPoint,
                        this.Key,
                        clientOptions);
        }

        private static void HandleParseError(
            IEnumerable<Error> errors,
            ParserResult<CTLConfig> parserResult)
        {
            SentenceBuilder sentenceBuilder = SentenceBuilder.Create();
            foreach (Error e in errors)
            {
                if (e is HelpRequestedError _)
                {
                    HelpText.AutoBuild(parserResult);
                }
                else
                {
                    Console.WriteLine(sentenceBuilder.FormatError(e));
                }
            }

            Environment.Exit(errors.Count());
        }
    }
}
