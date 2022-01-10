//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using System;
    using System.Net.Http;
    using System.Net;
    using System.Threading;
    using System.IO;
    using System.Net.Sockets;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    [TestClass]
    public class CosmosHttpClientCoreTests
    {
        [TestMethod]
        public async Task ResponseMessageHasRequestMessageAsync()
        {
            // We don't set the RequestMessage property on purpose on the Failed response
            // This will make it go through GatewayStoreClient.CreateDocumentClientExceptionAsync
            static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("test")
                };
                return Task.FromResult(response);
            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost"));

            HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                new ValueTask<HttpRequestMessage>(httpRequestMessage),
                ResourceType.Collection,
                timeoutPolicy: HttpTimeoutPolicyDefault.Instance,
                new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow),
                default);

            Assert.AreEqual(httpRequestMessage, responseMessage.RequestMessage);
        }

        [TestMethod]
        public async Task RetryTransientIssuesTestAsync()
        {
            using CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource();
            using CancellationTokenSource cancellationTokenSource2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource1.Token);
            cancellationTokenSource2.Cancel();
            Assert.IsFalse(cancellationTokenSource1.IsCancellationRequested);

            IReadOnlyDictionary<HttpTimeoutPolicy, IReadOnlyList<TimeSpan>> timeoutMap = new Dictionary<HttpTimeoutPolicy, IReadOnlyList<TimeSpan>>()
            {
                {HttpTimeoutPolicyControlPlaneRead.Instance,  new List<TimeSpan>()
                {
                    TimeSpan.FromSeconds(5.1),
                    TimeSpan.FromSeconds(10.1),
                    TimeSpan.FromSeconds(20.1)
                }},
                {HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,  new List<TimeSpan>()
                {
                    TimeSpan.FromSeconds(.6),
                    TimeSpan.FromSeconds(5.1),
                    TimeSpan.FromSeconds(65.1)
                }},
            };

            foreach (KeyValuePair<HttpTimeoutPolicy, IReadOnlyList<TimeSpan>> currentTimeoutPolicy in timeoutMap)
            {
                int count = 0;
                async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    count++;

                    if (count == 1)
                    {
                        Assert.IsFalse(cancellationToken.IsCancellationRequested);
                        await Task.Delay(currentTimeoutPolicy.Value[0]);
                        cancellationToken.ThrowIfCancellationRequested();
                        Assert.Fail("Cancellation token should be canceled");
                    }

                    if (count == 2)
                    {
                        Assert.IsFalse(cancellationToken.IsCancellationRequested);
                        await Task.Delay(currentTimeoutPolicy.Value[1]);
                        cancellationToken.ThrowIfCancellationRequested();
                        Assert.Fail("Cancellation token should be canceled");
                    }

                    if (count == 3)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }

                    throw new Exception("Should not return after the success");
                }

                DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

                HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(
                        result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                        resourceType: ResourceType.Collection,
                        timeoutPolicy: currentTimeoutPolicy.Key,
                        clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow),
                        cancellationToken: default);

                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            }
        }

        [TestMethod]
        public async Task RetryTransientIssuesForQueryPlanTestAsync()
        {
            DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                OperationType.QueryPlan,
                ResourceType.Document,
                @"dbs/1889fcb0-7d02-41a4-94c9-189f6aa1b444/colls/c264ae0f-7708-46fb-a015-29a40ea3c18b",
                new MemoryStream(),
                AuthorizationTokenType.PrimaryMasterKey,
                new StoreRequestNameValueCollection());

            HttpTimeoutPolicy retryPolicy = HttpTimeoutPolicy.GetTimeoutPolicy(documentServiceRequest);
            Assert.AreEqual(HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance, retryPolicy);

            int count = 0;
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> retry = retryPolicy.GetTimeoutEnumerator();
            async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                count++;
                retry.MoveNext();

                if (count <= 2)
                {
                    Assert.IsFalse(cancellationToken.IsCancellationRequested);
                    await Task.Delay(retry.Current.requestTimeout + TimeSpan.FromSeconds(.1));
                    cancellationToken.ThrowIfCancellationRequested();
                    Assert.Fail("Cancellation token should be canceled");
                }

                if (count == 3)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                throw new Exception("Should not return after the success");
            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                new ValueTask<HttpRequestMessage>(
                    result: new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost"))),
                    resourceType: ResourceType.Document,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                    clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow),
                    cancellationToken: default);

            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc;

            public MockMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request, cancellationToken);
            }
        }
    }
}
