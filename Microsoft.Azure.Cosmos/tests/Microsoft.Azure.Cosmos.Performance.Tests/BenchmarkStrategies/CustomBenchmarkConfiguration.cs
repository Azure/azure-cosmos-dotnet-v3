// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.BenchmarkStrategies
{
    using System.Linq;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Validators;
    using CosmosDB.Benchmark.Common.Models;

    /// <summary>
    /// Customized Benchmark Configuratio that adds most common columns and exporters.
    /// </summary>
    public class CustomBenchmarkConfiguration : ManualConfig
    {
        public static string GetReportPath(CosmosDBConfiguration configuration)
        {
            return configuration.ReportsPath;
        }

        public CustomBenchmarkConfiguration(CosmosDBConfiguration configuration)
        {
            this.Add(JitOptimizationsValidator.DontFailOnError);
            this.Add(DefaultConfig.Instance.GetLoggers().ToArray());
            this.Add(StatisticColumn.P90);
            this.Add(StatisticColumn.P95);
            this.Add(StatisticColumn.P100);
            this.Add(StatisticColumn.OperationsPerSecond);
            this.Add(MarkdownExporter.Default);
            this.Add(CsvExporter.Default);
            this.Add(DefaultConfig.Instance.GetColumnProviders().ToArray());
            this.ArtifactsPath = CustomBenchmarkConfiguration.GetReportPath(configuration);
        }
    }
}
