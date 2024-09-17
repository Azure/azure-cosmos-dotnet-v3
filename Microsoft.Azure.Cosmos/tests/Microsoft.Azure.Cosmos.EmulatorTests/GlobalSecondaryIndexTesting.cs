//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OpenTelemetry;
    using OpenTelemetry.Trace;

    [TestClass]
    public class GlobalSecondaryIndexTesting : BaseCosmosClientHelper
    {
        protected CosmosClientBuilder cosmosClientBuilder;

        [TestMethod]
        [DataRow(ConnectionMode.Gateway)]
        [DataRow(ConnectionMode.Direct)]
        public async Task QueryOperationCrossPartitionTest(ConnectionMode mode)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            TracerProvider traceProvider = Sdk.CreateTracerProviderBuilder()
             .AddConsoleExporter()
             .AddHttpClientInstrumentation()
             .AddSource("*")
             .Build();

            ContainerInternal itemsCore = (ContainerInternal)await this.CreateClientAndContainer(
                mode: mode,
                isLargeContainer: true);

            // Verify container has multiple partitions
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");

            Container container = (Container)itemsCore;

            await ToDoActivity.CreateRandomItems(
                container: container,
                pkCount: 2,
                perPKItemCount: 5);

            string sqlQueryText = "SELECT * FROM c WHERE c.description = 'CreateRandomToDoActivity'";

            List<object> families = new List<object>();

            double totalRequestCharge = 0;
            double totalTimeToGetTheResults = 0;

            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(queryDefinition))
            {
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                    }

                    totalRequestCharge += currentResultSet.RequestCharge;
                    totalTimeToGetTheResults += currentResultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds;
                }
            }

            Assert.AreEqual(10, families.Count);
            Console.WriteLine(totalRequestCharge);
            Console.WriteLine(totalTimeToGetTheResults);

            traceProvider.Dispose();

            await Task.Delay(2000);
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode,
           Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
           bool isLargeContainer = false)
        {
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();

            if (consistency.HasValue)
            {
                this.cosmosClientBuilder = this.cosmosClientBuilder
                    .WithConsistencyLevel(consistency.Value);
            }

            this.cosmosClientBuilder = this.cosmosClientBuilder
                .WithApplicationName("gsitesting")
                .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    DisableDistributedTracing = false,
                });

            this.SetClient(mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build());

            // Making sure client telemetry is enabled
            Assert.IsNotNull(this.GetClient().DocumentClient.telemetryToServiceHelper);

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);

        }
    }
}
