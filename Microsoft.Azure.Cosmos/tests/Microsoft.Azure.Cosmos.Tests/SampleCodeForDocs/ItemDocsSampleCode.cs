//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    internal class ItemDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            this.cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                authKeyOrResourceToken: "TestKey",
                clientOptions: new CosmosClientOptions());
        }
    }
}
