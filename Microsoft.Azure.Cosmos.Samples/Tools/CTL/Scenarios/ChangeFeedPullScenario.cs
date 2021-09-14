//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Diagnostics;
    using App.Metrics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using App.Metrics.Gauge;

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
            Container container = cosmosClient.GetContainer(config.Database, config.Collection);

            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
            {
                long documentTotal = 0;
                string continuation = null;
                FeedIterator<Dictionary<string, string>> changeFeedPull 
                    = container.GetChangeFeedIterator<Dictionary<string, string>>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental);

                try
                {
                    while (changeFeedPull.HasMoreResults)
                    {
                        FeedResponse<Dictionary<string, string>> response = await changeFeedPull.ReadNextAsync();
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
                        if (this.initializationResult.InsertedDocuments == documentTotal)
                        {
                            logger.LogInformation($"Success: The number of new documents match the number of pre-created documents: {this.initializationResult.InsertedDocuments}");
                        }
                        else
                        {
                            logger.LogError($"The prepopulated documents and the change feed documents don't match.  Preconfigured Docs = {this.initializationResult.InsertedDocuments}, Change feed Documents = {documentTotal}.{Environment.NewLine}{continuation}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    metrics.Measure.Gauge.SetValue(documentGauge, documentTotal);
                    logger.LogError(ex, "Failure while looping through change feed documents");
                }
            }

            stopWatch.Stop();
        }
    }
}
