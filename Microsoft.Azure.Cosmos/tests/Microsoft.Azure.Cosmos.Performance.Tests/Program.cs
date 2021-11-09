//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Reports;
    using BenchmarkDotNet.Running;
    using Newtonsoft.Json;

    class Program
    {
        static int Main(string[] args)
        {
            //CosmosDBConfiguration environmentConfiguration = ConfigurationService.Configuration;
            //Console.WriteLine($"Starting benchmark and dropping results on {environmentConfiguration.ReportsPath}.");
            //BenchmarkRunner.Run<ItemBenchmark>(new CustomBenchmarkConfiguration(environmentConfiguration));

            // The following flag is passed in via the gates to run the validation. This way local runs do not get blocked
            // on performance changes
            List<string> argsList = args != null ? new List<string>(args) : new List<string>();
            bool validateBaseline = argsList.Remove("--BaselineValidation");
            string[] updatedArgs = argsList.ToArray();

            if (validateBaseline)
            {
                SortedDictionary<string, double> operationToAllocatedMemory = new SortedDictionary<string, double>();

                // Run the test 3 times and average the results to help reduce any random variance in the results
                for(int i = 0; i < 3; i++)
                {
                    IEnumerable<Summary> summaries = BenchmarkSwitcher
                        .FromAssembly(typeof(Program).Assembly)
                        .Run(updatedArgs);

                    if (!PerformanceValidation.TryUpdateAllocatedMemoryAverage(summaries, operationToAllocatedMemory))
                    {
                        return -1;
                    }
                }

                return PerformanceValidation.ValidateSummaryResultsAgainstBaseline(operationToAllocatedMemory);
            }
            else
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                    .Run(updatedArgs);
            }

            return 0;
        }
    }
}
