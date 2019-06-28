//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    class ItemDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                accountKey: "TestKey",
                clientOptions: new CosmosClientOptions());
        }
    }
}
