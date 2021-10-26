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
        public static string GetReportPath(CosmosDBConfiguration configuration) => configuration.ReportsPath;

        public CustomBenchmarkConfiguration(CosmosDBConfiguration configuration)
        {
            this.AddValidator(JitOptimizationsValidator.DontFailOnError);
            this.AddLogger(DefaultConfig.Instance.GetLoggers().ToArray());
            this.AddColumn(StatisticColumn.P90);
            this.AddColumn(StatisticColumn.P95);
            this.AddColumn(StatisticColumn.P100);
            this.AddColumn(StatisticColumn.OperationsPerSecond);
            this.AddExporter(MarkdownExporter.Default);
            this.AddExporter(CsvExporter.Default);
            this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());
            this.ArtifactsPath = CustomBenchmarkConfiguration.GetReportPath(configuration);
        }
    }
}
