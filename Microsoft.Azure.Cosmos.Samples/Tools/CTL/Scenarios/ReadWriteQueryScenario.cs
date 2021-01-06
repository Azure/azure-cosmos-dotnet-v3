//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Histogram;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal class ReadWriteQueryScenario : CTLScenario
    {
        private static readonly string DefaultPartitionKeyPath = "/pk";

        private readonly Random random = new Random();

        public override async Task RunAsync(
            CTLConfig config, 
            CosmosClient cosmosClient,
            ILogger logger, 
            IMetrics metrics)
        {
            if (!TryParseReadWriteQueryPercentages(config.ReadWriteQueryPercentage, out ReadWriteQueryPercentage readWriteQueryPercentage))
            {
                logger.LogError("Cannot correctly parse {0} = {1}", nameof(config.ReadWriteQueryPercentage), config.ReadWriteQueryPercentage);
                return;
            }

            InitializationResult initializationResult = await CreateDatabaseAndContainersAsync(config, cosmosClient);
            if (initializationResult.CreatedDatabase)
            {
                logger.LogInformation("Created database for execution");
            }

            if (initializationResult.CreatedContainers.Count > 0)
            {
                logger.LogInformation("Created {0} collections for execution", initializationResult.CreatedContainers.Count);
            }

            try
            {
                await ExecuteOperationsAsync(config, logger, metrics, initializationResult);
            }
            catch (Exception unhandledException)
            {
                logger.LogError(unhandledException, "Unhandled exception executing {0}", nameof(ReadWriteQueryScenario));
            }
            finally
            {
                if (initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteAsync();
                }
                else
                {
                    foreach (string createdCollection in initializationResult.CreatedContainers)
                    {
                        await cosmosClient.GetContainer(config.Database, createdCollection).DeleteContainerAsync();
                    }
                }
            }
        }

        private static async Task ExecuteOperationsAsync(
            CTLConfig config,
            ILogger logger,
            IMetrics metrics,
            InitializationResult initializationResult)
        {
            logger.LogInformation("Pre-populating {0} documents", config.Operations);
            IReadOnlyCollection<Dictionary<string, string>> createdDocuments = await PopulateDocumentsAsync(config, logger, initializationResult.Containers);

            CounterOptions readSuccessMeter = new CounterOptions { Name = "#Read Successful Operations" };
            CounterOptions readFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations" };
            CounterOptions writeSuccessMeter = new CounterOptions { Name = "#Write Successful Operations" };
            CounterOptions writeFailureMeter = new CounterOptions { Name = "#Write Unsuccessful Operations" };
            CounterOptions querySuccessMeter = new CounterOptions { Name = "#Query Successful Operations" };
            CounterOptions queryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations" };

            HistogramOptions readLatencyHistogram = new HistogramOptions
            {
                Name = "Read latency",
                MeasurementUnit = Unit.Custom("Milliseconds"),
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            HistogramOptions writeLatencyHistogram = new HistogramOptions
            {
                Name = "Write latency",
                MeasurementUnit = Unit.Custom("Milliseconds"),
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            HistogramOptions queryLatencyHistogram = new HistogramOptions
            {
                Name = "Query latency",
                MeasurementUnit = Unit.Custom("Milliseconds"),
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };
        }

        private static async Task<IReadOnlyCollection<Dictionary<string, string>>> PopulateDocumentsAsync(
            CTLConfig config,
            ILogger logger,
            IEnumerable<Container> containers)
        {
            ConcurrentBag<Dictionary<string, string>> createdDocuments = new ConcurrentBag<Dictionary<string, string>>();
            foreach (Container container in containers)
            {
                long successes = 0;
                long failures = 0;

                List<Dictionary<string, string>> documentsToCreate = new List<Dictionary<string, string>>(config.Operations);
                await Utils.ForEachAsync(documentsToCreate, (Dictionary<string, string> doc) 
                    => container.CreateItemAsync(doc).ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            createdDocuments.Add(doc);
                            Interlocked.Increment(ref successes);
                        }
                        else
                        {
                            AggregateException innerExceptions = task.Exception.Flatten();
                            if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                            {
                                logger.LogError(cosmosException, "Failure pre-populating container {0}", container.Id);
                            }

                            Interlocked.Increment(ref failures);
                        }
                    }), 100);

                if (successes > 0)
                {
                    logger.LogInformation("Completed pre-populating {0} documents in container {1].", successes, container.Id);
                }

                if (failures > 0)
                {
                    logger.LogWarning("Failed pre-populating {0} documents in container {1].", failures, container.Id);
                }
            }

            return createdDocuments;
        }

        /// <summary>
        /// Create the database and the required number of collections.
        /// </summary>
        private static async Task<InitializationResult> CreateDatabaseAndContainersAsync(
            CTLConfig config,
            CosmosClient cosmosClient)
        {
            List<string> createdContainers = new List<string>();
            List<Container> containers = new List<Container>();
            InitializationResult result = new InitializationResult()
            {
                CreatedDatabase = false
            };

            Database database;
            try
            {
                database = await cosmosClient.GetDatabase(config.Database).ReadAsync();
            }
            catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(config.Database, config.Throughput);
                result.CreatedDatabase = true;
                database = databaseResponse.Database;
            }

            int collectionCount = config.CollectionCount;
            if (collectionCount <= 0)
            {
                collectionCount = 1;
            }

            for (int i = 1; i <= collectionCount; i++)
            {
                string containerName = $"{config.Collection}_{i}";
                Container container;
                try
                {
                    container = await database.GetContainer(containerName).ReadContainerAsync();
                }
                catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    container = await database.CreateContainerAsync(containerName, ReadWriteQueryScenario.DefaultPartitionKeyPath);
                    createdContainers.Add(containerName);
                }

                containers.Add(container);
            }

            result.CreatedContainers = createdContainers;
            result.Containers = containers;
            return result;
        }

        private static bool TryParseReadWriteQueryPercentages(
            string configuration,
            out ReadWriteQueryPercentage readWriteQueryPercentage)
        {
            readWriteQueryPercentage = default;
            string[] readWriteQueryPctList = configuration.Split(",");
            if (readWriteQueryPctList.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(readWriteQueryPctList[0], out int readPercentage)
                    || !int.TryParse(readWriteQueryPctList[1], out int writePercentage)
                    || !int.TryParse(readWriteQueryPctList[2], out int queryPercentage))
            {
                return false;
            }

            if ((readPercentage + writePercentage + queryPercentage) != 100)
            {
                return false;
            }

            readWriteQueryPercentage = new ReadWriteQueryPercentage()
            {
                ReadPercentage = readPercentage,
                WritePercentage = writePercentage,
                QueryPercentage = queryPercentage
            };

            return true;
        }

        private struct ReadWriteQueryPercentage
        {
            public int ReadPercentage;
            public int WritePercentage;
            public int QueryPercentage;
        }

        private struct InitializationResult
        {
            public bool CreatedDatabase;
            public IReadOnlyList<string> CreatedContainers;
            public IReadOnlyList<Container> Containers;
        }
    }
}
