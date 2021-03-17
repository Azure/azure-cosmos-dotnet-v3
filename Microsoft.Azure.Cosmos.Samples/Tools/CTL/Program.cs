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
    using App.Metrics.Gauge;
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
                if (config.OutputEventTraces)
                {
                    EnableTraceSourcesToConsole();
                }

                using CosmosClient client = config.CreateCosmosClient();

                string loggingContextIdentifier = $"{config.WorkloadType}{config.LogginContext}";
                using (logger.BeginScope(loggingContextIdentifier))
                {
                    IMetricsRoot metrics = ConfigureReporting(config, logger);

                    ICTLScenario scenario = CreateScenario(config.WorkloadType);

                    using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                    await scenario.InitializeAsync(
                        config: config,
                        cosmosClient: client,
                        logger: logger);

                    logger.LogInformation("Initialization completed.");

                    List<Task> tasks = new List<Task>
                    {
                        scenario.RunAsync(
                        config: config,
                        cosmosClient: client,
                        logger: logger,
                        metrics: metrics,
                        loggingContextIdentifier: loggingContextIdentifier,
                        cancellationToken: cancellationTokenSource.Token),

                        Task.Run(async () =>
                        { 
                            // Measure CPU/memory
                            Process process = Process.GetCurrentProcess();
                            
                            GaugeOptions processPhysicalMemoryGauge = new GaugeOptions
                            {
                                Name = "Process Working Set",
                                MeasurementUnit = Unit.Bytes,
                                Context = loggingContextIdentifier
                            };

                            GaugeOptions totalCpuGauge = new GaugeOptions
                            {
                                Name = "Total CPU",
                                MeasurementUnit = Unit.Percent,
                                Context = loggingContextIdentifier
                            };

                            GaugeOptions priviledgedCpuGauge = new GaugeOptions
                            {
                                Name = "Priviledged CPU",
                                MeasurementUnit = Unit.Percent,
                                Context = loggingContextIdentifier
                            };

                            GaugeOptions userCpuGauge = new GaugeOptions
                            {
                                Name = "User CPU",
                                MeasurementUnit = Unit.Percent,
                                Context = loggingContextIdentifier
                            };

                            DateTime lastTimeStamp = process.StartTime;
                            TimeSpan lastTotalProcessorTime = TimeSpan.Zero;
                            TimeSpan lastUserProcessorTime = TimeSpan.Zero;
                            TimeSpan lastPrivilegedProcessorTime = TimeSpan.Zero;

                            while (!cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(config.ReportingIntervalInSeconds));
                                process.Refresh();

                                double totalCpuTimeUsed = process.TotalProcessorTime.TotalMilliseconds - lastTotalProcessorTime.TotalMilliseconds;
                                double privilegedCpuTimeUsed = process.PrivilegedProcessorTime.TotalMilliseconds - lastPrivilegedProcessorTime.TotalMilliseconds;
                                double userCpuTimeUsed = process.UserProcessorTime.TotalMilliseconds - lastUserProcessorTime.TotalMilliseconds;

                                lastTotalProcessorTime = process.TotalProcessorTime;
                                lastPrivilegedProcessorTime = process.PrivilegedProcessorTime;
                                lastUserProcessorTime = process.UserProcessorTime;

                                double cpuTimeElapsed = (DateTime.UtcNow - lastTimeStamp).TotalMilliseconds * Environment.ProcessorCount;
                                lastTimeStamp = DateTime.UtcNow;

                                metrics.Measure.Gauge.SetValue(totalCpuGauge, totalCpuTimeUsed * 100 / cpuTimeElapsed);
                                metrics.Measure.Gauge.SetValue(priviledgedCpuGauge, privilegedCpuTimeUsed * 100 / cpuTimeElapsed);
                                metrics.Measure.Gauge.SetValue(userCpuGauge, userCpuTimeUsed * 100 / cpuTimeElapsed);
                                metrics.Measure.Gauge.SetValue(processPhysicalMemoryGauge, process.WorkingSet64);

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

        private static IMetricsRoot ConfigureReporting(
            CTLConfig config,
            ILogger logger)
        {
            if (!string.IsNullOrEmpty(config.GraphiteEndpoint))
            {
                logger.LogInformation($"Using Graphite server at {config.GraphiteEndpoint}:{config.GraphitePort}");
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
                WorkloadType.ChangeFeedProcessor => new ChangeFeedProcessorScenario(),
                _ => throw new NotImplementedException($"No mapping for {workloadType}"),
            };
        }

        private static void EnableTraceSourcesToConsole()
        {
            Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
            TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Clear();
            traceSource.Listeners.Add(new ConsoleTraceListener());
        }
    }
}
