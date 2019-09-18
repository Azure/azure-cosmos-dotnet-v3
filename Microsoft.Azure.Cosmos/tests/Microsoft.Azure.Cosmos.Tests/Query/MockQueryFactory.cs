//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Moq;

    internal static class MockQueryFactory
    {
        public static readonly int[] EmptyPage = new int[] { };
        public static readonly string DefaultDatabaseRid = ResourceId.NewDatabaseId(3810641).ToString();
        public static readonly string DefaultCollectionRid = ResourceId.NewDocumentCollectionId(DefaultDatabaseRid, 1376573569).ToString();
        public static readonly SqlQuerySpec DefaultQuerySpec = new SqlQuerySpec("SELECT * FROM C ");
        public static readonly CancellationToken DefaultCancellationToken = new CancellationTokenSource().Token;
        public static readonly Uri DefaultResourceLink = new Uri("dbs/MockDb/colls/MockQueryFactoryDefault", UriKind.Relative);
        public static readonly PartitionKeyRange DefaultPartitionKeyRange = new PartitionKeyRange() { MinInclusive = "", MaxExclusive = "FF", Id = "0" };
        public static readonly PartitionKeyRange DefaultPartitionKeyRange1AfterSplit = new PartitionKeyRange() { MinInclusive = "", MaxExclusive = "B", Id = "1" };
        public static readonly PartitionKeyRange DefaultPartitionKeyRange2AfterSplit = new PartitionKeyRange() { MinInclusive = "B", MaxExclusive = "FF", Id = "2" };
        public static readonly IReadOnlyList<PartitionKeyRange> DefaultPartitionKeyRangesAfterSplit = new List<PartitionKeyRange>()
        {
            DefaultPartitionKeyRange1AfterSplit,
            DefaultPartitionKeyRange2AfterSplit,
        }.AsReadOnly();

        public static IList<ToDoItem> GenerateAndMockResponse(
           Mock<CosmosQueryClient> mockQueryClient,
           bool isOrderByQuery,
           SqlQuerySpec sqlQuerySpec,
           string containerRid,
           string initContinuationToken,
           int maxPageSize,
           MockPartitionResponse[] mockResponseForSinglePartition,
           CancellationToken cancellationTokenForMocks)
        {
            // Get the total item count
            int totalItemCount = 0;
            foreach (MockPartitionResponse response in mockResponseForSinglePartition)
            {
                totalItemCount += response.GetTotalItemCount();
            }

            // Create all the items and order by id
            IList<ToDoItem> allItemsOrdered = ToDoItem.CreateItems(
                totalItemCount,
                "MockQueryFactory",
                containerRid).OrderBy(item => item.id).ToList();

            GenerateAndMockResponseHelper(
                mockQueryClient: mockQueryClient,
                allItemsOrdered: allItemsOrdered,
                isOrderByQuery: isOrderByQuery,
                sqlQuerySpec: sqlQuerySpec,
                containerRid: containerRid,
                initContinuationToken: initContinuationToken,
                maxPageSize: maxPageSize,
                mockResponseForSinglePartition: mockResponseForSinglePartition,
                cancellationTokenForMocks: cancellationTokenForMocks);

            return allItemsOrdered;
        }

        private static IList<ToDoItem> GenerateAndMockResponseHelper(
             Mock<CosmosQueryClient> mockQueryClient,
             IList<ToDoItem> allItemsOrdered,
             bool isOrderByQuery,
             SqlQuerySpec sqlQuerySpec,
             string containerRid,
             string initContinuationToken,
             int maxPageSize,
             MockPartitionResponse[] mockResponseForSinglePartition,
             CancellationToken cancellationTokenForMocks)
        {
            if (mockResponseForSinglePartition == null)
            {
                throw new ArgumentNullException(nameof(mockResponseForSinglePartition));
            }

            // Loop through all the partitions
            foreach (MockPartitionResponse partitionAndMessages in mockResponseForSinglePartition)
            {
                PartitionKeyRange partitionKeyRange = partitionAndMessages.PartitionKeyRange;

                string previousContinuationToken = initContinuationToken;

                // Loop through each message inside the partition
                List<int[]> messages = partitionAndMessages.MessagesWithItemIndex;
                int messagesCount = messages == null ? 0 : messages.Count;
                int lastMessageIndex = messagesCount - 1;
                for (int i = 0; i < messagesCount; i++)
                {
                    int[] message = partitionAndMessages.MessagesWithItemIndex[i];

                    string newContinuationToken = null;

                    List<ToDoItem> currentPageItems = new List<ToDoItem>();
                    // Null represents an empty page
                    if (message != null)
                    {
                        foreach (int itemPosition in message)
                        {
                            currentPageItems.Add(allItemsOrdered[itemPosition]);
                        }
                    }

                    // Last message should have null continuation token
                    // Split means it's not the last message for this PK range
                    if (i != lastMessageIndex || partitionAndMessages.HasSplit)
                    {
                        newContinuationToken = Guid.NewGuid().ToString();
                    }

                    QueryResponseCore queryResponse = QueryResponseMessageFactory.CreateQueryResponse(
                        currentPageItems,
                        isOrderByQuery,
                        newContinuationToken,
                        containerRid);

                    mockQueryClient.Setup(x =>
                      x.ExecuteItemQueryAsync(
                          It.IsAny<Uri>(),
                          ResourceType.Document,
                          OperationType.Query,
                          It.IsAny<QueryRequestOptions>(),
                          It.Is<SqlQuerySpec>(specInput => MockItemProducerFactory.IsSqlQuerySpecEqual(sqlQuerySpec, specInput)),
                          previousContinuationToken,
                          It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, containerRid)),
                          It.IsAny<bool>(),
                          maxPageSize,
                          cancellationTokenForMocks))
                          .Returns(Task.FromResult(queryResponse));

                    previousContinuationToken = newContinuationToken;
                }

                if (partitionAndMessages.HasSplit)
                {
                    QueryResponseCore querySplitResponse = QueryResponseMessageFactory.CreateSplitResponse(containerRid);

                    mockQueryClient.Setup(x =>
                            x.TryGetOverlappingRangesAsync(
                                containerRid,
                                It.Is<Documents.Routing.Range<string>>(inputRange => inputRange.Equals(partitionKeyRange.ToRange())),
                                true)).Returns(Task.FromResult(partitionAndMessages.GetPartitionKeyRangeOfSplit()));

                    mockQueryClient.Setup(x =>
                     x.ExecuteItemQueryAsync(
                         It.IsAny<Uri>(),
                         ResourceType.Document,
                         OperationType.Query,
                         It.IsAny<QueryRequestOptions>(),
                         It.Is<SqlQuerySpec>(specInput => MockItemProducerFactory.IsSqlQuerySpecEqual(sqlQuerySpec, specInput)),
                         previousContinuationToken,
                         It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, containerRid)),
                         It.IsAny<bool>(),
                         maxPageSize,
                         cancellationTokenForMocks))
                         .Returns(Task.FromResult(querySplitResponse));

                    GenerateAndMockResponseHelper(
                       mockQueryClient: mockQueryClient,
                       allItemsOrdered: allItemsOrdered,
                       isOrderByQuery: isOrderByQuery,
                       sqlQuerySpec: sqlQuerySpec,
                       containerRid: containerRid,
                       initContinuationToken: previousContinuationToken,
                       maxPageSize: maxPageSize,
                       mockResponseForSinglePartition: partitionAndMessages.Split,
                       cancellationTokenForMocks: cancellationTokenForMocks);
                }
            }

            return allItemsOrdered;
        }

        public static CosmosQueryContext CreateContext(
            CosmosQueryClient cosmosQueryClient)
        {
            return new CosmosQueryContextCore(
                client: cosmosQueryClient,
                queryRequestOptions: null,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(ToDoItem),
                resourceLink: DefaultResourceLink,
                correlatedActivityId: Guid.NewGuid(),
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                enableGroupBy: true,
                containerResourceId: DefaultCollectionRid);
        }

        public static MockPartitionResponse[] CreateDefaultResponse(
            int[] message1,
            int[] message2 = null,
            int[] message3 = null)
        {
            List<int[]> messages = new List<int[]>();
            if (message1 != null)
            {
                messages.Add(message1);
            }

            if (message2 != null)
            {
                messages.Add(message2);
            }

            if (message3 != null)
            {
                messages.Add(message3);
            }

            return CreateDefaultResponse(messages);
        }

        public static MockPartitionResponse[] CreateDefaultResponse(
            List<int[]> messages)
        {
            return new MockPartitionResponse[] {
                new MockPartitionResponse()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    MessagesWithItemIndex = messages,
                }
            };
        }

        public static MockPartitionResponse[] CreateDefaultSplit(
            List<int[]> beforeSplit,
            List<int[]> split1,
            List<int[]> split2)
        {
            return new MockPartitionResponse[] {
                new MockPartitionResponse()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    MessagesWithItemIndex = beforeSplit,
                    Split = new MockPartitionResponse[]
                    {
                        new MockPartitionResponse()
                        {
                            PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                            MessagesWithItemIndex = split1
                        },
                        new MockPartitionResponse()
                        {
                            PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                            MessagesWithItemIndex = split2,
                        }
                    }
                }
            };
        }

        public static List<MockPartitionResponse[]> GetSplitScenarios()
        {
            List<MockPartitionResponse[]> allSplitScenario = new List<MockPartitionResponse[]>();
            List<int[]> EmptyPage = new List<int[]>()
            {
                MockQueryFactory.EmptyPage
            };

            List<int[]> Item0 = new List<int[]>()
            {
                new int[] {0}
            };

            List<int[]> Item1 = new List<int[]>()
            {
                new int[] {1}
            };

            // Create combination of first page empty then split or starts with split
            List<List<int[]>> firstPartitionOptions = new List<List<int[]>>()
            {
                EmptyPage,
                null,
                new List<int[]>(){ new int[] {0, 1} }
            };

            foreach (List<int[]> firstPartition in firstPartitionOptions)
            {
                int itemCount = 0;
                if (firstPartition != null)
                {
                    foreach (int[] items in firstPartition)
                    {
                        itemCount += items.Length;
                    }
                }

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    EmptyPage,
                    EmptyPage));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    EmptyPage,
                    new List<int[]>()
                    {
                        new int[] { itemCount }
                    }));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    new List<int[]>()
                    {
                        new int[] { itemCount }
                    },
                    EmptyPage));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    new List<int[]>()
                    {
                        new int[] { itemCount }
                    },
                    new List<int[]>()
                    {
                        new int[] { itemCount + 1}
                    }));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    new List<int[]>()
                    {
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount }
                    },
                    EmptyPage));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    EmptyPage,
                    new List<int[]>()
                    {
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount }
                    }));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    EmptyPage,
                    new List<int[]>()
                    {
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount },
                        new int[] { itemCount + 1 },
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount + 2 },
                    }));

                allSplitScenario.Add(CreateDefaultSplit(
                    firstPartition,
                    new List<int[]>()
                    {
                        new int[] { itemCount },
                        new int[] { itemCount + 2 },
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount + 4 },
                    },
                    new List<int[]>()
                    {
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount + 1 },
                        new int[] { itemCount + 3 },
                        MockQueryFactory.EmptyPage,
                        new int[] { itemCount + 5 },
                    }));
            }

            return allSplitScenario;
        }

        public static List<MockPartitionResponse[]> GetAllCombinationWithEmptyPage()
        {
            return new List<MockPartitionResponse[]>
            {
                CreateDefaultResponse(
                    MockQueryFactory.EmptyPage),
                CreateDefaultResponse(
                    new int[] { 0 }),
                CreateDefaultResponse(
                    MockQueryFactory.EmptyPage,
                    new int[] { 0 }),
                CreateDefaultResponse(
                    new int[] { 0 },
                    MockQueryFactory.EmptyPage),
                CreateDefaultResponse(
                    new int[] { 0 },
                    new int[] { 1 }),
                CreateDefaultResponse(
                    new int[] { 0 },
                    MockQueryFactory.EmptyPage,
                    MockQueryFactory.EmptyPage),
                CreateDefaultResponse(
                    MockQueryFactory.EmptyPage,
                    new int[] { 0 },
                    MockQueryFactory.EmptyPage),
                CreateDefaultResponse(
                    MockQueryFactory.EmptyPage,
                    MockQueryFactory.EmptyPage,
                    new int[] { 0 }),
                CreateDefaultResponse(
                    new int[] { 0 },
                    new int[] { 1 },
                    MockQueryFactory.EmptyPage),
                CreateDefaultResponse(
                    MockQueryFactory.EmptyPage,
                    new int[] { 0 },
                    new int[] { 1 }),
                CreateDefaultResponse(
                    new int[] { 0 },
                    MockQueryFactory.EmptyPage,
                    new int[] { 1 }),
                CreateDefaultResponse(
                    new int[] { 0 },
                    new int[] { 1 },
                    new int[] { 2 }),
            };
        }

    }
}