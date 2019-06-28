//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    using System.Threading.Tasks;

    internal class DatabaseDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                accountKey: "TestKey",
                clientOptions: new CosmosClientOptions());
        }

        public async Task DatabaseCreateWithThroughput()
        {
            // <DatabaseCreateWithThroughput>
            string databaseName = "MyDatabaseName";
            await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    id: databaseName,
                    throughput: 1000);
            // </DatabaseCreateWithThroughput>
        }
    }
}
