//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;
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

            //ItemBenchmark itemBenchmark = new ItemBenchmark();
            //await itemBenchmark.InsertItem();

            //List<Task> all = new List<Task>();
            //for (int i = 0; i < 100; i++)
            //{
            //    all.Add(Test(100000));
            //}

            //Task.WaitAll(all.ToArray());
        }

        static async Task Test(int count)
        {
            await Task.Yield();

            ItemBenchmark itemBenchmark = new ItemBenchmark();
            for (int i=0; i< count; i++)
            {
                await itemBenchmark.CreateItem();
            }
        }
    }
}
