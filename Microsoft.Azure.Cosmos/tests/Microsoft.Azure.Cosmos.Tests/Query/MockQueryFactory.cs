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
        public static readonly string DefaultDatabaseRid = ResourceId.NewDatabaseId(3810641).ToString();
        public static readonly string DefaultCollectionRid = ResourceId.NewDocumentCollectionId(DefaultDatabaseRid, 1376573569).ToString();
        public static readonly SqlQuerySpec DefaultQuerySpec = new SqlQuerySpec("SELECT * FROM C ");
        public static readonly CancellationToken DefaultCancellationToken = new CancellationTokenSource().Token;
        public static readonly Uri DefaultResourceLink = new Uri("dbs/MockDb/colls/MockQueryFactoryDefault", UriKind.Relative);
        public static readonly PartitionKeyRange DefaultPartitionKeyRange = new PartitionKeyRange() { MinInclusive = "00", MaxExclusive = "FF", Id = "0" };
        public static readonly PartitionKeyRange DefaultPartitionKeyRange1AfterSplit = new PartitionKeyRange() { MinInclusive = "00", MaxExclusive = "B", Id = "1" };
        public static readonly PartitionKeyRange DefaultPartitionKeyRange2AfterSplit = new PartitionKeyRange() { MinInclusive = "B", MaxExclusive = "FF", Id = "2" };
        public static readonly IReadOnlyList<PartitionKeyRange> DefaultPartitionKeyRangesAfterSplit = new List<PartitionKeyRange>()
        {
            DefaultPartitionKeyRange1AfterSplit,
            DefaultPartitionKeyRange2AfterSplit,
        }.AsReadOnly();

        public static IList<ToDoItem> GenerateAndMockResponse(
            Mock<CosmosQueryClient> mockQueryClient,
            SqlQuerySpec sqlQuerySpec,
            string containerRid,
            string initContinuationToken,
            int maxPageSize,
            MockResponseForSinglePartition[] mockResponseForSinglePartition,
            CancellationToken cancellationToken)
        {
            if (mockResponseForSinglePartition == null)
            {
                throw new ArgumentNullException(nameof(mockResponseForSinglePartition));
            }

            // Setup the routing map in case there is a split and the ranges need to be updated
            Mock<IRoutingMapProvider> mockRoutingMap = new Mock<IRoutingMapProvider>();
            mockQueryClient.Setup(x => x.GetRoutingMapProviderAsync()).Returns(Task.FromResult(mockRoutingMap.Object));

            // Get the total item count
            int totalItemCount = 0;
            foreach (MockResponseForSinglePartition response in mockResponseForSinglePartition)
            {
                foreach(var message in response.Messages)
                {
                    int? currentItemCount = message.ItemsIndexPosition?.Length;
                    totalItemCount += currentItemCount ?? 0;
                }
            }

            // Create all the items and order by id
            IList<ToDoItem> allItemsOrdered = ToDoItem.CreateItems(
                totalItemCount,
                "MockQueryFactory",
                containerRid).OrderBy(item => item.id).ToList();

            // Loop through all the partitions
            foreach (MockResponseForSinglePartition partitionAndMessages in mockResponseForSinglePartition)
            {
                PartitionKeyRange partitionKeyRange = partitionAndMessages.PartitionKeyRange;

                string previousContinuationToken = partitionAndMessages.StartingContinuationToken;

                // Loop through each message inside the partition
                MockResponseMessages[] messages = partitionAndMessages.Messages;
                for (int i = 0; i < messages.Length; i++)
                {
                    MockResponseMessages message = partitionAndMessages.Messages[i];
                    QueryResponse queryResponse = null;
                    string newContinuationToken = null;

                    if (message.UpdatedRangesAfterSplit != null)
                    {
                        queryResponse = QueryResponseMessageFactory.CreateSplitResponse(containerRid);
                        mockRoutingMap.Setup(x =>
                            x.TryGetOverlappingRangesAsync(
                                containerRid,
                                It.Is<Documents.Routing.Range<string>>(inputRange => inputRange.Equals(partitionKeyRange.ToRange())),
                                true)).Returns(Task.FromResult(message.UpdatedRangesAfterSplit));
                    }
                    else
                    { 
                        List<ToDoItem> currentPageItems = new List<ToDoItem>();
                        // Null represents an empty page
                        if(message.ItemsIndexPosition != null)
                        {
                            foreach (int itemPosition in message.ItemsIndexPosition)
                            {
                                currentPageItems.Add(allItemsOrdered[itemPosition]);
                            }
                        }

                        // Last message should have null continuation token
                        if (i + 1 != messages.Length)
                        {
                            newContinuationToken = Guid.NewGuid().ToString();
                        }

                        queryResponse = QueryResponseMessageFactory.CreateQueryResponse(
                            currentPageItems,
                            newContinuationToken,
                            containerRid);
                    }

                    mockQueryClient.Setup(x =>
                      x.ExecuteItemQueryAsync(
                          It.IsAny<Uri>(),
                          ResourceType.Document,
                          OperationType.Query,
                          containerRid,
                          It.IsAny<QueryRequestOptions>(),
                          It.Is<SqlQuerySpec>(specInput => MockItemProducerFactory.IsSqlQuerySpecEqual(sqlQuerySpec, specInput)),
                          previousContinuationToken,
                          It.Is<PartitionKeyRangeIdentity>(rangeId => string.Equals(rangeId.PartitionKeyRangeId, partitionKeyRange.Id) && string.Equals(rangeId.CollectionRid, containerRid)),
                          It.IsAny<bool>(),
                          maxPageSize,
                          cancellationToken))
                          .Callback(() =>
                          {
                              if (message.DelayResponse.HasValue)
                              {
                                  Thread.Sleep(message.DelayResponse.Value);
                              }
                          })
                          .Returns(Task.FromResult(queryResponse));

                    previousContinuationToken = newContinuationToken;
                }
            }

            return allItemsOrdered;
        }

        public static CosmosQueryContext CreateContext(
            CosmosQueryClient cosmosQueryClient)
        {
            return new CosmosQueryContext(
                client: cosmosQueryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(ToDoItem),
                sqlQuerySpecFromUser: DefaultQuerySpec,
                queryRequestOptions: new QueryRequestOptions(),
                resourceLink: DefaultResourceLink,
                correlatedActivityId: Guid.NewGuid(),
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                containerResourceId: DefaultCollectionRid);
        }

        public static List<MockResponseForSinglePartition[]> GetSplitScenarios(string initialContinuationToken)
        {
            List<MockResponseForSinglePartition[]> allSplitScenario = new List<MockResponseForSinglePartition[]>();

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(1)
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0),
                        new MockResponseMessages(2)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(1),
                        new MockResponseMessages(3)
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0),
                        new MockResponseMessages(1),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(0),
                        new MockResponseMessages(1),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                        new MockResponseMessages(0),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(1),
                        new MockResponseMessages(2),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                        new MockResponseMessages(0),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                        new MockResponseMessages(1),
                    }
                }
            });

            allSplitScenario.Add(new MockResponseForSinglePartition[]{
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(DefaultPartitionKeyRangesAfterSplit)
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange1AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                        new MockResponseMessages(),
                        new MockResponseMessages(0),
                        new MockResponseMessages(2),
                    }
                },
                new MockResponseForSinglePartition()
                {
                    PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange2AfterSplit,
                    StartingContinuationToken = initialContinuationToken,
                    Messages = new MockResponseMessages[]
                    {
                        new MockResponseMessages(),
                        new MockResponseMessages(1),
                    }
                }
            });

            return allSplitScenario;
        }

        public static MockResponseForSinglePartition GetDefaultMockResponseForSinglePartition(string initialContinuationToken, params MockResponseMessages[] messages)
        {
            return new MockResponseForSinglePartition()
            {
                PartitionKeyRange = MockQueryFactory.DefaultPartitionKeyRange,
                StartingContinuationToken = initialContinuationToken,
                Messages = messages
            };
        }

        public static MockResponseForSinglePartition[] GetAllCombinationWithEmptyPage(string initialContinuationToken = null)
        {
            return new MockResponseForSinglePartition[]
            {
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages()),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(),
                    new MockResponseMessages(0)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0),
                    new MockResponseMessages()),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0),
                    new MockResponseMessages(1)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                     new MockResponseMessages(0),
                    new MockResponseMessages(),
                    new MockResponseMessages()),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(),
                    new MockResponseMessages(0),
                    new MockResponseMessages()),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(),
                    new MockResponseMessages(),
                    new MockResponseMessages(0)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0),
                    new MockResponseMessages(1),
                    new MockResponseMessages()),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0),
                    new MockResponseMessages(),
                    new MockResponseMessages(1)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(),
                    new MockResponseMessages(0),
                    new MockResponseMessages(1)),
                GetDefaultMockResponseForSinglePartition(
                    initialContinuationToken,
                    new MockResponseMessages(0),
                    new MockResponseMessages(1),
                    new MockResponseMessages(2))
            };
        }

    }
}
