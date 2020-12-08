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
            this.Add(JitOptimizationsValidator.DontFailOnError);
            this.Add(DefaultConfig.Instance.GetLoggers().ToArray());
            this.Add(StatisticColumn.Q3);
            this.Add(StatisticColumn.P80);
            this.Add(StatisticColumn.P85);
            this.Add(StatisticColumn.P90);
            this.Add(StatisticColumn.P95);
            this.Add(StatisticColumn.P100);
            this.Add(new IDiagnoser[] { MemoryDiagnoser.Default, ThreadingDiagnoser.Default });
            this.Add(StatisticColumn.OperationsPerSecond);
            this.Add(MarkdownExporter.Default);
            this.Add(CsvExporter.Default);
            this.Add(DefaultConfig.Instance.GetColumnProviders().ToArray());
        }
    }
}
