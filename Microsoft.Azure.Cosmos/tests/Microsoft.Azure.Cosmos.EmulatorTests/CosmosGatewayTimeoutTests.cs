//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
 
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosGatewayTimeoutTests
    { 
        [TestMethod]
        public async Task GatewayStoreClientTimeout()
        {
            using (CosmosClient client = TestCommon.CreateCosmosClient())
            {
                HttpClient httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMilliseconds(50);
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                httpClient.AddApiTypeHeader(ApiType.None);

                // Set requested API version header that can be used for
                // version enforcement.
                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                    HttpConstants.Versions.CurrentVersion);

                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);

                GatewayStoreClient gatewayStoreClient = new GatewayStoreClient(
                    httpClient,
                    DocumentClientEventSource.Instance,
                    null);

                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                try
                {

                    using (new ActivityScope(Guid.NewGuid()))
                    {
                        using (DocumentServiceRequest serviceRequest = new DocumentServiceRequest(
                                operationType: OperationType.Read,
                                resourceIdOrFullName: null,
                                resourceType: ResourceType.Database,
                                body: null,
                                headers: null,
                                isNameBased: false,
                                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
                        {
                            string authorization = ((IAuthorizationTokenProvider)client.DocumentClient).GetUserAuthorizationToken(
                                serviceRequest.ResourceAddress,
                                PathsHelper.GetResourcePath(serviceRequest.ResourceType),
                                HttpMethod.Get.ToString(),
                                serviceRequest.Headers,
                                AuthorizationTokenType.PrimaryMasterKey,
                                payload: out _);

                            serviceRequest.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;

                            await gatewayStoreClient.InvokeAsync(
                                serviceRequest,
                                ResourceType.Database,
                                new Uri("https://127.0.0.1:8081/dbs/97c0b8d0-9c6f-462f-8ea8-3374ab788273"),
                                cancellationTokenSource.Token);
                        }
                    }
                }
                catch (RequestTimeoutException rte)
                {
                    string message = rte.ToString();
                    Assert.IsTrue(message.Contains("Start Time"));
                    Assert.IsTrue(message.Contains("Total Duration"));
                    Assert.IsTrue(message.Contains("Http Client Timeout"));
                    Assert.IsTrue(message.Contains("Activity id"));
                }
            }
        }
    }
}
