//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Gauge;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class ChangeFeedProcessorScenario : ICTLScenario
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
            logger.LogInformation("Initializing counters and metrics.");
            CounterOptions documentCounter = new CounterOptions { Name = "#Documents received", Context = loggingContextIdentifier };
            GaugeOptions leaseGauge = new GaugeOptions { Name = "#Leases created", Context = loggingContextIdentifier };

            string leaseContainerId = Guid.NewGuid().ToString();
            Container leaseContainer = await cosmosClient.GetDatabase(config.Database).CreateContainerAsync(leaseContainerId, "/id");
            logger.LogInformation("Created lease container {0}", leaseContainerId);

            try
            {
                object lockObject = new object();
                long documentTotal = 0;
                ChangeFeedProcessor changeFeedProcessor = cosmosClient.GetContainer(config.Database, config.Collection)
                    .GetChangeFeedProcessorBuilder<SimpleItem>("ctlProcessor",
                    (IReadOnlyCollection<SimpleItem> docs, CancellationToken token) =>
                    {
                        lock (lockObject)
                        {
                            documentTotal += docs.Count;
                        }

                        metrics.Measure.Counter.Increment(documentCounter, docs.Count);
                        return Task.CompletedTask;
                    })
                    .WithLeaseContainer(leaseContainer)
                    .WithInstanceName(Guid.NewGuid().ToString())
                    .WithStartTime(DateTime.MinValue.ToUniversalTime())
                    .Build();

                await changeFeedProcessor.StartAsync();
                logger.LogInformation("Started change feed processor");

                await Task.Delay(config.RunningTimeDurationAsTimespan, cancellationToken);

                logger.LogInformation("Stopping change feed processor");
                await changeFeedProcessor.StopAsync();

                // List leases
                using FeedIterator<LeaseSchema> leaseIterator = leaseContainer.GetItemQueryIterator<LeaseSchema>();
                int leaseTotal = 0;
                List<FeedRange> ranges = new List<FeedRange>();
                while (leaseIterator.HasMoreResults)
                {
                    FeedResponse<LeaseSchema> response = await leaseIterator.ReadNextAsync();
                    foreach (LeaseSchema lease in response)
                    {
                        if (lease.LeaseToken != null)
                        {
                            logger.LogInformation($"Lease for range {lease.LeaseToken} - {lease.FeedRange.EffectiveRange.Min} - {lease.FeedRange.EffectiveRange.Max}");
                            ranges.Add(lease.FeedRange.EffectiveRange);
                            leaseTotal++;
                        }
                    }
                }

                logger.LogInformation($"Total count of leases {leaseTotal}.");
                metrics.Measure.Gauge.SetValue(leaseGauge, leaseTotal);

                string previousMin = "";
                foreach (FeedRange sortedRange in ranges.OrderBy(range => range.Min))
                {
                    if (previousMin != sortedRange.Min)
                    {
                        logger.LogError($"Expected a sorted range with Min <{previousMin}> but encountered range <{sortedRange.Min}>:<{sortedRange.Max}>");
                    }

                    previousMin = sortedRange.Max;
                }

                if (config.PreCreatedDocuments > 0)
                {
                    if (this.initializationResult.InsertedDocuments != documentTotal)
                    {
                        logger.LogError($"Expected to receive {this.initializationResult.InsertedDocuments} documents and got {documentTotal}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure during Change Feed Processor initialization");
            }
            finally
            {
                await leaseContainer.DeleteContainerAsync();
                if (this.initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteAsync();
                }

                if (this.initializationResult.CreatedContainer)
                {
                    await cosmosClient.GetContainer(config.Database, config.Collection).DeleteContainerAsync();
                }
            }
        }

        private class SimpleItem
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }

        internal class LeaseSchema
        {
            [JsonProperty("id")]
            public string LeaseId { get; set; }

            [JsonProperty("LeaseToken")]
            public string LeaseToken { get; set; }

            [JsonProperty("FeedRange")]
            public Range FeedRange { get; set; }

        }

        internal class Range
        {
            [JsonProperty("Range")]
            public FeedRange EffectiveRange { get; set; }
        }

        internal class FeedRange
        {
            [JsonProperty("min")]
            public string Min { get; set; }

            [JsonProperty("max")]
            public string Max { get; set; }
        }
    }
}
