//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Net.Http;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
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

        private readonly TimeSpan OriginalDefaultBackgroundRefreshClientConfigTimeInterval = TelemetryToServiceHelper.DefaultBackgroundRefreshClientConfigTimeInterval;
        
        [TestInitialize]
        public void TestInitialize()
        {
            TelemetryToServiceHelper.DefaultBackgroundRefreshClientConfigTimeInterval 
                = TimeSpan.FromMilliseconds(100);

            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();

            // Resetting time intervals
            TelemetryToServiceHelper.DefaultBackgroundRefreshClientConfigTimeInterval 
                = this.OriginalDefaultBackgroundRefreshClientConfigTimeInterval;
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

                        AccountClientConfiguration clientConfigProperties = new AccountClientConfiguration
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
                .WithTelemetryDisabled();

            this.SetClient(this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            Assert.IsNotNull(documentClient.telemetryToServiceHelper);
            Assert.IsFalse(documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
            .GetType()
            .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelper = (TelemetryToServiceHelper)collCache
                .GetType()
                .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(collCache);

            Assert.IsNotNull(telemetryToServiceHelper);
            Assert.IsFalse(telemetryToServiceHelper.IsClientTelemetryJobRunning());
        }

        [TestMethod]
        [DataRow(true, HttpStatusCode.OK)]
        [DataRow(false, HttpStatusCode.OK)]
        [DataRow(false, HttpStatusCode.BadRequest)] // Errored Client Config API
        public async Task Validate_ClientTelemetryJob_Status_with_Client_Config_Api_States_Async(bool clientTelemetryFlagEnabled, HttpStatusCode clientConfigApiStatus)
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
                        HttpResponseMessage result = new HttpResponseMessage(clientConfigApiStatus);

                        AccountClientConfiguration clientConfigProperties = new AccountClientConfiguration
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = clientTelemetryFlagEnabled,
                                Endpoint = clientTelemetryFlagEnabled ? EndpointUrl : null
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
                .WithTelemetryEnabled();

            this.SetClient(this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
            .GetType()
            .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelperFromCollectionCache = (TelemetryToServiceHelper)collCache
               .GetType()
               .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
               .GetValue(collCache);

            Assert.AreEqual(clientTelemetryFlagEnabled, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
            Assert.AreEqual(clientTelemetryFlagEnabled, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());
        }

        [TestMethod]
        [DataRow(true, false, HttpStatusCode.OK)]
        [DataRow(false, true, HttpStatusCode.OK)]
        [DataRow(true, false, HttpStatusCode.BadRequest)]
        [DataRow(false, true, HttpStatusCode.BadRequest)]
        public async Task Validate_ClientTelemetryJob_When_Flag_Is_Switched(bool flagState1, bool flagState2, HttpStatusCode clientConfigApiStatusAfterSwitch)
        {
            using ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            int counter = 0;
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Contains(Documents.Paths.ClientConfigPathSegment))
                    {
                        if (counter == 10)
                        {
                            manualResetEvent.Set();
                        }

                        counter++;
                      
                        bool isEnabled = counter < 5 ? flagState1 : flagState2;
                        HttpStatusCode apiStatusCode = counter < 5 ? HttpStatusCode.OK : clientConfigApiStatusAfterSwitch;

                        HttpResponseMessage result = new HttpResponseMessage(apiStatusCode);
                        string payload = JsonConvert.SerializeObject(new AccountClientConfiguration
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = isEnabled,
                                Endpoint = isEnabled ? EndpointUrl : null
                            }
                        });
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        return Task.FromResult(result);
                    }

                    return null;
                }
            };

            this.cosmosClientBuilder
                .WithHttpClientFactory(() => new HttpClient(httpHandler))
                .WithTelemetryEnabled();

            this.SetClient(this.cosmosClientBuilder.Build());

            this.database = await this.GetClient()
                .CreateDatabaseAsync(Guid.NewGuid().ToString());

            DocumentClient documentClient = this.GetClient().DocumentClient;

            ClientCollectionCache collCache = (ClientCollectionCache)documentClient
                                                .GetType()
                                                .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                                .GetValue(documentClient);

            TelemetryToServiceHelper telemetryToServiceHelperFromCollectionCache 
                = (TelemetryToServiceHelper)collCache
                   .GetType()
                   .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
                   .GetValue(collCache);

            Assert.AreEqual(flagState1, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
            Assert.AreEqual(flagState1, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());

            manualResetEvent.WaitOne(TimeSpan.FromSeconds(1));

            collCache = (ClientCollectionCache)documentClient
                                               .GetType()
                                               .GetField("collectionCache", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                               .GetValue(documentClient);

            telemetryToServiceHelperFromCollectionCache = (TelemetryToServiceHelper)collCache
               .GetType()
               .GetField("telemetryToServiceHelper", BindingFlags.Instance | BindingFlags.NonPublic)
               .GetValue(collCache);

            if (clientConfigApiStatusAfterSwitch == HttpStatusCode.OK)
            {
                Assert.AreEqual(flagState2, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
                Assert.AreEqual(flagState2, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());
            }
            else
            {
                // If the client config api errored out, the flag should not be changed
                Assert.AreEqual(flagState1, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
                Assert.AreEqual(flagState1, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());
            }
           
           
        }
    }
}