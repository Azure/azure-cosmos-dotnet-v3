//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Documents;

    [TestClass]
    public class CosmosDatabaseAccountSettingsTests
    {
        private static readonly string telemetryEndpoint = "https://dummy.clienttelemetry.url";
        
      
        [TestMethod]
        public async Task GetCosmosDatabaseAccountSettings_WhenEnabledClientTelemetry()
        {
            AccountClientConfiguration clientConfig = new AccountClientConfiguration
            {
                ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                {
                    IsEnabled = true,
                    Endpoint = telemetryEndpoint
                }
            };

            HttpClientHandlerHelper handlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Contains(Paths.ClientConfigPathSegment))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                        string payload = JsonConvert.SerializeObject(clientConfig);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                useGateway: true,
                customizeClientBuilder: clientBuilder => clientBuilder.WithHttpClientFactory(() => new HttpClient(handlerHelper)));
            
            AccountProperties accountProperties = await cosmosClient.ReadAccountAsync();
            Assert.IsNotNull(accountProperties);
            Assert.IsNotNull(accountProperties.Id);
            Assert.IsNotNull(accountProperties.ReadableRegions);
            Assert.IsTrue(accountProperties.ReadableRegions.Count() > 0);
            Assert.IsNotNull(accountProperties.WritableRegions);
            Assert.IsTrue(accountProperties.WritableRegions.Count() > 0);

            Assert.IsNotNull(accountProperties.ClientConfiguration);
            Assert.IsNotNull(accountProperties.ClientConfiguration.ClientTelemetryConfiguration);
            Assert.AreEqual(telemetryEndpoint, accountProperties.ClientConfiguration.ClientTelemetryConfiguration.Endpoint);
            Assert.IsTrue(accountProperties.ClientConfiguration.ClientTelemetryConfiguration.IsEnabled);

            cosmosClient.Dispose();
        }


        [TestMethod]
        public async Task GetCosmosDatabaseAccountSettings_WhenDisabledClientTelemetry()
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(
                useGateway: true);

            AccountProperties accountProperties = await cosmosClient.ReadAccountAsync();
            Assert.IsNotNull(accountProperties);
            Assert.IsNotNull(accountProperties.Id);
            Assert.IsNotNull(accountProperties.ReadableRegions);
            Assert.IsTrue(accountProperties.ReadableRegions.Count() > 0);
            Assert.IsNotNull(accountProperties.WritableRegions);
            Assert.IsTrue(accountProperties.WritableRegions.Count() > 0);

            Assert.IsNotNull(accountProperties.ClientConfiguration);
            Assert.IsNotNull(accountProperties.ClientConfiguration.ClientTelemetryConfiguration);
            Assert.IsNull(accountProperties.ClientConfiguration.ClientTelemetryConfiguration.Endpoint);
            Assert.IsFalse(accountProperties.ClientConfiguration.ClientTelemetryConfiguration.IsEnabled);

            cosmosClient.Dispose();
        }
    }
}
