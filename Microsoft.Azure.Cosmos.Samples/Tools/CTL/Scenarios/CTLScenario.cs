//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace CosmosCTL
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal abstract class CTLScenario
    {
        public abstract Task RunAsync(
            CTLConfig config,
            CosmosClient cosmosClient,
            ILogger logger,
            ILogger metricsCounter);
    }
}
