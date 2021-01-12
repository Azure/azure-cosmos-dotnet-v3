//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
                        .AddConsole());
            
            ILogger logger = loggerFactory.CreateLogger<Program>();

            try
            {
                CTLConfig config = CTLConfig.From(args);
                using CosmosClient client = config.CreateCosmosClient();

                using (logger.BeginScope(config.WorkloadType))
                {


                    logger.LogInformation($"{nameof(CosmosCTL)} completed successfully.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during execution");
            }
        }




        private static void ClearCoreSdkListeners()
        {
            Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
            TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Clear();
        }
    }
}
