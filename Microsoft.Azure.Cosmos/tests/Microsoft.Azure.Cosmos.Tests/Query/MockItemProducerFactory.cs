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
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    internal static class MockItemProducerFactory
    {
        public const string DefaultCollectionRid = "MockDefaultCollectionRid";
        public static readonly IReadOnlyList<int> Dataset = Enumerable.Range(1, 1000).ToList();
        public static readonly SqlQuerySpec DefaultQuerySpec = new SqlQuerySpec("SELECT * FROM C ");
        public static readonly PartitionKeyRange DefaultPartitionKeyRange = new PartitionKeyRange() { MinInclusive = "A", MaxExclusive = "B", Id = "0" };
        public static readonly int[] DefaultResponseSizes = { 3, 0, 3 };
        public static readonly CancellationToken DefaultCancellationToken = new CancellationTokenSource().Token;
        
        /// <summary>
        /// Create a item producer with a list of responses mocked
        /// </summary>
        /// <param name="responseMessagesPageSize">Each entry represents a response message and the number of items in the response.</param>
        /// <param name="sqlQuerySpec">The query spec the backend should be expecting</param>
        /// <param name="partitionKeyRange">The partition key range</param>
        /// <param name="continuationToken">The initial continuation token.</param>
        /// <param name="maxPageSize">The max page size</param>
        /// <param name="completeDelegate">A delegate that is called when a page is loaded</param>
        /// <param name="responseDelay">Delays the ExecuteQueryAsync response. This allows testing race conditions.</param>
        /// <param name="cancellationToken">The expected continuation token</param>
        public static (ItemProducer itemProducer, ReadOnlyCollection<ToDoItem> allItems) Create(
            int[] responseMessagesPageSize = null,
            SqlQuerySpec sqlQuerySpec = null,
            PartitionKeyRange partitionKeyRange = null,
            string continuationToken = null,
            int maxPageSize = 50,
            ItemProducer.ProduceAsyncCompleteDelegate completeDelegate = null,
            TimeSpan? responseDelay = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (responseMessagesPageSize == null)
            {
                responseMessagesPageSize = DefaultResponseSizes;
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
            mockQueryContext.Setup(x => x.ContainerResourceId).Returns(DefaultCollectionRid);

            // Setup a list of query responses. It generates a new continuation token for each response. This allows the mock to return the messages in the correct order.
            List<ToDoItem> allItems = MockSinglePartitionKeyRangeContext(
                mockQueryContext,
                responseMessagesPageSize,
                sqlQuerySpec,
                partitionKeyRange,
                continuationToken,
                maxPageSize,
                DefaultCollectionRid,
                responseDelay,
                cancellationToken);

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
            int numberOfDocuments,
            double requestCharge,
            QueryMetrics queryMetrics,
            long responseLengthInBytes,
            CancellationToken token)
        {
            return;
        }

        public static (ItemProducerTree itemProducerTree, ReadOnlyCollection<ToDoItem> allItems) CreateTree(
            Mock<CosmosQueryContext> mockQueryContext = null,
            int[] responseMessagesPageSize = null,
            SqlQuerySpec sqlQuerySpec = null,
            PartitionKeyRange partitionKeyRange = null,
            string continuationToken = null,
            int maxPageSize = 50,
            bool deferFirstPage = true,
            string collectionRid = DefaultCollectionRid,
            IComparer<ItemProducerTree> itemProducerTreeComparer = null,
            ItemProducerTree.ProduceAsyncCompleteDelegate completeDelegate = null,
            TimeSpan? responseDelay = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (responseMessagesPageSize == null)
            {
                responseMessagesPageSize = DefaultResponseSizes;
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
                completeDelegate = DefaultTreeProduceAsyncCompleteDelegate;
            }

            if (itemProducerTreeComparer == null)
            {
                itemProducerTreeComparer = new ParallelItemProducerTreeComparer();
            }

            if (mockQueryContext == null)
            {
                mockQueryContext = new Mock<CosmosQueryContext>();
            }

            mockQueryContext.Setup(x => x.ContainerResourceId).Returns(collectionRid);

            // Setup a list of query responses. It generates a new continuation token for each response. This allows the mock to return the messages in the correct order.
            List<ToDoItem> allItems = MockSinglePartitionKeyRangeContext(
                mockQueryContext,
                responseMessagesPageSize,
                sqlQuerySpec,
                partitionKeyRange,
                continuationToken,
                maxPageSize,
                collectionRid,
                responseDelay,
                cancellationToken);

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                mockQueryContext.Object,
                sqlQuerySpec,
                partitionKeyRange,
                completeDelegate,
                itemProducerTreeComparer,
                CosmosElementEqualityComparer.Value,
                deferFirstPage,
                collectionRid,
                maxPageSize,
                initialContinuationToken: continuationToken);

            return (itemProducerTree, allItems.AsReadOnly());
        }

        public static void DefaultTreeProduceAsyncCompleteDelegate(
            ItemProducerTree itemProducerTree,
            int numberOfDocuments,
            double requestCharge,
            QueryMetrics queryMetrics,
            long responseLengthInBytes,
            CancellationToken token)
        {
            return;
        }

        public static List<ToDoItem> MockSinglePartitionKeyRangeContext(
            Mock<CosmosQueryContext> mockQueryContext,
            int[] responseMessagesPageSize,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyRange partitionKeyRange,
            string continuationToken,
            int maxPageSize,
            string collectionRid,
            TimeSpan? responseDelay,
            CancellationToken cancellationToken)
        {
            // Setup a list of query responses. It generates a new continuation token for each response. This allows the mock to return the messages in the correct order.
            List<ToDoItem> allItems = new List<ToDoItem>();
            string previousContinuationToken = continuationToken;
            for (int i = 0; i < responseMessagesPageSize.Length; i++)
            {
                string newContinuationToken = null;

                // The last response should have a null continuation token
                if (i + 1 != responseMessagesPageSize.Length)
                {
                    newContinuationToken = Guid.NewGuid().ToString();
                }

                (QueryResponse response, ReadOnlyCollection<ToDoItem> items) queryResponse = QueryResponseMessageFactory.Create(
                    itemIdPrefix: $"page{i}-pk{partitionKeyRange.Id}-",
                    continuationToken: newContinuationToken,
                    collectionRid: collectionRid,
                    itemCount: responseMessagesPageSize[i]);

                allItems.AddRange(queryResponse.items);

                mockQueryContext.Setup(x =>
                    x.ExecuteQueryAsync(
                        sqlQuerySpec,
                        previousContinuationToken,
                        It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, collectionRid)),
                        It.IsAny<bool>(),
                        maxPageSize,
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

            return allItems;
        }
    }
}
