//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class HandlerTests
    {

        [TestMethod]
        public void HandlerOrder()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            Type[] types = new Type[]
            {
                typeof(RequestInvokerHandler),
                typeof(DiagnosticsHandler),
                typeof(TelemetryHandler),
                typeof(RetryHandler),
                typeof(RouterHandler)
            };

            RequestHandler handler = client.RequestHandler;
            foreach (Type type in types)
            {
                Assert.IsTrue(type.Equals(handler.GetType()));
                handler = handler.InnerHandler;
            }

            Assert.IsNull(handler);
        }

        [TestMethod]
        public async Task TestPreProcessingHandler()
        {
            RequestHandler preProcessHandler = new PreProcessingTestHandler();
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder.AddCustomHandlers(preProcessHandler));

            Assert.IsTrue(typeof(RequestInvokerHandler).Equals(client.RequestHandler.GetType()));
            Assert.IsTrue(typeof(PreProcessingTestHandler).Equals(client.RequestHandler.InnerHandler.GetType()));

            Container container = client.GetDatabase("testdb")
                                        .GetContainer("testcontainer");

            HttpStatusCode[] testHttpStatusCodes = new HttpStatusCode[]
                                {
                                    HttpStatusCode.OK
                                };

            // User operations
            foreach (HttpStatusCode code in testHttpStatusCodes)
            {
                ItemRequestOptions options = new ItemRequestOptions
                {
                    Properties = new Dictionary<string, object>()
                {
                    { PreProcessingTestHandler.StatusCodeName, code },
                }
                };

                ItemResponse<object> response = await container.ReadItemAsync<object>("id1", new Cosmos.PartitionKey("pk1"), options);
                Console.WriteLine($"Got status code {response.StatusCode}");
                Assert.AreEqual(code, response.StatusCode);
            }

            // Meta-data operations
            foreach (HttpStatusCode code in testHttpStatusCodes)
            {
                ContainerRequestOptions options = new ContainerRequestOptions
                {
                    Properties = new Dictionary<string, object>()
                {
                    { PreProcessingTestHandler.StatusCodeName, code }
                }
                };

                ContainerResponse response = await container.DeleteContainerAsync(options);

                Console.WriteLine($"Got status code {response.StatusCode}");
                Assert.AreEqual(code, response.StatusCode);

            }
        }

        [TestMethod]
        public async Task RequestOptionsHandlerCanHandleRequestOptions()
        {
            const string PropertyKey = "propkey";
            const string Condition = "*";
            object propertyValue = Encoding.UTF8.GetBytes("test");
            RequestOptions options = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>(new List<KeyValuePair<string, object>> {
                    new KeyValuePair<string, object>(PropertyKey, propertyValue)
                }),
                IfMatchEtag = Condition,
            };

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(propertyValue, request.Properties[PropertyKey]);
                Assert.AreEqual(Condition, request.Headers.GetValues(HttpConstants.HttpHeaders.IfMatch).First());
                return TestHandler.ReturnSuccess();
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
            {
                InnerHandler = testHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = options;

            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task RequestOptionsConsistencyLevel()
        {
            List<Cosmos.ConsistencyLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().ToList();
            List<Documents.ConsistencyLevel> documentLevels = Enum.GetValues(typeof(Documents.ConsistencyLevel)).Cast<Documents.ConsistencyLevel>().ToList();
            CollectionAssert.AreEqual(cosmosLevels, documentLevels, new EnumComparer(), "Document consistency level is different from cosmos consistency level");

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(accountConsistencyLevel: Cosmos.ConsistencyLevel.Strong);

            foreach (Cosmos.ConsistencyLevel level in cosmosLevels)
            {
                List<RequestOptions> requestOptions = new List<RequestOptions>
                {
                    new ItemRequestOptions
                    {
                        ConsistencyLevel = level
                    },

                    new QueryRequestOptions
                    {
                        ConsistencyLevel = level
                    },

                    new StoredProcedureRequestOptions
                    {
                        ConsistencyLevel = level
                    }
                };

                foreach (RequestOptions option in requestOptions)
                {
                    TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                    {
                        Assert.AreEqual(level.ToString(), request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
                        return TestHandler.ReturnSuccess();
                    });

                    RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
                    {
                        InnerHandler = testHandler
                    };

                    RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"))
                    {
                        ResourceType = ResourceType.Document
                    };
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
                    requestMessage.OperationType = OperationType.Read;
                    requestMessage.RequestOptions = option;

                    await invoker.SendAsync(requestMessage, new CancellationToken());
                }
            }
        }

        [TestMethod]
        public async Task QueryRequestOptionsDedicatedGatewayRequestOptions()
        {
            TimeSpan maxStaleness = TimeSpan.FromMinutes(5);

            DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
            {
                MaxIntegratedCacheStaleness = maxStaleness
            };

            List<RequestOptions> requestOptions = new List<RequestOptions>
            {
                new ItemRequestOptions
                {
                    DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
                },
                new QueryRequestOptions
                {
                    DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
                },
            };

            foreach (RequestOptions option in requestOptions)
            {
                TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.AreEqual(maxStaleness.TotalMilliseconds.ToString(CultureInfo.InvariantCulture), request.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestCacheStaleness]);

                    return TestHandler.ReturnSuccess();
                });

                using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

                RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
                {
                    InnerHandler = testHandler
                };
                RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
                requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
                requestMessage.ResourceType = ResourceType.Document;
                requestMessage.OperationType = OperationType.Read;
                requestMessage.RequestOptions = option;
                await invoker.SendAsync(requestMessage, new CancellationToken());
            }
        }

        [TestMethod]
        public async Task QueryRequestOptionsSessionToken()
        {
            const string SessionToken = "SessionToken";
            ItemRequestOptions options = new ItemRequestOptions
            {
                SessionToken = SessionToken
            };

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(SessionToken, request.Headers.GetValues(HttpConstants.HttpHeaders.SessionToken).First());
                return TestHandler.ReturnSuccess();
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
            {
                InnerHandler = testHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = options;
            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task ConsistencyLevelClient()
        {
            List<Cosmos.ConsistencyLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().ToList();
            foreach (Cosmos.ConsistencyLevel clientLevel in cosmosLevels)
            {
                using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                   accountConsistencyLevel: Cosmos.ConsistencyLevel.Strong,
                   customizeClientBuilder: builder => builder.WithConsistencyLevel(clientLevel));

                TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.AreEqual(clientLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
                    return TestHandler.ReturnSuccess();
                });

                RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: client.ClientOptions.ConsistencyLevel, requestedClientPriorityLevel: null)
                {
                    InnerHandler = testHandler
                };

                RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"))
                {
                    ResourceType = ResourceType.Document
                };
                requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
                requestMessage.OperationType = OperationType.Read;

                await invoker.SendAsync(requestMessage, new CancellationToken());
            }
        }

        [TestMethod]
        public async Task PriorityLevelClient()
        {
            List<Cosmos.PriorityLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.PriorityLevel)).Cast<Cosmos.PriorityLevel>().ToList();
            foreach (Cosmos.PriorityLevel clientLevel in cosmosLevels)
            {
                using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                   accountConsistencyLevel: null,
                   customizeClientBuilder: builder => builder.WithPriorityLevel(clientLevel));

                TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.AreEqual(clientLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.PriorityLevel]);
                    return TestHandler.ReturnSuccess();
                });

                RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: client.ClientOptions.PriorityLevel)
                {
                    InnerHandler = testHandler
                };

                RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"))
                {
                    ResourceType = ResourceType.Document
                };
                requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
                requestMessage.OperationType = OperationType.Read;

                await invoker.SendAsync(requestMessage, new CancellationToken());
            }
        }

        [TestMethod]
        public async Task TestRequestPriorityLevelTakesPrecedence()
        {
            Cosmos.PriorityLevel clientLevel = Cosmos.PriorityLevel.Low;
            Cosmos.PriorityLevel requestLevel = Cosmos.PriorityLevel.High;

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
               accountConsistencyLevel: null,
               customizeClientBuilder: builder => builder.WithPriorityLevel(clientLevel));

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(requestLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.PriorityLevel]);
                return TestHandler.ReturnSuccess();
            });

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: client.ClientOptions.PriorityLevel)
            {
                InnerHandler = testHandler
            };

            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"))
            {
                ResourceType = ResourceType.Document
            };
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = new RequestOptions
            {
                PriorityLevel = requestLevel
            };

            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task ConsistencyLevelClientAndRequestOption()
        {
            Cosmos.ConsistencyLevel requestOptionLevel = Cosmos.ConsistencyLevel.BoundedStaleness;
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Strong,
                customizeClientBuilder: builder => builder.WithConsistencyLevel(Cosmos.ConsistencyLevel.Eventual));

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(requestOptionLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
                return TestHandler.ReturnSuccess();
            });

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
            {
                InnerHandler = testHandler
            };

            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"))
            {
                ResourceType = ResourceType.Document
            };
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = new ItemRequestOptions() { ConsistencyLevel = requestOptionLevel };

            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task RequestOptionsHandlerCanHandleDataPlaneRequestOptions()
        {
            const string Condition = "*";
            const string SessionToken = "test";
            ItemRequestOptions options = new ItemRequestOptions
            {
                IfNoneMatchEtag = Condition,
                ConsistencyLevel = (Cosmos.ConsistencyLevel)ConsistencyLevel.Eventual,
                SessionToken = SessionToken
            };

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(Condition, request.Headers.GetValues(HttpConstants.HttpHeaders.IfNoneMatch).First());
                Assert.AreEqual(ConsistencyLevel.Eventual.ToString(), request.Headers.GetValues(HttpConstants.HttpHeaders.ConsistencyLevel).First());
                Assert.AreEqual(SessionToken, request.Headers.GetValues(HttpConstants.HttpHeaders.SessionToken).First());
                return TestHandler.ReturnSuccess();
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null, requestedClientPriorityLevel: null)
            {
                InnerHandler = testHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = options;
            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task Test()
        {
            SomePayload t = new SomePayload()
            {
                V1 = "Value1",
                V2 = "Value2",
            };

            JsonSerializer js = new JsonSerializer();
            using (MemoryStream ms = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(ms);
                JsonTextWriter tw = new JsonTextWriter(sw);
                js.Serialize(tw, t);

                ms.Seek(0, SeekOrigin.Begin);

                HttpMethod method = HttpMethod.Get;
                string ep = "https://httpbin.org/put";
                HttpRequestMessage hrm = new HttpRequestMessage(method, ep)
                {
                    Content = new StreamContent(ms)
                };

                for (int i = 0; i < 5; i++)
                {
                    using (MemoryStream msCopy = new MemoryStream())
                    {
                        await hrm.Content.CopyToAsync(msCopy);
                    }
                }
            }
        }

        [TestMethod]
        public void TestAggregateExceptionConverter()
        {
            string errorMessage = "BadRequest message";
            IEnumerable<Exception> exceptions = new List<Exception>()
            {
                new DocumentClientException(errorMessage, innerException: null, statusCode: HttpStatusCode.BadRequest)
            };

            AggregateException ae = new AggregateException(message: "Test AE message", innerExceptions: exceptions);

            ResponseMessage response = TransportHandler.AggregateExceptionConverter(ae, null);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsTrue(response.ErrorMessage.Contains(errorMessage));
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangePartitionKeyAndMultiHashContainerAsync()
        {
            dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98502" };
            Cosmos.PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(item.city)
                .Add(item.state)
                .Build();

            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangeEpk>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Paths = new Collection<string>(new List<string>() { "/city", "/state", "/zipCode" })
                },
                inputFeedRange: new FeedRangePartitionKey(partitionKey),
                expectedFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(
                    min: "01620B162169497AFD85FA66E99F73760845FB119899DE50766A2C4CEFC2FA73",
                    max: "01620B162169497AFD85FA66E99F73760845FB119899DE50766A2C4CEFC2FA73FF",
                    isMinInclusive: true,
                    isMaxInclusive: default)),
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Once());
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangePartitionKeyOnHashContainerAsync()
        {
            dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond" };
            Cosmos.PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(item.city)
                .Build();

            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangePartitionKey>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Paths = new Collection<string>(new List<string>() { "/city" })
                },
                inputFeedRange: new FeedRangePartitionKey(partitionKey),
                expectedFeedRange: new FeedRangePartitionKey(partitionKey),
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Once());
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangePartitionKeyOnRangeContainerAsync()
        {
            dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond" };
            Cosmos.PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(item.city)
                .Build();

            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangePartitionKey>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Range,
                    Paths = new Collection<string>(new List<string>() { "/city" })
                },
                inputFeedRange: new FeedRangePartitionKey(partitionKey),
                expectedFeedRange: new FeedRangePartitionKey(partitionKey),
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Once());
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangeEpkOnMultiHashContainerAsync()
        {
            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangeEpk>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Paths = new Collection<string>(new List<string>() { "/city", "/state", "/zipCode" })
                },
                inputFeedRange: FeedRangeEpk.FullRange,
                expectedFeedRange: FeedRangeEpk.FullRange,
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Never());
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangeEpkOnHashContainerAsync()
        {
            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangeEpk>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Paths = new Collection<string>(new List<string>() { "/city" })
                },
                inputFeedRange: FeedRangeEpk.FullRange,
                expectedFeedRange: FeedRangeEpk.FullRange,
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Never());
        }

        [TestMethod]
        public async Task TestResolveFeedRangeBasedOnPrefixWithFeedRangeEpkOnRangeContainerAsync()
        {
            await HandlerTests.TestResolveFeedRangeBasedOnPrefixAsync<FeedRangeEpk>(
                partitionKeyDefinition: new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Range,
                    Paths = new Collection<string>(new List<string>() { "/city" })
                },
                inputFeedRange: FeedRangeEpk.FullRange,
                expectedFeedRange: FeedRangeEpk.FullRange,
                getPartitionKeyDefinitionAsyncExecutions: Moq.Times.Never());
        }

        private static async Task TestResolveFeedRangeBasedOnPrefixAsync<TFeedRange>(
            PartitionKeyDefinition partitionKeyDefinition,
            FeedRangeInternal inputFeedRange,
            FeedRangeInternal expectedFeedRange,
            Moq.Times getPartitionKeyDefinitionAsyncExecutions)
            where TFeedRange : FeedRangeInternal
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                accountConsistencyLevel: Cosmos.ConsistencyLevel.Strong,
                customizeClientBuilder: builder => builder.WithConsistencyLevel(Cosmos.ConsistencyLevel.Eventual));

            Moq.Mock<ContainerInternal> mockContainer = MockCosmosUtil.CreateMockContainer(
                dbName: Guid.NewGuid().ToString(),
                containerName: Guid.NewGuid().ToString());

            CancellationToken cancellationToken = CancellationToken.None;

            mockContainer
                .Setup(container => container.GetPartitionKeyDefinitionAsync(cancellationToken))
                .Returns(Task.FromResult(partitionKeyDefinition));

            RequestInvokerHandler invoker = new(
                client: client,
                requestedClientConsistencyLevel: default,
                requestedClientPriorityLevel: default);

            Cosmos.FeedRange feedRange = await RequestInvokerHandler.ResolveFeedRangeBasedOnPrefixContainerAsync(
                feedRange: inputFeedRange,
                cosmosContainerCore: mockContainer.Object,
                cancellationToken: cancellationToken);

            mockContainer.Verify(x => x.GetPartitionKeyDefinitionAsync(Moq.It.IsAny<CancellationToken>()), getPartitionKeyDefinitionAsyncExecutions);

            Assert.IsNotNull(feedRange, "FeedRange did not initialize");

            Assert.IsInstanceOfType(
                value: feedRange,
                expectedType: typeof(TFeedRange));

            Assert.AreEqual(
                expected: expectedFeedRange.ToJsonString(),
                actual: feedRange.ToJsonString());
        }

        private class SomePayload
        {
            public string V1 { get; set; }
            public string V2 { get; set; }
        }

        private class EnumComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if ((int)x == (int)y &&
                    string.Equals(x.ToString(), y.ToString()))
                {
                    return 0;
                }

                return 1;
            }
        }
    }
}