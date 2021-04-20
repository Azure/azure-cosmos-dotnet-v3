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
    using App.Metrics.Counter;
    using App.Metrics.Timer;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal class ReadWriteQueryScenario : ICTLScenario
    {
        private readonly Random random = new Random();

        private ReadWriteQueryPercentage readWriteQueryPercentage;
        private IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> createdDocuments;
        private InitializationResult initializationResult;

        public async Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger)
        {
            if (!TryParseReadWriteQueryPercentages(config.ReadWriteQueryPercentage, out this.readWriteQueryPercentage))
            {
                logger.LogError("Cannot correctly parse {0} = {1}", nameof(config.ReadWriteQueryPercentage), config.ReadWriteQueryPercentage);
                return;
            }

            this.initializationResult = await CreateDatabaseAndContainersAsync(config, cosmosClient);
            if (this.initializationResult.CreatedDatabase)
            {
                logger.LogInformation("Created database for execution");
            }

            if (this.initializationResult.CreatedContainers.Count > 0)
            {
                logger.LogInformation("Created {0} collections for execution", this.initializationResult.CreatedContainers.Count);
            }

            logger.LogInformation("Pre-populating {0} documents", config.PreCreatedDocuments);
            this.createdDocuments = await Utils.PopulateDocumentsAsync(config, logger, this.initializationResult.Containers);
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
                await this.ExecuteOperationsAsync(
                    config,
                    logger,
                    metrics,
                    loggingContextIdentifier,
                    this.initializationResult,
                    this.readWriteQueryPercentage,
                    cancellationToken);
            }
            catch (Exception unhandledException)
            {
                logger.LogError(unhandledException, "Unhandled exception executing {0}", nameof(ReadWriteQueryScenario));
            }
            finally
            {
                if (this.initializationResult.CreatedDatabase)
                {
                    await cosmosClient.GetDatabase(config.Database).DeleteAsync();
                }
                else
                {
                    foreach (string createdCollection in this.initializationResult.CreatedContainers)
                    {
                        await cosmosClient.GetContainer(config.Database, createdCollection).DeleteContainerAsync();
                    }
                }
            }
        }

        private async Task ExecuteOperationsAsync(
            CTLConfig config,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            InitializationResult initializationResult,
            ReadWriteQueryPercentage readWriteQueryPercentage,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Initializing counters and metrics.");
            CounterOptions readSuccessMeter = new CounterOptions { Name = "#Read Successful Operations", Context = loggingContextIdentifier };
            CounterOptions readFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations", Context = loggingContextIdentifier };
            CounterOptions writeSuccessMeter = new CounterOptions { Name = "#Write Successful Operations", Context = loggingContextIdentifier };
            CounterOptions writeFailureMeter = new CounterOptions { Name = "#Write Unsuccessful Operations", Context = loggingContextIdentifier };
            CounterOptions querySuccessMeter = new CounterOptions { Name = "#Query Successful Operations", Context = loggingContextIdentifier };
            CounterOptions queryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations", Context = loggingContextIdentifier };

            TimerOptions readLatencyTimer = new TimerOptions
            {
                Name = "Read latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = loggingContextIdentifier,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            TimerOptions writeLatencyTimer = new TimerOptions
            {
                Name = "Write latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = loggingContextIdentifier,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            TimerOptions queryLatencyTimer = new TimerOptions
            {
                Name = "Query latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = loggingContextIdentifier,
                Reservoir = () => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir()
            };

            SemaphoreSlim concurrencyControlSemaphore = new SemaphoreSlim(config.Concurrency);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int writeRange = readWriteQueryPercentage.ReadPercentage + readWriteQueryPercentage.WritePercentage;
            long diagnosticsThresholdDuration = (long)config.DiagnosticsThresholdDurationAsTimespan.TotalMilliseconds;
            List<Task> operations = new List<Task>();
            for (long i = 0; ShouldContinue(stopwatch, i, config); i++)
            {
                await concurrencyControlSemaphore.WaitAsync(cancellationToken);
                long index = (long)i % 100;
                if (index < readWriteQueryPercentage.ReadPercentage)
                {
                    operations.Add(CTLOperationHandler<ItemResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(readLatencyTimer),
                        resultProducer: new SingleExecutionResultProducer<ItemResponse<Dictionary<string, string>>>(() => this.CreateReadOperation(
                            operation: i,
                            partitionKeyAttributeName: config.CollectionPartitionKey,
                            containers: initializationResult.Containers,
                            createdDocumentsPerContainer: this.createdDocuments)),
                        onSuccess: () => {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(readSuccessMeter);
                        },
                        onFailure: (Exception ex) =>
                        {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(readFailureMeter);
                            logger.LogError(ex, "Failure during read operation");
                        },
                        logDiagnostics: (ItemResponse<Dictionary<string, string>> response) => logger.LogInformation("Read request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));
                }
                else if (index < writeRange)
                {
                    operations.Add(CTLOperationHandler<ItemResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(writeLatencyTimer),
                        resultProducer: new SingleExecutionResultProducer<ItemResponse<Dictionary<string, string>>>(() => this.CreateWriteOperation(
                            operation: i,
                            partitionKeyAttributeName: config.CollectionPartitionKey,
                            containers: initializationResult.Containers,
                            isContentResponseOnWriteEnabled: config.IsContentResponseOnWriteEnabled)),
                        onSuccess: () =>
                        {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(writeSuccessMeter);
                        },
                        onFailure: (Exception ex) =>
                        {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(writeFailureMeter);
                            logger.LogError(ex, "Failure during write operation");
                        },
                        logDiagnostics: (ItemResponse<Dictionary<string, string>> response) => logger.LogInformation("Write request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));

                }
                else
                {
                    operations.Add(CTLOperationHandler<FeedResponse<Dictionary<string, string>>>.PerformOperationAsync(
                        diagnosticsLoggingThreshold: diagnosticsThresholdDuration,
                        createTimerContext: () => metrics.Measure.Timer.Time(queryLatencyTimer),
                        resultProducer: new IteratorResultProducer<Dictionary<string, string>>(this.CreateQueryOperation(
                            operation: i,
                            containers: initializationResult.Containers)),
                        onSuccess: () =>
                        {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(querySuccessMeter);
                        },
                        onFailure: (Exception ex) =>
                        {
                            concurrencyControlSemaphore.Release();
                            metrics.Measure.Counter.Increment(queryFailureMeter);
                            logger.LogError(ex, "Failure during query operation");
                        },
                        logDiagnostics: (FeedResponse<Dictionary<string, string>> response) => logger.LogInformation("Query request took more than latency threshold {0}, diagnostics: {1}", config.DiagnosticsThresholdDuration, response.Diagnostics.ToString()),
                        cancellationToken: cancellationToken));
                }
            }

            await Task.WhenAll(operations);
            stopwatch.Stop();
            logger.LogInformation("[{0}] operations performed in [{1}] seconds.",
                operations.Count, stopwatch.Elapsed.TotalSeconds);
        }

        private Task<ItemResponse<Dictionary<string, string>>> CreateReadOperation(
            long operation,
            string partitionKeyAttributeName,
            IReadOnlyList<Container> containers,
            IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, string>>> createdDocumentsPerContainer)
        {
            Container container = containers[(int)operation % containers.Count];
            IReadOnlyList<Dictionary<string, string>> documents = createdDocumentsPerContainer[container.Id];
            Dictionary<string, string> document = documents[this.random.Next(documents.Count)];
            return container.ReadItemAsync<Dictionary<string, string>>(document["id"], new PartitionKey(document[partitionKeyAttributeName]));
        }

        private Task<ItemResponse<Dictionary<string, string>>> CreateWriteOperation(
            long operation,
            string partitionKeyAttributeName,
            IReadOnlyList<Container> containers,
            bool isContentResponseOnWriteEnabled)
        {
            Container container = containers[(int)operation % containers.Count];
            Dictionary<string, string> document = Utils.GenerateDocument(partitionKeyAttributeName);
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                EnableContentResponseOnWrite = isContentResponseOnWriteEnabled
            };

            return container.CreateItemAsync<Dictionary<string, string>>(document, requestOptions: itemRequestOptions);
        }

        private FeedIterator<Dictionary<string, string>> CreateQueryOperation(
                long operation,
                IReadOnlyList<Container> containers)
        {
            Container container = containers[(int)operation % containers.Count];
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions() { MaxItemCount = 10 };
            return container.GetItemQueryIterator<Dictionary<string, string>>(
                queryText: "Select top 100 * from c order by c._ts",
                requestOptions: queryRequestOptions);
        }

        private static bool ShouldContinue(
            Stopwatch stopwatch,
            long iterationCount,
            CTLConfig config)
        {
            TimeSpan maxDurationTime = config.RunningTimeDurationAsTimespan;
            long maxNumberOfOperations = config.Operations;

            if (maxDurationTime == null)
            {
                return iterationCount < maxNumberOfOperations;
            }

            if (maxDurationTime.TotalMilliseconds < stopwatch.ElapsedMilliseconds)
            {
                return false;
            }

            if (maxNumberOfOperations < 0)
            {
                return true;
            }

            return iterationCount < maxNumberOfOperations;
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
                string containerName = collectionCount == 1 ? config.Collection : $"{config.Collection}_{i}";
                Container container;
                try
                {
                    container = await database.GetContainer(containerName).ReadContainerAsync();
                }
                catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    container = await database.CreateContainerAsync(containerName, $"/{config.CollectionPartitionKey}");
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
