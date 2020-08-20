//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Moq;

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosGatewayTimeoutTests
    {
        [TestMethod]
        public async Task GatewayStoreClientTimeout()
        {
            using (CosmosClient client = TestCommon.CreateCosmosClient(useGateway: true))
            {
                // Creates the store clients in the document client
                await client.DocumentClient.EnsureValidClientAsync();

                // Get the GatewayStoreModel
                GatewayStoreModel gatewayStore;
                using (DocumentServiceRequest serviceRequest = new DocumentServiceRequest(
                                operationType: OperationType.Read,
                                resourceIdOrFullName: null,
                                resourceType: ResourceType.Database,
                                body: null,
                                headers: null,
                                isNameBased: false,
                                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
                {
                    serviceRequest.UseGatewayMode = true;
                    gatewayStore = (GatewayStoreModel)client.DocumentClient.GetStoreProxy(serviceRequest);
                }

                CosmosHttpClientCore cosmosHttpClient = (CosmosHttpClientCore)client.ClientContext.CosmosHttpClient;
                // Set the http request timeout to 10 ms to cause a timeout exception
                HttpClient httpClient = new HttpClient(new TimeOutHttpClientHandler());
                FieldInfo httpClientProperty = cosmosHttpClient.GetType().GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
                httpClientProperty.SetValue(cosmosHttpClient, httpClient);

                FieldInfo gatewayRequestTimeoutProperty = cosmosHttpClient.GetType().GetField("GatewayRequestTimeout", BindingFlags.NonPublic | BindingFlags.Static);
                gatewayRequestTimeoutProperty.SetValue(cosmosHttpClient, TimeSpan.FromSeconds(1));

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

        private class TimeOutHttpClientHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new TaskCanceledException();
            }
        }
    }
}
