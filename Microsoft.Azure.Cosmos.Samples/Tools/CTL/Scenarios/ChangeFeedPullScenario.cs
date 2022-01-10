//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Gauge;
    using App.Metrics.Timer;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal class ChangeFeedPullScenario : ICTLScenario
    {
        private Utils.InitializationResult initializationResult;

        public async Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {
            this.initializationResult = await Utils.CreateDatabaseAndContainerAsync(config, cosmosClient);

            if (this.initializationResult.CreatedDatabase)
            {
                logger.LogInformation("Created database for execution");
            }

            if (this.initializationResult.CreatedContainer)
            {
                logger.LogInformation("Created collection for execution");
            }

            if (config.PreCreatedDocuments > 0)
            {
                logger.LogInformation("Pre-populating {0} documents", config.PreCreatedDocuments);
                IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> insertedDocuments = await Utils.PopulateDocumentsAsync(config, logger, new List<Container>() { cosmosClient.GetContainer(config.Database, config.Collection) });
                this.initializationResult.InsertedDocuments = insertedDocuments[config.Collection].Count;
            }
        }

        public async Task RunAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            CancellationToken cancellationToken)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            GaugeOptions documentGauge= new GaugeOptions { Name = "#Documents received", Context = loggingContextIdentifier };

            TimerOptions readLatencyTimer = new TimerOptions
            {
                Name = "Latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = loggingContextIdentifier,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            Container container = cosmosClient.GetContainer(config.Database, config.Collection);

            try
            {
                while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
                {
                    long documentTotal = 0;
                    string continuation = null;
                    using FeedIterator<Dictionary<string, string>> changeFeedPull
                        = container.GetChangeFeedIterator<Dictionary<string, string>>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental);

                    try
                    {
                        while (changeFeedPull.HasMoreResults)
                        {
                            FeedResponse<Dictionary<string, string>> response;
                            using (TimerContext timerContext = metrics.Measure.Timer.Time(readLatencyTimer))
                            {
                                response = await changeFeedPull.ReadNextAsync();
                                Utils.LogDiagnostics(
                                    logger: logger,
                                    operationName: nameof(ChangeFeedPullScenario),
                                    timerContextLatency: timerContext.Elapsed,
                                    config: config,
                                    cosmosDiagnostics: response.Diagnostics);
                            }

                            documentTotal += response.Count;
                            continuation = response.ContinuationToken;
                            if (response.StatusCode == HttpStatusCode.NotModified)
                            {
                                break;
                            }
                        }

                        metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);

                        if (config.PreCreatedDocuments > 0)
                        {
                            if (this.initializationResult.InsertedDocuments != documentTotal)
                            {
                                Utils.LogError(logger, loggingContextIdentifier, $"The prepopulated documents and the change feed documents don't match.  Preconfigured Docs = {this.initializationResult.InsertedDocuments}, Change feed Documents = {documentTotal}.{Environment.NewLine}{continuation}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);
                        Utils.LogError(logger, loggingContextIdentifier, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogError(logger, loggingContextIdentifier, ex);
            }
            finally
            {
                stopWatch.Stop();
                if (this.initializationResult.CreatedContainer)
                {
                    await cosmosClient.GetContainer(config.Database, config.Collection).DeleteContainerStreamAsync();
                }

                if (this.initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteStreamAsync();
                }
            }
        }
    }
}
