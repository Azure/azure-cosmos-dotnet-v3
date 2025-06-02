//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal class CreateItemBenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string pk = "pk_benchmark";

        public CreateItemBenchmarkOperation(CosmosClient client, string db, string containerName)
        {
            this.container = client.GetContainer(db, containerName);
        }

        public async Task PrepareAsync() { }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            CosmosIntegrationTestObject item = new CosmosIntegrationTestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = this.pk,
                Other = "Create Test"
            };

            ItemResponse<CosmosIntegrationTestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Failed to create item: {item.Id}");
            }

            return new OperationResult { OperationType = BenchmarkOperationType.Insert };
        }

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Insert;

        public class CosmosIntegrationTestObject
        {
            public string Id { get; set; }
            public string Pk { get; set; }
            public string Other { get; set; }
        }
    }
}
