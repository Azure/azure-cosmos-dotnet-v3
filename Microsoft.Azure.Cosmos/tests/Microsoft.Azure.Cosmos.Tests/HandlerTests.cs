//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
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

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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

                    RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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

                RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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

                RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: client.ClientOptions.ConsistencyLevel)
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

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, requestedClientConsistencyLevel: null)
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
