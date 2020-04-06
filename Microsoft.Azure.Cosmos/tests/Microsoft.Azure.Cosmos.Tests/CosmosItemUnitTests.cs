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
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
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
            Mock<ContainerCore> containerMock = new Mock<ContainerCore>();
            ContainerCore container = containerMock.Object;

            containerMock.Setup(e => e.GetPartitionKeyPathTokensAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new string[] { "pk" }));

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
            
            foreach(dynamic poco in supportedTypesToTest)
            {
                object pk = await container.GetPartitionKeyValueFromStreamAsync(MockCosmosUtil.Serializer.ToStream(poco));
                if(pk is bool)
                {
                    Assert.AreEqual(poco.pk, (bool)pk);
                }
                else if (pk is double)
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
                else if (pk is string)
                {
                    if(poco.pk is DateTime)
                    {
                        Assert.AreEqual(poco.pk.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), (string)pk);
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

            foreach(dynamic poco in unsupportedTypesToTest)
            {                   
                await Assert.ThrowsExceptionAsync<ArgumentException>(async () => {
                    await container.GetPartitionKeyValueFromStreamAsync(MockCosmosUtil.Serializer.ToStream(poco));
                });
            }

            //null should return null
            object pkValue = await container.GetPartitionKeyValueFromStreamAsync(MockCosmosUtil.Serializer.ToStream(new { pk = (object)null }));
            Assert.AreEqual(Cosmos.PartitionKey.Null, pkValue);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_CreateStream()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

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
                    container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_UpsertStream()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

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
                    container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_ReplaceStream()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

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
                    container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_ReadStream()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

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
                    container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_DeleteStream()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

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
                container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Create()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(
                testItem,
                partitionKey: partitionKey);
            container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Upsert()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.UpsertItemAsync<dynamic>(
                testItem,
                partitionKey: partitionKey);
            container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Replace()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.ReplaceItemAsync<dynamic>(
                testItem,
                testItem.id);
            container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Read()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.ReadItemAsync<dynamic>(
                id: testItem.id,
                partitionKey: partitionKey);
            container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AllowBatchingRequestsSendsToExecutor_Delete()
        {
            CosmosClientContext cosmosClientContext = this.CreateMockBulkCosmosClientContext();

            DatabaseInlineCore db = new DatabaseInlineCore(cosmosClientContext, "test");
            ExecutorContainerCore container = new ExecutorContainerCore(cosmosClientContext, db, "test");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
            };

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(testItem.pk);
            ItemResponse<dynamic> response = await container.DeleteItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id);

            container.MockedExecutor.Verify(c => c.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task TestNestedPartitionKeyValueFromStreamAsync()
        {
            Mock<ContainerCore> containerMock = new Mock<ContainerCore>();
            ContainerCore container = containerMock.Object;

            containerMock.Setup(e => e.GetPartitionKeyPathTokensAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new string[] { "a", "b", "c" }));

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
                object pk = await container.GetPartitionKeyValueFromStreamAsync(MockCosmosUtil.Serializer.ToStream(poco));
                Assert.IsTrue(object.ReferenceEquals(Cosmos.PartitionKey.None, pk) || object.Equals(Cosmos.PartitionKey.None, pk));
            }
        }

        private CosmosClientContext CreateMockBulkCosmosClientContext()
        {
            CosmosClient cosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            return ClientContextCore.Create(
               cosmosClient.Endpoint,
               new MockDocumentClient(),
               new CosmosClientOptions() { AllowBulkExecution = true });
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

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
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
                response = new ResponseMessage(httpStatusCode, request, errorMessage: null);
                response.Content = request.Content;
                return Task.FromResult(response);
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
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

        private class ExecutorContainerCore : ContainerCore
        {
            public readonly Mock<BatchAsyncContainerExecutor> MockedExecutor = new Mock<BatchAsyncContainerExecutor>();
            public ExecutorContainerCore(
                CosmosClientContext clientContext,
                DatabaseInlineCore database,
                string containerId) : base (clientContext, database, containerId)
            {
                this.MockedExecutor
                    .Setup(e => e.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TransactionalBatchOperationResult(HttpStatusCode.OK)
                    {
                       DiagnosticsContext = new CosmosDiagnosticsContextCore()
                    });
            }

            internal override BatchAsyncContainerExecutor InitializeBatchExecutorForContainer() => this.MockedExecutor.Object;
        }

        private class ExecutorWithThrottlingContainerCore : ContainerCore
        {
            public readonly Mock<BatchAsyncContainerExecutor> MockedExecutor = new Mock<BatchAsyncContainerExecutor>();
            public ExecutorWithThrottlingContainerCore(
                CosmosClientContext clientContext,
                DatabaseInlineCore database,
                string containerId) : base(clientContext, database, containerId)
            {
                this.MockedExecutor
                    .SetupSequence(e => e.AddAsync(It.IsAny<ItemBatchOperation>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TransactionalBatchOperationResult((HttpStatusCode) StatusCodes.TooManyRequests))
                    .ReturnsAsync(new TransactionalBatchOperationResult(HttpStatusCode.OK));
            }

            internal override BatchAsyncContainerExecutor InitializeBatchExecutorForContainer() => this.MockedExecutor.Object;
        }
    }
}
