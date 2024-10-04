//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    public abstract class BaseCosmosClientHelper
    {
        private static readonly CosmosClient defaultCosmosClient = TestCommon.CreateCosmosClient();

        private CosmosClient cosmosClient = null;
        protected Database database = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        private async Task BaseInit(CosmosClient client)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            await Util.DeleteAllDatabasesAsync(client);

            this.database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);
        }

        public async Task TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

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

            // Only dispose if the caller set a custom client
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
