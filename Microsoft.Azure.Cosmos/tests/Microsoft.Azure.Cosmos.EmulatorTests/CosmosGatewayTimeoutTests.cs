//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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

                // Get the GatewayStoreClient
                FieldInfo gatewayStoreClientProperty = gatewayStore.GetType().GetField("gatewayStoreClient", BindingFlags.NonPublic | BindingFlags.Instance);
                GatewayStoreClient storeClient = (GatewayStoreClient)gatewayStoreClientProperty.GetValue(gatewayStore);

                // Set the http request timeout to 10 ms to cause a timeout exception
                HttpClient httpClient = new HttpClient(new TimeOutHttpClientHandler());
                FieldInfo httpClientProperty = storeClient.GetType().GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
                httpClientProperty.SetValue(storeClient, httpClient);

                // Verify the failure has the required info
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
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

        private sealed class TimeOutHttpClientHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new TaskCanceledException();
            }
        }
    }
}
