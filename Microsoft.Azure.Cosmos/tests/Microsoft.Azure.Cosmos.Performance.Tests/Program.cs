//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using BenchmarkDotNet.Running;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;

    class Program
    {
        static void Main(string[] args)
        {
            //CosmosDBConfiguration environmentConfiguration = ConfigurationService.Configuration;
            //Console.WriteLine($"Starting benchmark and dropping results on {environmentConfiguration.ReportsPath}.");
            //BenchmarkRunner.Run<ItemBenchmark>(new CustomBenchmarkConfiguration(environmentConfiguration));
            BenchmarkRunner.Run<DiagnosticBenchmark>();
            //BenchmarkSwitcher
            //    .FromAssembly(typeof(Program).Assembly)
            //    .Run(args);
        }
    }
}
