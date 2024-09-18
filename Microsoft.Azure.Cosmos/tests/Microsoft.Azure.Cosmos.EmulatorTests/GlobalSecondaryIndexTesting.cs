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
        [DataRow(ConnectionMode.Gateway, true)]
        [DataRow(ConnectionMode.Direct, true)]
        [DataRow(ConnectionMode.Gateway, false)]
        [DataRow(ConnectionMode.Direct, false)]
        public async Task QueryOperationCrossPartitionTest(ConnectionMode mode, bool isGsiEnabled)
        {
            ReaderInterface.partitionKeyRanges.Clear();
            Environment.SetEnvironmentVariable("GSI_ENABLED", Convert.ToString(isGsiEnabled));

            /*AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            TracerProvider traceProvider = Sdk.CreateTracerProviderBuilder()
             .AddConsoleExporter()
            // .AddHttpClientInstrumentation()
             .AddSource("*")
             .Build();*/

            ContainerInternal itemsCore = (ContainerInternal)await this.CreateClientAndContainer(
                mode: mode,
                isLargeContainer: true);

            // Verify container has multiple partitions
            DocumentFeedResponse<Documents.PartitionKeyRange> pkRanges = await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri);
            Console.WriteLine("Number of partitions: " + pkRanges.Count);
            
            List<Documents.PartitionKeyRange> pkRangesList = new List<Documents.PartitionKeyRange>();
            foreach (Documents.PartitionKeyRange pkRange in pkRanges)
            {
                pkRangesList.Add(pkRange);
                break;
            }
            Console.WriteLine("Writing to GSI Count: " + pkRangesList.Count);
            ReaderInterface.partitionKeyRanges.Add("CreateRandomToDoActivity", pkRangesList);

            Assert.IsTrue(pkRanges.Count > 1, "Should have created a multi partition container.");

            Container container = (Container)itemsCore;

            await ToDoActivity.CreateRandomItems(
                container: container,
                pkCount: 2,
                perPKItemCount: 5);

            string sqlQueryText = "SELECT * FROM c WHERE c.description = @param1";

            List<object> families = new List<object>();

            double totalRequestCharge = 0;
            double totalTimeToGetTheResults = 0;

            Console.WriteLine("====> IS GSI ENABLED " + ConfigurationManager.GetEnvironmentVariable<bool>("GSI_ENABLED", false));
            QueryDefinition queryDefinition = new (sqlQueryText);
            int counter = 1;
            using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(queryDefinition.WithParameter("@param1", "CreateRandomToDoActivity")))
            {
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                    }

                    Console.WriteLine("Page " + counter++ + " Request Charge : " + currentResultSet.RequestCharge + " Latency(ms) : " + currentResultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds);
                    totalRequestCharge += currentResultSet.RequestCharge;
                    totalTimeToGetTheResults += currentResultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds;
                }
            }

            Console.WriteLine("Total Request Charge: " + totalRequestCharge);
            Console.WriteLine("Total Latency: " + totalTimeToGetTheResults);

            // traceProvider.Dispose();

            await Task.Delay(2000);

            await this.database.DeleteStreamAsync();
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
                   // DisableDistributedTracing = false,
                });

            this.SetClient(mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync("gsidatabase");

            return await this.database.CreateContainerAsync(
                id: "gsicontainer",
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);

        }
    }
}
