//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    internal static class MockItemProducerFactory
    {
        public static readonly IReadOnlyList<int> Dataset = Enumerable.Range(1, 1000).ToList();
        public static readonly SqlQuerySpec DefaultQuerySpec = new SqlQuerySpec("SELECT * FROM C ");
        public static readonly PartitionKeyRange DefaultPartitionKeyRange = new PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B", Id = "0" };
        public static readonly int[] DefaultResponseSizes = { 3, 0, 3 };

        public static (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) Create(
            int[] responseMessagePageSizes = null,
            SqlQuerySpec sqlQuerySpec = null,
            PartitionKeyRange partitionKeyRange = null,
            string continuationToken = null,
            int maxPageSize = 50,
            ItemProducer.ProduceAsyncCompleteDelegate completeDelegate = null,
            TimeSpan? responseDelay = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if(responseMessagePageSizes == null)
            {
                responseMessagePageSizes = DefaultResponseSizes;
            }

            if (sqlQuerySpec == null)
            {
                sqlQuerySpec = DefaultQuerySpec;
            }

            if (partitionKeyRange == null)
            {
                partitionKeyRange = DefaultPartitionKeyRange;
            }

            if (completeDelegate == null)
            {
                completeDelegate = DefaultProduceAsyncCompleteDelegate;
            }

            Mock<CosmosQueryContext> mockQueryContext = new Mock<CosmosQueryContext>();
            mockQueryContext.Setup(x => x.ContainerResourceId).Returns("ContainerResourceId");

            // Setup a list of query responses. It generates a new continuation token for each response. This allows the mock to return the messages in the correct order.
            List<ToDoItem> allItems = new List<ToDoItem>();
            string previousContinuationToken = continuationToken;
            for (int i = 0; i < responseMessagePageSizes.Length; i++)
            {
                string newContinuationToken = null;

                // The last response should have a null continuation token
                if (i + 1 != responseMessagePageSizes.Length)
                {
                    newContinuationToken = Guid.NewGuid().ToString();
                }

                (QueryResponse response, ReadOnlyCollection<ToDoItem> items) queryResponse = QueryResponseMessageFactory.Create( $"page{i}-", newContinuationToken, responseMessagePageSizes[i]);

                allItems.AddRange(queryResponse.items);

                mockQueryContext.Setup(x =>
                    x.ExecuteQueryAsync(
                        sqlQuerySpec,
                        previousContinuationToken,
                        It.IsAny<PartitionKeyRangeIdentity>(),
                        It.IsAny<bool>(),
                        It.IsAny<int>(),
                        cancellationToken))
                        .Callback(() =>
                        {
                            if (responseDelay.HasValue)
                            {
                                Thread.Sleep(responseDelay.Value);
                            }
                        })
                        .Returns(Task.FromResult(queryResponse.response));
                previousContinuationToken = newContinuationToken;
            }

            ItemProducer itemProducer = new ItemProducer(
                mockQueryContext.Object,
                sqlQuerySpec,
                partitionKeyRange,
                completeDelegate,
                CosmosElementEqualityComparer.Value,
                maxPageSize,
                initialContinuationToken: continuationToken);

            return (itemProducer, allItems.AsReadOnly());
        }

        public static void DefaultProduceAsyncCompleteDelegate(
            ItemProducer producer,
            int numberOfDocuments,
            double requestCharge,
            QueryMetrics queryMetrics,
            long responseLengthInBytes,
            CancellationToken token)
        {
            return;
        }
    }
}
