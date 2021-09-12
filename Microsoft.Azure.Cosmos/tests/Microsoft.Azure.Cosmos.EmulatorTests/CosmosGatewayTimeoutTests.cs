//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Castle.DynamicProxy.Generators.Emitters;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosGatewayTimeoutTests
    {
        [TestMethod]
        public async Task GatewayStoreClientTimeout()
        {
            // Cause http client to throw a TaskCanceledException to simulate a timeout
            HttpClient httpClient = new HttpClient(new TimeOutHttpClientHandler())
            {
                Timeout = TimeSpan.FromSeconds(1)
            };

            using (CosmosClient client = TestCommon.CreateCosmosClient(x => x.WithConnectionModeGateway().WithHttpClientFactory(() => httpClient)))
            {
                // Verify the failure has the required info
                try
                {
                    await client.CreateDatabaseAsync("TestGatewayTimeoutDb" + Guid.NewGuid().ToString());
                    Assert.Fail("Operation should have timed out:");
                }
                catch (CosmosException rte)
                {
                    string message = rte.ToString();
                    Assert.IsTrue(message.Contains("Start Time"), "Start Time:" + message);
                    Assert.IsTrue(message.Contains("Total Duration"), "Total Duration:" + message);
                    Assert.IsTrue(message.Contains("Http Client Timeout"), "Http Client Timeout:" + message);
                    Assert.IsTrue(message.Contains("Activity id"), "Activity id:" + message);
                }
            }
        }

        [TestMethod]
        public async Task QueryPlanRetryTimeoutTestAsync()
        {
            HttpClientHandlerHelper httpClientHandler = new HttpClientHandlerHelper();
            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => builder
                .WithConnectionModeGateway()
                .WithHttpClientFactory(() => new HttpClient(httpClientHandler))))
            {
                Cosmos.Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
                ContainerInternal container = (ContainerInternal)await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");

                Container gatewayQueryPlanContainer = new ContainerInlineCore(
                    client.ClientContext,
                    (DatabaseInternal)database,
                    container.Id,
                    new DisableServiceInterop(client.ClientContext, container));

                bool isQueryRequestFound = false;
                httpClientHandler.RequestCallBack = (request, cancellationToken) =>
                {
                    if (request.Headers.TryGetValues(HttpConstants.HttpHeaders.IsQueryPlanRequest, out IEnumerable<string> isQueryPlan) &&
                        isQueryPlan.FirstOrDefault() == bool.TrueString)
                    {
                        Assert.IsFalse(isQueryRequestFound, "Should only call get query plan once.");
                        Assert.AreNotEqual(cancellationToken, default);
                        isQueryRequestFound = true;
                    }
                };

                using FeedIterator<JObject> iterator = gatewayQueryPlanContainer.GetItemQueryIterator<JObject>("select * From T order by T.status");
                FeedResponse<JObject> response = await iterator.ReadNextAsync();

                Assert.IsTrue(isQueryRequestFound, "Query plan call back was not called.");
                await database.DeleteStreamAsync();
            }
        }

        [TestMethod]
        public async Task CosmosHttpClientRetryValidation()
        {
            TransientHttpClientCreatorHandler handler = new TransientHttpClientCreatorHandler();
            HttpClient httpClient = new HttpClient(handler);
            using (CosmosClient client = TestCommon.CreateCosmosClient(builder =>
                builder.WithConnectionModeGateway()
                    .WithHttpClientFactory(() => httpClient)))
            {
                // Verify the failure has the required info
                try
                {
                    await client.CreateDatabaseAsync("TestGatewayTimeoutDb" + Guid.NewGuid().ToString());
                    Assert.Fail("Operation should have timed out:");
                }
                catch (CosmosException rte)
                {
                    Assert.IsTrue(handler.Count >= 6);
                    string message = rte.ToString();
                    Assert.IsTrue(message.Contains("Start Time"), "Start Time:" + message);
                    Assert.IsTrue(message.Contains("Total Duration"), "Total Duration:" + message);
                    Assert.IsTrue(message.Contains("Http Client Timeout"), "Http Client Timeout:" + message);
                }
            }
        }

        private class TimeOutHttpClientHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new TaskCanceledException();
            }
        }

        private class TransientHttpClientCreatorHandler : DelegatingHandler
        {
            public int Count { get; private set; } = 0;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (this.Count++ <= 3)
                {
                    throw new WebException();
                }

                throw new TaskCanceledException();
            }
        }

        private class HttpClientHandlerHelper : DelegatingHandler
        {
            public HttpClientHandlerHelper() : base(new HttpClientHandler())
            {
            }

            public Action<HttpRequestMessage, CancellationToken> RequestCallBack { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.RequestCallBack?.Invoke(request, cancellationToken);
                return base.SendAsync(request, cancellationToken);
            }
        }

        private class DisableServiceInterop : CosmosQueryClientCore
        {
            public DisableServiceInterop(
                CosmosClientContext clientContext,
                ContainerInternal cosmosContainerCore) :
                base(clientContext, cosmosContainerCore)
            {
            }

            public override bool ByPassQueryParsing()
            {
                return true;
            }
        }
    }
}
