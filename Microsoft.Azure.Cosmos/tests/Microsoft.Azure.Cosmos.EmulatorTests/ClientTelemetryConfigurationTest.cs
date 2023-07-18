//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Net.Http;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Fluent;
    using System;
    using Microsoft.Azure.Cosmos.Routing;
    using System.Reflection;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Telemetry;

    [TestClass]
    public class ClientTelemetryConfigurationTest : BaseCosmosClientHelper
    {
        private const string EndpointUrl = "http://dummy.test.com/";
        private CosmosClientBuilder cosmosClientBuilder;

        [TestInitialize]
        public void TestInitialize()
        {
            ClientTelemetryOptions.DefaultTimeStampInSeconds = TimeSpan.FromSeconds(1);
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
        }
        
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task Validate_ClientTelemetryJob_Status_if_Disabled_At_Instance_LevelAsync()
        {
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Equals(EndpointUrl))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                        return Task.FromResult(result);
                    }
                    else if (request.RequestUri.AbsoluteUri.Contains(Documents.Paths.ClientConfigPathSegment))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        AccountClientConfigProperties clientConfigProperties = new AccountClientConfigProperties
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = true,
                                Endpoint = EndpointUrl
                            }
                        };

                        string payload = JsonConvert.SerializeObject(clientConfigProperties);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            this.cosmosClientBuilder
                .WithHttpClientFactory(() => new HttpClient(httpHandler))
                .DisableClientTelemetryToService();

            this.SetClient(this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            Assert.IsNull(documentClient.TelemetryToServiceHelper);

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
            .GetType()
            .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelper = (TelemetryToServiceHelper)collCache
                .GetType()
                .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(collCache);

            Assert.IsNull(telemetryToServiceHelper);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Validate_ClientTelemetryJob_Status_if_Enabled_Or_DisabledAsync(bool isEnabled)
        {
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Equals(EndpointUrl))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                        return Task.FromResult(result);
                    }
                    else if (request.RequestUri.AbsoluteUri.Contains(Documents.Paths.ClientConfigPathSegment))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        AccountClientConfigProperties clientConfigProperties = new AccountClientConfigProperties
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = isEnabled,
                                Endpoint = isEnabled ? EndpointUrl: null
                            }
                        };

                        string payload = JsonConvert.SerializeObject(clientConfigProperties);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            this.cosmosClientBuilder
                .WithHttpClientFactory(() => new HttpClient(httpHandler));
            this.SetClient(this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
            .GetType()
            .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelper = (TelemetryToServiceHelper)collCache
               .GetType()
               .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
               .GetValue(collCache);

            if (isEnabled)
            {
                Assert.IsNotNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance);
                Assert.IsNotNull(telemetryToServiceHelper.clientTelemetryInstance);
            }
            else
            {
                Assert.IsNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance);
                Assert.IsNull(telemetryToServiceHelper.clientTelemetryInstance);
            }
        }
        
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Validate_ClientTelemetryJob_When_Flag_Is_Switched(bool isEnabledInitially)
        {
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            
            int counter = 0;
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Contains(Documents.Paths.ClientConfigPathSegment))
                    {
                        counter++;
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        AccountClientConfigProperties clientConfigProperties = null;

                        if (counter < 5)
                        {
                            clientConfigProperties = new AccountClientConfigProperties
                            {
                                ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                                {
                                    IsEnabled = isEnabledInitially,
                                    Endpoint = isEnabledInitially ? EndpointUrl : null
                                }
                            };
                        }
                        else
                        {
                            clientConfigProperties = new AccountClientConfigProperties
                            {
                                ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                                {
                                    IsEnabled = !isEnabledInitially,
                                    Endpoint = !isEnabledInitially ? EndpointUrl : null
                                }
                            };
                        }
                        
                        if (counter == 8)
                        {
                            manualResetEvent.Set();
                        }
                        string payload = JsonConvert.SerializeObject(clientConfigProperties);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                       
                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            this.cosmosClientBuilder
                .WithHttpClientFactory(() => new HttpClient(httpHandler));
            this.SetClient(this.cosmosClientBuilder.Build());

            TelemetryToServiceHelper.DefaultBackgroundRefreshClientConfigTimeIntervalInMS = 10;

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
                                                .GetType()
                                                .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                                .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelper = (TelemetryToServiceHelper)collCache
               .GetType()
               .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
               .GetValue(collCache);

            if (isEnabledInitially)
            {
                Assert.IsNotNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance, "Before: Client Telemetry Job should be Running");
                Assert.IsNotNull(telemetryToServiceHelper.clientTelemetryInstance);
            }
            else
            {
                Assert.IsNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance, "Before: Client Telemetry Job should be Stopped");
                Assert.IsNull(telemetryToServiceHelper.clientTelemetryInstance);
            }

            manualResetEvent.WaitOne(TimeSpan.FromMilliseconds(100));

            documentClient = this.GetClient().DocumentClient;

            collCache = (ClientCollectionCache)documentClient
                                                .GetType()
                                                .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                                .GetValue(documentClient);

            telemetryToServiceHelper = (TelemetryToServiceHelper)collCache
               .GetType()
               .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
               .GetValue(collCache);

            if (isEnabledInitially)
            {
                Assert.IsNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance, "After: Client Telemetry Job should be Stopped");
                Assert.IsNull(telemetryToServiceHelper.clientTelemetryInstance);
            }
            else
            {
                Assert.IsNotNull(documentClient.TelemetryToServiceHelper.clientTelemetryInstance, "After: Client Telemetry Job should be Running");
                Assert.IsNotNull(telemetryToServiceHelper.clientTelemetryInstance);
            }
            
        }
    }
}
