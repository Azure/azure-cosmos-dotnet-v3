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
            this.cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                authKeyOrResourceToken: "TestKey",
                clientOptions: new CosmosClientOptions());
        }

        public async Task DatabaseCreateWithThroughput()
        {
            // <DatabaseCreateWithThroughput>
            //create the database with throughput
            string databaseName = "MyDatabaseName";
            await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    id: databaseName,
                    throughput: 1000);
            // </DatabaseCreateWithThroughput>
        }
    }
}