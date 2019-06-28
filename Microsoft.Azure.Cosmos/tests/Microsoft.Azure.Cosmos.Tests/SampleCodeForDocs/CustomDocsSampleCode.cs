//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    using Microsoft.Azure.Cosmos.Scripts;
    using System.Threading.Tasks;

    class CustomDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                accountKey: "TestKey",
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
            var requestCharge = itemResponse.RequestCharge;

            Scripts scripts = container.Scripts;
            StoredProcedureExecuteResponse<object> sprocResponse = await scripts.ExecuteStoredProcedureAsync<object, object>(
                storedProcedureId: storedProcedureId,
                input: new object(),
                partitionKey: new PartitionKey(partitionKey));
            requestCharge = sprocResponse.RequestCharge;

            FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(
                 queryText: queryText,
                 requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(partitionKey) });
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<dynamic> feedResponse = await feedIterator.ReadNextAsync();
                requestCharge = feedResponse.RequestCharge;
            }
            // </GetRequestCharge>
        }
    }
}
