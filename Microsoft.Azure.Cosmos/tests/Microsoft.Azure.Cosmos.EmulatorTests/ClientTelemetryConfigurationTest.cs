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
                = TimeSpan.FromMinutes(10);
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
        [DataRow(true, false)]
        [DataRow(false, false)]
        [DataRow(false, true)] // Errored Client Config API should be treated as disabled client telemetry state
        public async Task Validate_ClientTelemetryJob_Status_if_Enabled_Or_Disabled_Or_Errored_Client_Config_ApiAsync(bool isEnabled, bool isErrored)
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
                        if(isErrored)
                        {
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
                        }

                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        AccountClientConfiguration clientConfigProperties = new AccountClientConfiguration
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = isEnabled,
                                Endpoint = isEnabled ? EndpointUrl : null
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

            Assert.AreEqual(isEnabled, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
            Assert.AreEqual(isEnabled, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());
        }

        [TestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        public async Task Validate_ClientTelemetryJob_When_Flag_Is_Switched(bool flagState1, bool flagState2)
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

                        Console.WriteLine($"Counter: {counter}");
                        counter++;
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        bool isEnabled = counter < 5 ? flagState1 : flagState2;
                        string payload = JsonConvert.SerializeObject(new AccountClientConfiguration
                        {
                            ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                            {
                                IsEnabled = isEnabled,
                                Endpoint = isEnabled ? EndpointUrl : null
                            }
                        });

                        Console.WriteLine($"payload: {payload}");
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        Console.WriteLine($"i ma here");
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

            Assert.AreEqual(flagState2, documentClient.telemetryToServiceHelper.IsClientTelemetryJobRunning());
            Assert.AreEqual(flagState2, telemetryToServiceHelperFromCollectionCache.IsClientTelemetryJobRunning());
        }
    }
}