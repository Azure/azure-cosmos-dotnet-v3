//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;

    class CustomDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            this.cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                authKeyOrResourceToken: "TestKey",
                clientOptions: new CosmosClientOptions());
        }

        public async Task GetRequestChargeFromResource()
        {
            // <GetRequestCharge>
            Container container = this.cosmosClient.GetContainer("database", "container");
            string itemId = "myItem";
            string partitionKey = "partitionKey";
            string storedProcedureId = "storedProcedureId";
            string queryText = "SELECT * FROM c";

            ItemResponse<dynamic> itemResponse = await container.CreateItemAsync<dynamic>(
                item: new { id = itemId, pk = partitionKey },
                partitionKey: new PartitionKey(partitionKey));
            _ = itemResponse.RequestCharge;

            Scripts scripts = container.Scripts;
            StoredProcedureExecuteResponse<object> sprocResponse = await scripts.ExecuteStoredProcedureAsync<object>(
                storedProcedureId: storedProcedureId,
                partitionKey: new PartitionKey(partitionKey),
                parameters: new dynamic[] { new object() });

            _ = sprocResponse.RequestCharge;

            FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(
                 queryText: queryText,
                 requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(partitionKey) });
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<dynamic> feedResponse = await feedIterator.ReadNextAsync();
                _ = feedResponse.RequestCharge;
            }
            // </GetRequestCharge>
        }
    }
}