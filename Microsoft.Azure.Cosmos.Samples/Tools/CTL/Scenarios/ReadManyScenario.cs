//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using App.Metrics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using App.Metrics.Gauge;

    internal class ReadManyScenario : ICTLScenario
    {
        private Utils.InitializationResult initializationResult;
        private List<(string, PartitionKey)> idAndPkPairs;

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
                this.idAndPkPairs = new List<(string, PartitionKey)>();
                logger.LogInformation("Pre-populating {0} documents", config.PreCreatedDocuments);
                IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> insertedDocuments = await Utils.PopulateDocumentsAsync(config, logger, new List<Container>() { cosmosClient.GetContainer(config.Database, config.Collection) });
                this.initializationResult.InsertedDocuments = insertedDocuments[config.Collection].Count;
                foreach(Dictionary<string, string> document in insertedDocuments[config.Collection])
                {
                    this.idAndPkPairs.Add((document["id"], new PartitionKey(document[config.CollectionPartitionKey])));
                }
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
            try
            {
                Stopwatch stopWatch = Stopwatch.StartNew();

                GaugeOptions documentGauge = new GaugeOptions
                {
                    Name = $"#ReadMany received documents",
                    MeasurementUnit = Unit.Items,
                    Context = loggingContextIdentifier
                };

                Container container = cosmosClient.GetContainer(config.Database, config.Collection);
                while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan
                    && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        FeedResponse<Dictionary<string, string>> response = await container.ReadManyItemsAsync<Dictionary<string, string>>(this.idAndPkPairs);
                        metrics.Measure.Gauge.SetValue(documentGauge, response.Count);
                        if (this.idAndPkPairs.Count > 0 && this.idAndPkPairs.Count != response.Count)
                        {
                            Utils.LogError(logger, loggingContextIdentifier, $"ReadMany expected {this.idAndPkPairs.Count} but got {response.Count}{Environment.NewLine}{response.Diagnostics}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogError(logger, loggingContextIdentifier, ex);
                    }
                }

                stopWatch.Stop();
            }
            catch (Exception ex)
            {
                Utils.LogError(logger, loggingContextIdentifier, ex);
            }
            finally
            {
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
