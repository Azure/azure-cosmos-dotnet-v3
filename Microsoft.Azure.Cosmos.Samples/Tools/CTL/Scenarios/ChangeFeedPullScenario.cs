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


    internal class ChangeFeedPullScenario : ICTLScenario
    {

        public async Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {

            if (config.PreCreatedDocuments > 0)
            {
                logger.LogInformation("Pre-populating {0} documents", config.PreCreatedDocuments);
                await Utils.PopulateDocumentsAsync(config, logger, new List<Container>() { cosmosClient.GetContainer(config.Database, config.Collection) });
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

            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
            {
                int documentTotal = 0;
                string continuation = null;
                Container container = cosmosClient.GetContainer(config.Database, config.Collection);
                FeedIterator<Dictionary<string, string>> changeFeedPull = container.GetChangeFeedIterator<Dictionary<string, string>>
                                                                                (ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental);
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

                    if (config.PreCreatedDocuments == documentTotal)
                    {
                        logger.LogInformation($"Success: The number of new documents match the number of pre-created documents: {config.PreCreatedDocuments}");
                    }
                    else
                    {
                        logger.LogError($"The prepopulated documents and the new documents don't match.  Preconfigured Docs = {config.PreCreatedDocuments}, New Documents = {documentTotal}");
                        logger.LogError(continuation);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure while looping through new documents");
                }
            }

            stopWatch.Stop();
        }
    }
}
