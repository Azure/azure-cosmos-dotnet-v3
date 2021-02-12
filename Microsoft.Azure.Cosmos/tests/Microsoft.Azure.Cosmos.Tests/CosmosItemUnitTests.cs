//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosItemUnitTests
    {
        [TestMethod]
        public async Task TestItemPartitionKeyTypes()
        {
            dynamic item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };
            await VerifyItemOperations(new Cosmos.PartitionKey(item.pk), "[\"FF627B77-568E-4541-A47E-041EAC10E46F\"]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = 4567,
            };
            await VerifyItemOperations(new Cosmos.PartitionKey(item.pk), "[4567.0]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = 4567.1234,
            };
            await VerifyItemOperations(new Cosmos.PartitionKey(item.pk), "[4567.1234]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = true,
            };
            await VerifyItemOperations(new Cosmos.PartitionKey(item.pk), "[true]", item);
        }

        [TestMethod]
        public async Task TestNullItemPartitionKeyFlag()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            await VerifyItemOperations(new Cosmos.PartitionKey(Undefined.Value), "[{}]", testItem);
        }

        [TestMethod]
        public async Task TestNullItemPartitionKeyBehavior()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            await VerifyItemNullPartitionKeyExpectations(testItem, null);

            ItemRequestOptions requestOptions = new ItemRequestOptions();
            await VerifyItemNullPartitionKeyExpectations(testItem, requestOptions);
        }

        [TestMethod]
        public async Task TestGetPartitionKeyValueFromStreamAsync()
        {
            ContainerInternal mockContainer = (ContainerInternal)MockCosmosUtil.CreateMockCosmosClient().GetContainer("TestDb", "Test");
            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            ContainerInternal container = containerMock.Object;

            containerMock.Setup(e => e.GetPartitionKeyPathTokensAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>> { new List<string> { "pk" } }));
            containerMock
                .Setup(
                    x => x.GetPartitionKeyValueFromStreamAsync(
                        It.IsAny<Stream>(), 
                        It.IsAny<ITrace>(), 
                        It.IsAny<CancellationToken>()))
                .Returns<Stream, ITrace, CancellationToken>(
                (stream, trace, cancellationToken) => mockContainer.GetPartitionKeyValueFromStreamAsync(
                    stream,
                    trace, 
                    cancellationToken));

            DateTime dateTime = new DateTime(2019, 05, 15, 12, 1, 2, 3, DateTimeKind.Utc);
            Guid guid = Guid.NewGuid();

            //Test supported types
            List<dynamic> supportedTypesToTest = new List<dynamic> {
                new { pk = true },
                new { pk = false },
                new { pk = byte.MaxValue },
                new { pk = sbyte.MaxValue },
                new { pk = short.MaxValue },
                new { pk = ushort.MaxValue },
                new { pk = int.MaxValue },
                new { pk = uint.MaxValue },
                new { pk = long.MaxValue },
                new { pk = ulong.MaxValue },
                new { pk = float.MaxValue },
                new { pk = double.MaxValue },
                new { pk = decimal.MaxValue },
                new { pk = char.MaxValue },
                new { pk = "test" },
                new { pk = dateTime },
                new { pk = guid },
            };

            foreach (dynamic poco in supportedTypesToTest)
            {
                object pk = await container.GetPartitionKeyValueFromStreamAsync(
                    MockCosmosUtil.Serializer.ToStream(poco),
                    NoOpTrace.Singleton,
                    default(CancellationToken));
                if (pk is bool boolValue)
                {
                    Assert.AreEqual(poco.pk, boolValue);
                }
                else if (pk is double doubleValue)
                {
                    if (poco.pk is float)
                    {
                        Assert.AreEqual(poco.pk, Convert.ToSingle(pk));
                    }
                    else if (poco.pk is double)
                    {
                        Assert.AreEqual(poco.pk, Convert.ToDouble(pk));
                    }
                    else if (poco.pk is decimal)
                    {
                        Assert.AreEqual(Convert.ToDouble(poco.pk), (double)pk);
                    }
                }
                else if (pk is string stringValue)
                {
                    if (poco.pk is DateTime)
                    {
                        Assert.AreEqual(poco.pk.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), stringValue);
                    }
                    else
                    {
                        Assert.AreEqual(poco.pk.ToString(), (string)pk);
                    }
                }
            }

            //Unsupported types should throw
            List<dynamic> unsupportedTypesToTest = new List<dynamic> {
                new { pk = new { test = "test" } },
                new { pk = new int[]{ 1, 2, 3 } },
                new { pk = new ArraySegment<byte>(new byte[]{ 0 }) },
            };

            foreach (dynamic poco in unsupportedTypesToTest)
            {
                await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await container.GetPartitionKeyValueFromStreamAsync(
                    MockCosmosUtil.Serializer.ToStream(poco),
                    NoOpTrace.Singleton,
                    default(CancellationToken)));
            }

            //null should return null
            object pkValue = await container.GetPartitionKeyValueFromStreamAsync(
                MockCosmosUtil.Serializer.ToStream(new { pk = (object)null }),
                NoOpTrace.Singleton,
                default);
            Assert.AreEqual(Cosmos.PartitionKey.Null, pkValue);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_CreateStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
                Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
                using (ResponseMessage streamResponse = await container.CreateItemStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream))
                {
                    mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_UpsertStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
                Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
                using (ResponseMessage streamResponse = await container.UpsertItemStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream))
                {
                    mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_ReplaceStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
                Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
                using (ResponseMessage streamResponse = await container.ReplaceItemStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    streamPayload: itemStream))
                {
                    mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_ReadStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
                Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
                using (ResponseMessage streamResponse = await container.ReadItemStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id))
                {
                    mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_DeleteStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            using (ResponseMessage streamResponse = await container.DeleteItemStreamAsync(
                partitionKey: partitionKey,
                id: testItem.id))
            {
                mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_PatchStream()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            using (ResponseMessage streamResponse = await container.PatchItemStreamAsync(
                partitionKey: partitionKey,
                id: testItem.id,
                patchOperations: patch))
            {
                mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Create()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(
                testItem,
                partitionKey: partitionKey);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Upsert()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.UpsertItemAsync<dynamic>(
                testItem,
                partitionKey: partitionKey);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Replace()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.ReplaceItemAsync<dynamic>(
                testItem,
                testItem.id);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Read()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.ReadItemAsync<dynamic>(
                id: testItem.id,
                partitionKey: partitionKey);
            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Delete()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.DeleteItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id);

            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Patch()
        {
            (ContainerInternal container, Mock<BatchAsyncContainerExecutor> mockedExecutor) = this.CreateMockBulkCosmosClientContext();

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.PatchItemAsync<dynamic>(
                testItem.id,
                partitionKey,
                patch);

            mockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task PartitionKeyDeleteUnitTest()
        {
            dynamic item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };
            await this.VerifyPartitionKeyDeleteOperation(new Cosmos.PartitionKey(item.pk), "[\"FF627B77-568E-4541-A47E-041EAC10E46F\"]");
        }

        [TestMethod]
        public async Task TestNestedPartitionKeyValueFromStreamAsync()
        {
            ContainerInternal originalContainer = (ContainerInternal)MockCosmosUtil.CreateMockCosmosClient().GetContainer("TestDb", "Test");

            Mock<ContainerCore> mockedContainer = new Mock<ContainerCore>(
                originalContainer.ClientContext,
                (DatabaseInternal)originalContainer.Database,
                originalContainer.Id,
                null)
            {
                CallBase = true
            };

            mockedContainer.Setup(e => e.GetPartitionKeyPathTokensAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>> {new List<string> { "a", "b", "c" }}));

            ContainerInternal containerWithMockPartitionKeyPath = mockedContainer.Object;

            List<dynamic> invalidNestedItems = new List<dynamic>
            {
                new // a/b/d (leaf invalid)
                {
                    id = Guid.NewGuid().ToString(),
                    a = new
                    {
                        b = new
                        {
                            d = "pk1",
                        }
                    }
                },
                new // a/d/c (middle invalid)
                {
                    id = Guid.NewGuid().ToString(),
                    a = new
                    {
                        d = new
                        {
                            c = "pk1",
                        }
                    }
                },
                new // nested/a/b/c (root invalid)
                {
                    id = Guid.NewGuid().ToString(),
                    nested = new
                    {
                        a = new
                        {
                            b = new
                            {
                                c = "pk1",
                            }
                        }
                    }
                },
                new // nested/a/b/c/d (root & tail invalid)
                {
                    id = Guid.NewGuid().ToString(),
                    nested = new
                    {
                        a = new
                        {
                            b = new
                            {
                                c = new
                                {
                                    d = "pk1"
                                }
                            }
                        }
                    }
                }
            };

            foreach (dynamic poco in invalidNestedItems)
            {
                object pk = await containerWithMockPartitionKeyPath.GetPartitionKeyValueFromStreamAsync(
                    MockCosmosUtil.Serializer.ToStream(poco),
                    NoOpTrace.Singleton,
                    default(CancellationToken));
                Assert.IsTrue(object.Equals(Cosmos.PartitionKey.None, pk));
            }
        }

        [TestMethod]
        public async Task TestMultipleNestedPartitionKeyValueFromStreamAsync()
        {
            ContainerInternal originalContainer = (ContainerInternal)MockCosmosUtil.CreateMockCosmosClient().GetContainer("TestDb", "Test");

            Mock<ContainerCore> mockedContainer = new Mock<ContainerCore>(
                originalContainer.ClientContext,
                (DatabaseInternal)originalContainer.Database,
                originalContainer.Id,
                null)
            {
                CallBase = true
            };

            mockedContainer.Setup(e => e.GetPartitionKeyPathTokensAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<IReadOnlyList<string>>) new List<IReadOnlyList<string>> { new List<string> { "a", "b", "c" }, new List<string> { "a","e","f" } }));

            ContainerInternal containerWithMockPartitionKeyPath = mockedContainer.Object;

            List<dynamic> validNestedItems = new List<dynamic>
            {
                (
                    new // a/b/c (Specify only one partition key)
                    {
                        id = Guid.NewGuid().ToString(),
                        a = new
                        {
                            b = new
                            {
                                c = 10,
                            }
                        }
                    },
                    "[10.0,{}]"
                ),
                (
                    new // 
                    {
                        id = Guid.NewGuid().ToString(),
                        a = new
                        {
                            b = new
                            {
                                c = 10,
                            },
                            e = new
                            {
                                f = 15,
                            }
                        }
                    },
                    "[10.0,15.0]"
                ),
                (
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        a = new
                        {
                            b = new
                            {
                                c = 10,
                            },
                            e = new
                            {
                                f = default(string), //null
                            }
                        }
                    },
                    "[10.0,null]"
                ),
                (
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        a = new
                        {
                            e = new
                            {
                                f = 10,
                            }
                        }
                    },
                    "[{},10.0]"
                ),
                (
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        a = new
                        {
                            e = 10,
                            b = new
                            {
                                k = 10,
                            }
                        }

                    },
                    "[{},{}]"
                )
            };

            foreach (dynamic poco in validNestedItems)
            {
                Cosmos.PartitionKey pk = await containerWithMockPartitionKeyPath.GetPartitionKeyValueFromStreamAsync(
                    MockCosmosUtil.Serializer.ToStream(poco.Item1),
                    NoOpTrace.Singleton,
                    default(CancellationToken));
                string partitionKeyString = pk.InternalKey.ToJsonString();
                Assert.AreEqual(poco.Item2, partitionKeyString);
            }

        }

        private (ContainerInternal, Mock<BatchAsyncContainerExecutor>) CreateMockBulkCosmosClientContext()
        {
            CosmosClientContext context = MockCosmosUtil.CreateMockCosmosClient(
                builder => builder.WithBulkExecution(true)).ClientContext;
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((x, y, z) => context.CreateLink(x, y, z));
            mockContext.Setup(x => x.ClientOptions).Returns(context.ClientOptions);
            mockContext.Setup(x => x.ResponseFactory).Returns(context.ResponseFactory);
            mockContext.Setup(x => x.SerializerCore).Returns(context.SerializerCore);
            mockContext.Setup(x => x.DocumentClient).Returns(context.DocumentClient);

            mockContext.Setup(x => x.OperationHelperAsync<ResponseMessage>(
                It.IsAny<string>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<ResponseMessage>>>()))
               .Returns<string, RequestOptions, Func<ITrace, Task<ResponseMessage>>>(
                (operationName, requestOptions, func) => func(NoOpTrace.Singleton));

            mockContext.Setup(x => x.OperationHelperAsync<ItemResponse<dynamic>>(
                It.IsAny<string>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<ItemResponse<dynamic>>>>()))
               .Returns<string, RequestOptions, Func<ITrace, Task<ItemResponse<dynamic>>>>(
                (operationName, requestOptions, func) => func(NoOpTrace.Singleton));

            mockContext.Setup(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerInternal>(),
                It.IsAny<Cosmos.PartitionKey?>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                (uri, resourceType, operationType, requestOptions, containerInternal, pk, itemId, stream, requestEnricher, trace, cancellationToken) =>
                 context.ProcessResourceOperationStreamAsync(
                     uri,
                     resourceType,
                     operationType,
                     requestOptions,
                     containerInternal,
                     pk,
                     itemId,
                     stream,
                     requestEnricher,
                     trace,
                     cancellationToken));

            Mock<BatchAsyncContainerExecutor> mockedExecutor = this.GetMockedBatchExcecutor();
            mockContext.Setup(x => x.GetExecutorForContainer(It.IsAny<ContainerInternal>())).Returns(mockedExecutor.Object);

            DatabaseInternal db = new DatabaseInlineCore(mockContext.Object, "test");
            ContainerInternal container = new ContainerInlineCore(mockContext.Object, db, "test");

            return (container, mockedExecutor);
        }

        private async Task VerifyItemNullPartitionKeyExpectations(
            dynamic testItem,
            ItemRequestOptions requestOptions = null)
        {
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.IsNotNull(request.Headers.PartitionKey);
                JToken.Parse(Documents.Routing.PartitionKeyInternal.Undefined.ToString());
                Assert.IsTrue(new JTokenEqualityComparer().Equals(
                        JToken.Parse(Documents.Routing.PartitionKeyInternal.Undefined.ToString()),
                        JToken.Parse(request.Headers.PartitionKey)),
                        "Arguments {0} {1} ", Documents.Routing.PartitionKeyInternal.Undefined.ToString(), request.Headers.PartitionKey);

                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK));
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler));

            Container container = client.GetDatabase("testdb")
                                        .GetContainer("testcontainer");

            await container.CreateItemAsync<dynamic>(
                    item: testItem,
                    requestOptions: requestOptions);

            await container.UpsertItemAsync<dynamic>(
                    item: testItem,
                    requestOptions: requestOptions);

            await container.ReplaceItemAsync<dynamic>(
                    id: testItem.id,
                    item: testItem,
                    requestOptions: requestOptions);
        }

        private async Task VerifyItemOperations(
            Cosmos.PartitionKey partitionKey,
            string partitionKeySerialized,
            dynamic testItem,
            ItemRequestOptions requestOptions = null)
        {
            ResponseMessage response = null;
            HttpStatusCode httpStatusCode = HttpStatusCode.OK;
            int testHandlerHitCount = 0;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.IsTrue(request.RequestUri.OriginalString.StartsWith(@"dbs/testdb/colls/testcontainer"));
                Assert.AreEqual(requestOptions, request.RequestOptions);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.IsNotNull(request.Headers.PartitionKey);
                Assert.AreEqual(partitionKeySerialized, request.Headers.PartitionKey);
                testHandlerHitCount++;
                response = new ResponseMessage(httpStatusCode, request, errorMessage: null)
                {
                    Content = request.Content
                };
                return Task.FromResult(response);
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(testHandler));

            Container container = client.GetDatabase("testdb")
                                        .GetContainer("testcontainer");

            ItemResponse<dynamic> itemResponse = await container.CreateItemAsync<dynamic>(
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.ReadItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.UpsertItemAsync<dynamic>(
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.ReplaceItemAsync<dynamic>(
                id: testItem.id,
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.DeleteItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            Assert.AreEqual(5, testHandlerHitCount, "An operation did not make it to the handler");

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                using (ResponseMessage streamResponse = await container.CreateItemStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                using (ResponseMessage streamResponse = await container.ReadItemStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                using (ResponseMessage streamResponse = await container.UpsertItemStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                using (ResponseMessage streamResponse = await container.ReplaceItemStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    streamPayload: itemStream,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem))
            {
                using (ResponseMessage streamResponse = await container.DeleteItemStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            Assert.AreEqual(10, testHandlerHitCount, "A stream operation did not make it to the handler");
        }

        private async Task VerifyPartitionKeyDeleteOperation(
            Cosmos.PartitionKey partitionKey,
            string partitionKeySerialized,
            RequestOptions requestOptions = null)
        {
            ResponseMessage response = null;
            HttpStatusCode httpStatusCode = HttpStatusCode.OK;
            int testHandlerHitCount = 0;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.IsTrue(request.RequestUri.OriginalString.StartsWith(@"dbs/testdb/colls/testcontainer"));
                Assert.AreEqual(requestOptions, request.RequestOptions);
                Assert.AreEqual(ResourceType.PartitionKey, request.ResourceType);
                Assert.IsNotNull(request.Headers.PartitionKey);
                Assert.AreEqual(partitionKeySerialized, request.Headers.PartitionKey);
                testHandlerHitCount++;
                response = new ResponseMessage(httpStatusCode, request, errorMessage: null);
                response.Content = request.Content;
                return Task.FromResult(response);
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(testHandler));

            Container container = client.GetDatabase("testdb")
                                        .GetContainer("testcontainer");

            ContainerInternal containerInternal = (ContainerInternal)container;
            ResponseMessage responseMessage = await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(
                partitionKey: partitionKey,
                requestOptions: requestOptions);
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(httpStatusCode, responseMessage.StatusCode);
            Assert.AreEqual(1, testHandlerHitCount, "The operation did not make it to the handler");
        }

        private Mock<BatchAsyncContainerExecutor> GetMockedBatchExcecutor()
        {
            Mock<BatchAsyncContainerExecutor> mockedExecutor = new Mock<BatchAsyncContainerExecutor>();

            mockedExecutor
                .Setup(e => e.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionalBatchOperationResult(HttpStatusCode.OK)
                {
                });

            return mockedExecutor;
        }
    }
}
