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
    using System.Text;
    using App.Metrics.Counter;

    internal class QueryScenario : ICTLScenario
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

            CounterOptions documentCounter = new CounterOptions { Name = "#Documents received", Context = loggingContextIdentifier };
            while (stopWatch.Elapsed <= config.RunningTimeDurationAsTimespan)
            {
                // To really debug what happened on the query, having a list of all continuations would be useful.
                List<string> allContinuations = new List<string>();
                int documentTotal = 0;
                string continuation;
                Container container = cosmosClient.GetContainer(config.Database, config.Collection);
                FeedIterator<Dictionary<string, string>> query = container.GetItemQueryIterator<Dictionary<string, string>>("select * from c");
                try
                {
                    while (query.HasMoreResults)
                    {
                        FeedResponse<Dictionary<string, string>> response = await query.ReadNextAsync();
                        documentTotal += response.Count;
                        metrics.Measure.Counter.Increment(documentCounter, response.Count);
                        continuation = response.ContinuationToken;
                        allContinuations.Add(continuation);
                        if (continuation != null)
                        {
                            // Use continuation to paginate on the query instead of draining just the initial query
                            // This validates that we can indeed move forward with the continuation
                            query = container.GetItemQueryIterator<Dictionary<string, string>>("select * from c", continuation);
                        }
                    }

                    if (config.PreCreatedDocuments > 0)
                    {
                        if (this.initializationResult.InsertedDocuments != documentTotal)
                        {
                            StringBuilder errorDetail = new StringBuilder();
                            errorDetail.AppendLine($"Expected to read {this.initializationResult.InsertedDocuments} but got {documentTotal}");
                            foreach (string c in allContinuations)
                            {
                                errorDetail.AppendLine($"Continuation: {c}");
                            }

                            logger.LogError(errorDetail.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    StringBuilder errorDetail = new StringBuilder();
                    errorDetail.AppendLine("Failure while looping through query.");
                    foreach (string c in allContinuations)
                    {
                        errorDetail.AppendLine($"Continuation: {c}");
                    }

                    logger.LogError(ex, errorDetail.ToString());
                }
            }

            stopWatch.Stop();
        }
    }
}
