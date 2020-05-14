//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using BenchmarkDotNet.Running;
    using CosmosDB.Benchmark.Common.Models;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;
    using Microsoft.Azure.Cosmos.Performance.Tests.BenchmarkStrategies;
    using Microsoft.Azure.Cosmos.Performance.Tests.Services;

    class Program
    {
        static void Main(string[] args)
        {
            //CosmosDBConfiguration environmentConfiguration = ConfigurationService.Configuration;
            //Console.WriteLine($"Starting benchmark and dropping results on {environmentConfiguration.ReportsPath}.");
            //BenchmarkRunner.Run<ItemBenchmark>(new CustomBenchmarkConfiguration(environmentConfiguration));

            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args);
        }
    }
}
