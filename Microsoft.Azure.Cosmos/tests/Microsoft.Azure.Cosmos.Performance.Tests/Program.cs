//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Reports;
    using BenchmarkDotNet.Running;

    class Program
    {
        static int Main(string[] args)
        {
            //CosmosDBConfiguration environmentConfiguration = ConfigurationService.Configuration;
            //Console.WriteLine($"Starting benchmark and dropping results on {environmentConfiguration.ReportsPath}.");
            //BenchmarkRunner.Run<ItemBenchmark>(new CustomBenchmarkConfiguration(environmentConfiguration));

            IEnumerable<Summary> summaries = BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args);

            foreach (Summary summary in summaries)
            {
                string[] content = summary.Table.Columns.First(x => string.Equals(@"Op/s", x.Header)).Content;
                foreach (string ops in content)
                {
                    if (string.Equals(@"NA", ops))
                    {
                        return -1;
                    }
                }
            }

            return 0;
        }
    }
}
