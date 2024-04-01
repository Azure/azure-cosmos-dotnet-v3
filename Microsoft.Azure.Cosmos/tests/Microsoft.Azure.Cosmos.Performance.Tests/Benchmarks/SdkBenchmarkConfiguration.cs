//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System.Linq;
    using BenchmarkDotNet.Columns;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Exporters.Csv;
    using BenchmarkDotNet.Validators;

    public class SdkBenchmarkConfiguration : ManualConfig
    {
        public SdkBenchmarkConfiguration()
        {
            this.AddValidator(JitOptimizationsValidator.DontFailOnError);
            this.AddLogger(DefaultConfig.Instance.GetLoggers().ToArray());
            this.AddColumn(StatisticColumn.Q3);
            this.AddColumn(StatisticColumn.P80);
            this.AddColumn(StatisticColumn.P85);
            this.AddColumn(StatisticColumn.P90);
            this.AddColumn(StatisticColumn.P95);
            this.AddColumn(StatisticColumn.P100);
            this.AddDiagnoser(new IDiagnoser[] { MemoryDiagnoser.Default, ThreadingDiagnoser.Default });
            this.AddColumn(StatisticColumn.OperationsPerSecond);
            this.AddExporter(MarkdownExporter.Default);
            this.AddExporter(CsvExporter.Default);
            this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());
        }
    }
}
