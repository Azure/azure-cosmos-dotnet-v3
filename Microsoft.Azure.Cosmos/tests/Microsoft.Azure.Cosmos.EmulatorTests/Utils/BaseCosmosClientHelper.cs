//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;

    public abstract class BaseCosmosClientHelper
    {
        static private CosmosClient defaultCosmosClient = null;

        private CosmosClient cosmosClient = null;
        protected Database database = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        private async Task BaseInit(CosmosClient client)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);
            Logger.LogLine($"Created {client.ClientId} clients");
        }

        public async Task TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            BaseCosmosClientHelper.defaultCosmosClient ??= TestCommon.CreateCosmosClient();
            
            await this.BaseInit(BaseCosmosClientHelper.defaultCosmosClient);
        }

        public async Task TestInit(
            bool validateSinglePartitionKeyRangeCacheCall,
            Action<CosmosClientBuilder> customizeClientBuilder = null,
            string accountEndpointOverride = null)
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(
                validatePartitionKeyRangeCalls: validateSinglePartitionKeyRangeCacheCall,
                customizeClientBuilder: customizeClientBuilder,
                accountEndpointOverride: accountEndpointOverride);
            await this.BaseInit(this.cosmosClient);
        }

        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteStreamAsync(
                    requestOptions: null,
                    cancellationToken: this.cancellationToken);
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient?.Dispose();
        }

        public CosmosClient GetClient()
        {
            return this.cosmosClient ?? BaseCosmosClientHelper.defaultCosmosClient;
        }

        public void SetClient(CosmosClient client)
        {
            this.cosmosClient?.Dispose();
            this.cosmosClient = client;
        }
    }
}
