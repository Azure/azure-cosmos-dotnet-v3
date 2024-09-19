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

    [TestClass]
    public class GlobalSecondaryIndexTesting
    {
        [TestMethod]
        [DataRow(ConnectionMode.Gateway, true)]
        /*[DataRow(ConnectionMode.Gateway, false)]
        [DataRow(ConnectionMode.Direct, true)]
        [DataRow(ConnectionMode.Direct, false)]*/
        public async Task QueryOperationCrossPartitionTest(ConnectionMode mode, bool isGsiEnabled)
        {
            Environment.SetEnvironmentVariable("GSI_ENABLED", Convert.ToString(isGsiEnabled));

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: "",
                authKeyOrResourceToken: "")
                .WithApplicationName("gsitesting")
                .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    // DisableDistributedTracing = false,
                });

            if( mode == ConnectionMode.Gateway)
            {
                cosmosClientBuilder.WithConnectionModeGateway();
            }
            else
            {
                cosmosClientBuilder.WithConnectionModeDirect();
            }

            CosmosClient client = cosmosClientBuilder.Build();

            ContainerInternal containerInternal = (ContainerInternal)client.GetContainer("testdb1", "records100mgsidistinct100m");

            // Verify container has multiple partitions
            DocumentFeedResponse<Documents.PartitionKeyRange> pkRanges = await containerInternal.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(containerInternal.LinkUri);
            Console.WriteLine("Original Number of partitions: " + pkRanges.Count);

            Container container = (Container)containerInternal;

            string sqlQueryText = "SELECT * FROM c WHERE c.emailId1 = @emailId1";

            List<object> families = new List<object>();
            QueryDefinition queryDefinition = new (sqlQueryText);

            int counter = 1;
            double totalRequestCharge = 0;
            double totalTimeToGetTheResults = 0;
            using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(queryDefinition.WithParameter("@emailId1", "7CNLE0O695@outlook.com")))
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
        }
    }
}
