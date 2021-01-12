//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Formatters.Json;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

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
                    IMetricsRoot metrics = ConfigureReporting(config);

                    ICTLScenario scenario = CreateScenario(config.WorkloadType);

                    using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                    await scenario.InitializeAsync(
                        config: config,
                        cosmosClient: client,
                        logger: logger);

                    List<Task> tasks = new List<Task>
                    {
                        scenario.RunAsync(
                        config: config,
                        cosmosClient: client,
                        logger: logger,
                        metrics: metrics,
                        cancellationToken: cancellationTokenSource.Token),

                        Task.Run(async () =>
                        {
                            while (!cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(config.ReportingIntervalInSeconds));
                                await Task.WhenAll(metrics.ReportRunner.RunAllAsync());
                            }

                        })
                    };

                    await Task.WhenAny(tasks);
                    cancellationTokenSource.Cancel();
                    // Final report
                    await Task.WhenAll(metrics.ReportRunner.RunAllAsync());

                    logger.LogInformation($"{nameof(CosmosCTL)} completed successfully.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during execution");
            }
        }

        private static IMetricsRoot ConfigureReporting(CTLConfig config)
        {
            if (!string.IsNullOrEmpty(config.GraphiteEndpoint))
            {
                return new MetricsBuilder()
                    .Report.ToGraphite(
                        options => {
                            options.Graphite.BaseUri = new Uri($"{config.GraphiteEndpoint}:{config.GraphitePort}");
                            options.ClientPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
                            options.ClientPolicy.FailuresBeforeBackoff = 5;
                            options.ClientPolicy.Timeout = TimeSpan.FromSeconds(10);
                            options.FlushInterval = TimeSpan.FromSeconds(config.ReportingIntervalInSeconds);
                        })
                    .Build();
            }

            return new MetricsBuilder()
                .Report.ToConsole(
                    options => {
                        options.FlushInterval = TimeSpan.FromSeconds(config.ReportingIntervalInSeconds);
                        options.MetricsOutputFormatter = new MetricsJsonOutputFormatter();
                    })
                .Build();
        }

        private static ICTLScenario CreateScenario(WorkloadType workloadType)
        {
            return workloadType switch
            {
                WorkloadType.ReadWriteQuery => new ReadWriteQueryScenario(),
                _ => throw new NotImplementedException($"No mapping for {workloadType}"),
            };
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
