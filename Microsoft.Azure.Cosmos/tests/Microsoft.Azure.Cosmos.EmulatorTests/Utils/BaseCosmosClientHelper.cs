//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class BaseCosmosClientHelper
    {
        protected CosmosClient cosmosClient = null;
        protected CosmosDatabase database = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellation;

        public async Task TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellation = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.Databases.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellation: this.cancellation);
        }

        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.database != null)
            {
                await this.database.DeleteAsync(
                    requestOptions: null,
                    cancellation: this.cancellation);
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient.Dispose();
        }
    }
}
