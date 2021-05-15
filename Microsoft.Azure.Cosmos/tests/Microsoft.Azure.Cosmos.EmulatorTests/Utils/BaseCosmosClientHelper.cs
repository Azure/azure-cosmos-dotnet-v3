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
        protected CosmosClient cosmosClient = null;
        protected Database database = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        public async Task TestInit(
            bool validateSinglePartitionKeyRangeCacheCall = false,
            Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient(validatePartitionKeyRangeCalls: validateSinglePartitionKeyRangeCacheCall, customizeClientBuilder: customizeClientBuilder);
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);
        }

        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.database != null)
            {
                await this.database.DeleteStreamAsync(
                    requestOptions: null,
                    cancellationToken: this.cancellationToken);
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient.Dispose();
        }
    }
}
