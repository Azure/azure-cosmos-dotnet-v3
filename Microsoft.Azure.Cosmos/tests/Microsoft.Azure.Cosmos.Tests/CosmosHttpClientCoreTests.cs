//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            using (ITrace trace = Trace.GetRootTrace(nameof(ResponseMessageHasRequestMessageAsync)))
            {
                HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(httpRequestMessage),
                    ResourceType.Collection,
                    timeoutPolicy: HttpTimeoutPolicyDefault.Instance,
                    new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                    default);

                Assert.AreEqual(httpRequestMessage, responseMessage.RequestMessage);
            }
        }

        [TestMethod]
        [TestCategory("Flaky")]
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

                using (ITrace trace = Trace.GetRootTrace(nameof(RetryTransientIssuesTestAsync)))
                {
                    HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(
                        result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                        resourceType: ResourceType.Collection,
                        timeoutPolicy: currentTimeoutPolicy.Key,
                        clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                        cancellationToken: default);

                    Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
                }
            }
        }

        [TestMethod]
        public void VerifyShouldRetryOnResponseTest()
        {
            foreach (HttpStatusCode statusCode in Enum.GetValues(typeof(HttpStatusCode)))
            {
                HttpResponseMessage responseMessage = new HttpResponseMessage(statusCode);
                Assert.IsFalse(HttpTimeoutPolicyDefault.Instance.ShouldRetryBasedOnResponse(HttpMethod.Get, responseMessage));
                Assert.IsFalse(HttpTimeoutPolicyControlPlaneRead.Instance.ShouldRetryBasedOnResponse(HttpMethod.Get, responseMessage));

                Assert.IsFalse(HttpTimeoutPolicyDefault.Instance.ShouldRetryBasedOnResponse(HttpMethod.Put, responseMessage));
                Assert.IsFalse(HttpTimeoutPolicyControlPlaneRead.Instance.ShouldRetryBasedOnResponse(HttpMethod.Put, responseMessage));

                Assert.IsFalse(HttpTimeoutPolicyNoRetry.Instance.ShouldRetryBasedOnResponse(HttpMethod.Put, responseMessage));
                Assert.IsFalse(HttpTimeoutPolicyNoRetry.Instance.ShouldRetryBasedOnResponse(HttpMethod.Get, responseMessage));

                if (statusCode == HttpStatusCode.RequestTimeout)
                {
                    Assert.IsTrue(HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.ShouldRetryBasedOnResponse(HttpMethod.Get, responseMessage));
                    Assert.IsTrue(HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.ShouldRetryBasedOnResponse(HttpMethod.Put, responseMessage));
                }
                else
                {
                    Assert.IsFalse(HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.ShouldRetryBasedOnResponse(HttpMethod.Get, responseMessage));
                    Assert.IsFalse(HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.ShouldRetryBasedOnResponse(HttpMethod.Put, responseMessage));
                }
            }
        }

        [TestMethod]
        public async Task RetryTransient408sTestAsync()
        {
            int count = 0;
            async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                count++;

                if (count <= 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                    return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                }

                if (count == 3)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                throw new Exception("Should not return after the success");
            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            using (ITrace trace = Trace.GetRootTrace(nameof(RetryTransient408sTestAsync)))
            {
                HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                new ValueTask<HttpRequestMessage>(
                    result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                    resourceType: ResourceType.Collection,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                    clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                    cancellationToken: default);

                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            }
        }

        [TestMethod]
        public async Task DoesNotRetryTransient408sOnDefaultPolicyTestAsync()
        {
            int count = 0;
            async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                count++;

                if (count <= 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                    return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                }

                if (count == 3)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                throw new Exception("Should not return after the success");
            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            using (ITrace trace = Trace.GetRootTrace(nameof(DoesNotRetryTransient408sOnDefaultPolicyTestAsync)))
            {
                HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                new ValueTask<HttpRequestMessage>(
                    result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                    resourceType: ResourceType.Collection,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                    clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                    cancellationToken: default);

                Assert.AreEqual(HttpStatusCode.RequestTimeout, responseMessage.StatusCode, "Should be a request timeout");
            }
        }

        [TestMethod]
        public async Task Retry3TimesOnDefaultPolicyTestAsync()
        {
            int count = 0;
            Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                count++;

                throw new OperationCanceledException("API with exception");

            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            try
            {
                using (ITrace trace = Trace.GetRootTrace(nameof(Retry3TimesOnDefaultPolicyTestAsync)))
                {
                    HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(
                        result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                        resourceType: ResourceType.Collection,
                        timeoutPolicy: HttpTimeoutPolicyDefault.Instance,
                        clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                        cancellationToken: default);
                }
            }
            catch (Exception)
            {
                //Ignore the exception
            }

            Assert.AreEqual(3, count, "Should retry 3 times");
        }

        [TestMethod]
        public async Task HttpTimeoutThrow503TestAsync()
        {

            async Task TestScenarioAsync(HttpMethod method, ResourceType resourceType, HttpTimeoutPolicy timeoutPolicy, Type expectedException, int expectedNumberOfRetrys)
            {
                int count = 0;
                Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    count++;

                    throw new OperationCanceledException("API with exception");

                }

                DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

                try
                {
                    using (ITrace trace = Trace.GetRootTrace(nameof(NoRetryOnNoRetryPolicyTestAsync)))
                    {
                        HttpResponseMessage responseMessage1 = await cosmoshttpClient.SendHttpAsync(() =>
                        new ValueTask<HttpRequestMessage>(
                            result: new HttpRequestMessage(method, new Uri("http://localhost"))),
                            resourceType: resourceType,
                            timeoutPolicy: timeoutPolicy,
                            clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                            cancellationToken: default);
                    }
                }
                catch (Exception e)
                {
                    Assert.AreEqual(expectedNumberOfRetrys, count, "Should retry 3 times for read methods, for writes should only be tried once");
                    Assert.AreEqual(e.GetType(), expectedException);

                    if (e.GetType() == typeof(CosmosException))
                    {
                        CosmosException cosmosException = (CosmosException)e;
                        Assert.AreEqual(cosmosException.StatusCode, System.Net.HttpStatusCode.ServiceUnavailable);
                        Assert.AreEqual((int)cosmosException.SubStatusCode, (int)SubStatusCodes.TransportGenerated503);

                        Assert.IsNotNull(cosmosException.Trace);
                        Assert.AreNotEqual(cosmosException.Trace, NoOpTrace.Singleton);
                    }
                }

            }

            //Data plane read
            await TestScenarioAsync(HttpMethod.Get, ResourceType.Document, HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout, typeof(CosmosException), 3);

            //Data plane write (Should throw a 408 OperationCanceledException rather than a 503)
            await TestScenarioAsync(HttpMethod.Post, ResourceType.Document, HttpTimeoutPolicyDefault.Instance, typeof(TaskCanceledException), 1);

            //Meta data read
            await TestScenarioAsync(HttpMethod.Get, ResourceType.Database, HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout, typeof(CosmosException), 3);

            //Query plan read (note all query plan operations are reads).
            await TestScenarioAsync(HttpMethod.Get, ResourceType.Document, HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout, typeof(CosmosException), 3);

            //Metadata Write (Should throw a 408 OperationCanceledException rather than a 503)
            await TestScenarioAsync(HttpMethod.Post, ResourceType.Document, HttpTimeoutPolicyDefault.Instance, typeof(TaskCanceledException), 1);
        }

        [TestMethod]
        public async Task NoRetryOnNoRetryPolicyTestAsync()
        {
            int count = 0;
            Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (count == 0)
                {
                    Assert.IsFalse(cancellationToken.IsCancellationRequested);
                }
                count++;

                throw new OperationCanceledException("API with exception");
            }

            DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            using CosmosHttpClient cosmoshttpClient = MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler));

            try
            {
                using (ITrace trace = Trace.GetRootTrace(nameof(NoRetryOnNoRetryPolicyTestAsync)))
                {
                    HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(
                        result: new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost"))),
                        resourceType: ResourceType.Collection,
                        timeoutPolicy: HttpTimeoutPolicyNoRetry.Instance,
                        clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                        cancellationToken: default);
                }
            }
            catch (Exception)
            {
                //Ignore the exception
            }

            Assert.AreEqual(1, count, "Should not retry at all");
        }

        [TestMethod]
        [TestCategory("Flaky")]
        public async Task RetryTransientIssuesForQueryPlanTestAsync()
        {
            DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                OperationType.QueryPlan,
                ResourceType.Document,
                @"dbs/1889fcb0-7d02-41a4-94c9-189f6aa1b444/colls/c264ae0f-7708-46fb-a015-29a40ea3c18b",
                new MemoryStream(),
                AuthorizationTokenType.PrimaryMasterKey,
                new Documents.Collections.RequestNameValueCollection());

            HttpTimeoutPolicy retryPolicy = HttpTimeoutPolicy.GetTimeoutPolicy(documentServiceRequest);
            Assert.AreEqual(HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout, retryPolicy);

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

            using (ITrace trace = Trace.GetRootTrace(nameof(RetryTransientIssuesForQueryPlanTestAsync)))
            {
                HttpResponseMessage responseMessage = await cosmoshttpClient.SendHttpAsync(() =>
                    new ValueTask<HttpRequestMessage>(
                        result: new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost"))),
                        resourceType: ResourceType.Document,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                        clientSideRequestStatistics: new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace),
                        cancellationToken: default);

                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
            }
        }

        [TestMethod]
        public void CreateSocketsHttpHandlerCreatesCorrectValueType()
        {
            int gatewayLimit = 10;
            IWebProxy webProxy = null;
            Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback = (certificate2, x509Chain, sslPolicyErrors) => false;

            HttpMessageHandler handler = CosmosHttpClientCore.CreateSocketsHttpHandlerHelper(
                gatewayLimit,
                webProxy,
                serverCertificateCustomValidationCallback);

            Assert.AreEqual(Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http"), handler.GetType());
            SocketsHttpHandler socketsHandler = (SocketsHttpHandler)handler;

            Assert.IsTrue(TimeSpan.FromMinutes(5.5) >= socketsHandler.PooledConnectionLifetime);
            Assert.IsTrue(TimeSpan.FromMinutes(5) <= socketsHandler.PooledConnectionLifetime);
            Assert.AreEqual(webProxy, socketsHandler.Proxy);
            Assert.AreEqual(gatewayLimit, socketsHandler.MaxConnectionsPerServer);

            //Create cert for test
            X509Certificate2 x509Certificate2 = new CertificateRequest("cn=www.test", ECDsa.Create(), HashAlgorithmName.SHA256).CreateSelfSigned(DateTime.Now, DateTime.Now.AddYears(1));
            X509Chain x509Chain = new X509Chain();
            SslPolicyErrors sslPolicyErrors = new SslPolicyErrors();
            Assert.IsFalse(socketsHandler.SslOptions.RemoteCertificateValidationCallback.Invoke(new object(), x509Certificate2, x509Chain, sslPolicyErrors));
        }

        [TestMethod]
        public void CreateHttpClientHandlerCreatesCorrectValueType()
        {
            int gatewayLimit = 10;
            IWebProxy webProxy = null;
            Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback = (certificate2, x509Chain, sslPolicyErrors) => false;

            HttpMessageHandler handler = CosmosHttpClientCore.CreateHttpClientHandlerHelper(gatewayLimit, webProxy, serverCertificateCustomValidationCallback);

            Assert.AreEqual(Type.GetType("System.Net.Http.HttpClientHandler, System.Net.Http"), handler.GetType());
            HttpClientHandler clientHandler = (HttpClientHandler)handler;

            Assert.AreEqual(webProxy, clientHandler.Proxy);
            Assert.AreEqual(gatewayLimit, clientHandler.MaxConnectionsPerServer);

            //Create cert for test
            X509Certificate2 x509Certificate2 = new CertificateRequest("cn=www.test", ECDsa.Create(), HashAlgorithmName.SHA256).CreateSelfSigned(DateTime.Now, DateTime.Now.AddYears(1));
            X509Chain x509Chain = new X509Chain();
            SslPolicyErrors sslPolicyErrors = new SslPolicyErrors();
            Assert.IsFalse(clientHandler.ServerCertificateCustomValidationCallback.Invoke(new HttpRequestMessage(), x509Certificate2, x509Chain, sslPolicyErrors));
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