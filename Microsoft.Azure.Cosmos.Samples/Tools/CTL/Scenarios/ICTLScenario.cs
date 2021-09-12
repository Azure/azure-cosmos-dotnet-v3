//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace CosmosCTL
{
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal interface ICTLScenario
    {
        /// <summary>
        /// Initialization tasks that will not be measured nor produce metrics.
        /// Such as container creation if needed.
        public Task InitializeAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger);

        public Task RunAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            IMetrics metrics,
            string loggingContextIdentifier,
            CancellationToken cancellationToken);
    }
}
