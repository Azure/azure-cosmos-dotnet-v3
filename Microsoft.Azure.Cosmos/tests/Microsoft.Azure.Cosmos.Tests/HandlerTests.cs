//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class HandlerTests
    {

        [TestMethod]
        public void HandlerOrder()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            Type[] types = new Type[]
            {
                typeof(RequestInvokerHandler),
                typeof(RetryHandler),
                typeof(RouterHandler)
            };

            CosmosRequestHandler handler = client.RequestHandler;
            foreach (Type type in types)
            {
                Assert.IsTrue(type.Equals(handler.GetType()));
                handler = (CosmosRequestHandler)handler.InnerHandler;
            }

            Assert.IsNull(handler);
        }

        [TestMethod]
        public async Task TestPreProcessingHandler()
        {
            CosmosRequestHandler preProcessHandler = new PreProcessingTestHandler();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient((builder) => builder.AddCustomHandlers(preProcessHandler));

            Assert.IsTrue(typeof(RequestInvokerHandler).Equals(client.RequestHandler.GetType()));
            Assert.IsTrue(typeof(PreProcessingTestHandler).Equals(client.RequestHandler.InnerHandler.GetType()));

            Container container = client.GetDatabase("testdb")
                                        .GetContainer("testcontainer");

            HttpStatusCode[] testHttpStatusCodes = new HttpStatusCode[]
                                {
                                    HttpStatusCode.OK,
                                    HttpStatusCode.NotFound
                                };

            // User operations
            foreach (HttpStatusCode code in testHttpStatusCodes)
            {
                ItemRequestOptions options = new ItemRequestOptions();
                options.Properties = new Dictionary<string, object>();
                options.Properties.Add(PreProcessingTestHandler.StatusCodeName, code);

                ItemResponse<object> response = await container.ReadItemAsync<object>(new Cosmos.PartitionKey("pk1"), "id1", options);
                Console.WriteLine($"Got status code {response.StatusCode}");
                Assert.AreEqual(code, response.StatusCode);
            }

            // Meta-data operations
            foreach (HttpStatusCode code in testHttpStatusCodes)
            {
                ContainerRequestOptions options = new ContainerRequestOptions();
                options.Properties = new Dictionary<string, object>();
                options.Properties.Add(PreProcessingTestHandler.StatusCodeName, code);

                ContainerResponse response = await container.DeleteAsync(options);

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

            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                Assert.AreEqual(propertyValue, request.Properties[PropertyKey]);
                Assert.AreEqual(Condition, request.Headers.GetValues(HttpConstants.HttpHeaders.IfMatch).First());
                return TestHandler.ReturnSuccess();
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = testHandler;
            CosmosRequestMessage requestMessage = new CosmosRequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            requestMessage.RequestOptions = options;

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

            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                Assert.AreEqual(Condition, request.Headers.GetValues(HttpConstants.HttpHeaders.IfNoneMatch).First());
                Assert.AreEqual(ConsistencyLevel.Eventual.ToString(), request.Headers.GetValues(HttpConstants.HttpHeaders.ConsistencyLevel).First());
                Assert.AreEqual(SessionToken, request.Headers.GetValues(HttpConstants.HttpHeaders.SessionToken).First());
                return TestHandler.ReturnSuccess();
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = testHandler;
            CosmosRequestMessage requestMessage = new CosmosRequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
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
                HttpRequestMessage hrm = new HttpRequestMessage(method, ep);
                hrm.Content = new StreamContent(ms);

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

            CosmosResponseMessage response = TransportHandler.AggregateExceptionConverter(ae, null);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsTrue(response.ErrorMessage.StartsWith(errorMessage));
        }

        private class SomePayload
        {
            public string V1 { get; set; }
            public string V2 { get; set; }
        }
    }
}
