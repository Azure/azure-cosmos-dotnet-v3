//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;
    using Moq;

    internal static class MockItemProducerFactory
    {
        public static readonly string DefaultDatabaseRid = MockQueryFactory.DefaultDatabaseRid;
        public static readonly string DefaultCollectionRid = MockQueryFactory.DefaultCollectionRid;
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
            Action executeCallback = null,
            CancellationToken cancellationToken = default)
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
                executeCallback,
                cancellationToken);

            ItemProducer itemProducer = new ItemProducer(
                mockQueryContext.Object,
                sqlQuerySpec,
                partitionKeyRange,
                completeDelegate,
                new DefaultCosmosElementEqualityComparer(),
                new TestInjections(simulate429s: false, simulateEmptyPages: false),
                maxPageSize,
                initialContinuationToken: continuationToken);

            return (itemProducer, allItems.AsReadOnly());
        }

        public static void DefaultProduceAsyncCompleteDelegate(
            int numberOfDocuments,
            double requestCharge,
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
            string collectionRid = null,
            IComparer<ItemProducerTree> itemProducerTreeComparer = null,
            ItemProducerTree.ProduceAsyncCompleteDelegate completeDelegate = null,
            Action executeCallback = null,
            CancellationToken cancellationToken = default)
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
                itemProducerTreeComparer = DeterministicParallelItemProducerTreeComparer.Singleton;
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
                executeCallback,
                cancellationToken);

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                mockQueryContext.Object,
                sqlQuerySpec,
                partitionKeyRange,
                completeDelegate,
                itemProducerTreeComparer,
                new DefaultCosmosElementEqualityComparer(),
                new TestInjections(simulate429s: false, simulateEmptyPages: false),
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
            long responseLengthInBytes,
            CancellationToken token)
        {
            return;
        }

        public static List<ToDoItem> MockSinglePartitionKeyRangeContext(
            Mock<CosmosQueryClient> mockQueryContext,
            int[] responseMessagesPageSize,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyRange partitionKeyRange,
            string continuationToken,
            int maxPageSize,
            string collectionRid,
            Action executeCallback,
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

                (QueryResponseCore response, IList<ToDoItem> items) queryResponse = QueryResponseMessageFactory.Create(
                    itemIdPrefix: $"page{i}-pk{partitionKeyRange.Id}-",
                    continuationToken: newContinuationToken,
                    collectionRid: collectionRid,
                    itemCount: responseMessagesPageSize[i]);

                allItems.AddRange(queryResponse.items);

                mockQueryContext.Setup(x =>
                    x.ExecuteItemQueryAsync(
                        It.IsAny<string>(),
                        ResourceType.Document,
                        OperationType.Query,
                        It.IsAny<Guid>(),
                        It.IsAny<QueryRequestOptions>(),
                        It.IsAny<Action<QueryPageDiagnostics>>(),
                        It.Is<SqlQuerySpec>(specInput => IsSqlQuerySpecEqual(sqlQuerySpec, specInput)),
                        previousContinuationToken,
                        It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, collectionRid)),
                        It.IsAny<bool>(),
                        maxPageSize,
                        cancellationToken))
                        .Callback(() => executeCallback?.Invoke())
                        .Returns(Task.FromResult(queryResponse.response));


                if (responseMessagesPageSize[i] != QueryResponseMessageFactory.SPLIT)
                {
                    previousContinuationToken = newContinuationToken;
                }
            }

            return allItems;
        }

        public static List<ToDoItem> MockSinglePartitionKeyRangeContext(
            Mock<CosmosQueryContext> mockQueryContext,
            int[] responseMessagesPageSize,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyRange partitionKeyRange,
            string continuationToken,
            int maxPageSize,
            string collectionRid,
            Action executeCallback,
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

                (QueryResponseCore response, IList<ToDoItem> items) queryResponse = QueryResponseMessageFactory.Create(
                    itemIdPrefix: $"page{i}-pk{partitionKeyRange.Id}-",
                    continuationToken: newContinuationToken,
                    collectionRid: collectionRid,
                    itemCount: responseMessagesPageSize[i]);

                allItems.AddRange(queryResponse.items);

                mockQueryContext.Setup(x =>
                    x.ExecuteQueryAsync(
                        It.Is<SqlQuerySpec>(specInput => IsSqlQuerySpecEqual(sqlQuerySpec, specInput)),
                        previousContinuationToken,
                        It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, collectionRid)),
                        It.IsAny<bool>(),
                        maxPageSize,
                        cancellationToken))
                        .Callback(() => executeCallback?.Invoke())
                        .Returns(Task.FromResult(queryResponse.response));
                previousContinuationToken = newContinuationToken;
            }

            return allItems;
        }

        public static bool IsSqlQuerySpecEqual(SqlQuerySpec expected, SqlQuerySpec actual)
        {
            if (expected == actual || (expected == null && actual == null))
            {
                return true;
            }

            if ((expected != null && actual == null) ||
                (expected == null && actual != null))
            {
                return false;
            }

            if (!string.Equals(expected.QueryText, actual.QueryText))
            {
                return false;
            }

            return IsSqlParameterCollectionsEqual(expected.Parameters, actual.Parameters);
        }

        private static bool IsSqlParameterCollectionsEqual(SqlParameterCollection expected, SqlParameterCollection actual)
        {
            if (expected == actual || (expected == null && actual == null))
            {
                return true;
            }

            if ((expected != null && actual == null) ||
                (expected == null && actual != null) ||
                    (expected.Count != actual.Count))
            {
                return false;
            }

            return expected.SequenceEqual(actual);
        }

        private sealed class DefaultCosmosElementEqualityComparer : IEqualityComparer<CosmosElement>
        {
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                return x == y;
            }

            public int GetHashCode(CosmosElement obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}