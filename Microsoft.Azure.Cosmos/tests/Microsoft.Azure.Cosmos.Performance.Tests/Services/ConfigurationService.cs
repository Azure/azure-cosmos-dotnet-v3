// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Services
{
    using System;
    using CosmosDB.Benchmark.Common.Models;
    using Microsoft.Extensions.Configuration;

    public static class ConfigurationService
    {
        public static CosmosDBConfiguration Configuration
        {
            get
            {
                return cosmosDBConfiguration.Value;
            }
        }

        private static readonly Lazy<CosmosDBConfiguration> cosmosDBConfiguration = new Lazy<CosmosDBConfiguration>(CreateConfiguration);

        private static CosmosDBConfiguration CreateConfiguration()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables().Build();

            return new CosmosDBConfiguration()
            {
                    ReportsPath = configuration.GetValue<string>("PERFORMANCE_REPORT_PATH")
            };
        }
    }
}