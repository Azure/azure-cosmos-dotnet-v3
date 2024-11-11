//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientOptionsUnitTests
    {
        public const string AccountEndpoint = "https://localhost:8081/";
        public const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";
        public Func<HttpClient> HttpClientFactoryDelegate = () => new HttpClient();

        [TestMethod]
        public void VerifyCosmosConfigurationPropertiesGetUpdated()
        {
            string endpoint = AccountEndpoint;
            string key = MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey;
            string region = Regions.WestCentralUS;
            ConnectionMode connectionMode = ConnectionMode.Gateway;
            TimeSpan requestTimeout = TimeSpan.FromDays(1);
            int maxConnections = 9001;
            string userAgentSuffix = "testSuffix";
            RequestHandler preProcessHandler = new TestHandler();
            ApiType apiType = ApiType.Sql;
            int maxRetryAttemptsOnThrottledRequests = 9999;
            TimeSpan maxRetryWaitTime = TimeSpan.FromHours(6);
            bool enableTcpConnectionEndpointRediscovery = true;
            CosmosSerializationOptions cosmosSerializerOptions = new CosmosSerializationOptions()
            {
                IgnoreNullValues = true,
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            };
            TimeSpan idleTcpConnectionTimeout = new TimeSpan(0, 10, 0);
            TimeSpan openTcpConnectionTimeout = new TimeSpan(0, 0, 5);
            int maxRequestsPerTcpConnection = 30;
            int maxTcpConnectionsPerEndpoint = 65535;
            Cosmos.PortReuseMode portReuseMode = Cosmos.PortReuseMode.PrivatePortPool;
            IWebProxy webProxy = new TestWebProxy();
            Cosmos.ConsistencyLevel consistencyLevel = Cosmos.ConsistencyLevel.ConsistentPrefix;
            Cosmos.PriorityLevel priorityLevel = Cosmos.PriorityLevel.Low;

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: key);

            CosmosClient cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            CosmosClientOptions clientOptions = cosmosClient.ClientOptions;

            Assert.AreEqual(endpoint, cosmosClient.Endpoint.OriginalString, "AccountEndpoint did not save correctly");
            Assert.AreEqual(key, cosmosClient.AccountKey, "AccountKey did not save correctly");

            //Verify the default values are different from the new values
            Assert.AreNotEqual(region, clientOptions.ApplicationRegion);
            Assert.IsNull(clientOptions.ApplicationPreferredRegions);
            Assert.AreNotEqual(connectionMode, clientOptions.ConnectionMode);
            Assert.AreNotEqual(maxConnections, clientOptions.GatewayModeMaxConnectionLimit);
            Assert.AreNotEqual(requestTimeout, clientOptions.RequestTimeout);
            Assert.AreNotEqual(userAgentSuffix, clientOptions.ApplicationName);
            Assert.AreNotEqual(apiType, clientOptions.ApiType);
            Assert.IsFalse(clientOptions.AllowBulkExecution);
            Assert.AreEqual(0, clientOptions.CustomHandlers.Count);
            Assert.IsNull(clientOptions.SerializerOptions);
            Assert.IsNotNull(clientOptions.Serializer);
            Assert.IsNull(clientOptions.WebProxy);
            Assert.IsFalse(clientOptions.LimitToEndpoint);
            Assert.IsTrue(clientOptions.EnableTcpConnectionEndpointRediscovery);
            Assert.IsNull(clientOptions.HttpClientFactory);
            Assert.AreNotEqual(consistencyLevel, clientOptions.ConsistencyLevel);
            Assert.AreNotEqual(priorityLevel, clientOptions.PriorityLevel);
            Assert.IsFalse(clientOptions.EnablePartitionLevelFailover);
            Assert.IsFalse(clientOptions.EnableAdvancedReplicaSelectionForTcp.HasValue);

            //Verify GetConnectionPolicy returns the correct values for default
            ConnectionPolicy policy = clientOptions.GetConnectionPolicy(clientId: 0);
            Assert.AreEqual(ConnectionMode.Direct, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Tcp, policy.ConnectionProtocol);
            Assert.AreEqual(clientOptions.GatewayModeMaxConnectionLimit, policy.MaxConnectionLimit);
            Assert.AreEqual(clientOptions.RequestTimeout, policy.RequestTimeout);
            Assert.IsNull(policy.IdleTcpConnectionTimeout);
            Assert.IsNull(policy.OpenTcpConnectionTimeout);
            Assert.IsNull(policy.MaxRequestsPerTcpConnection);
            Assert.IsNull(policy.MaxTcpConnectionsPerEndpoint);
            Assert.IsTrue(policy.EnableEndpointDiscovery);
            Assert.IsTrue(policy.EnableTcpConnectionEndpointRediscovery);
            Assert.IsNull(policy.HttpClientFactory);
            Assert.AreNotEqual(Cosmos.ConsistencyLevel.Session, clientOptions.ConsistencyLevel);
            Assert.IsFalse(policy.EnablePartitionLevelFailover);
            Assert.IsFalse(clientOptions.EnableAdvancedReplicaSelectionForTcp.HasValue);
#if PREVIEW
            Assert.IsFalse(clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing);
#else
            Assert.IsTrue(clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing);
#endif
            Assert.IsTrue(clientOptions.CosmosClientTelemetryOptions.DisableSendingMetricsToService);

            cosmosClientBuilder.WithApplicationRegion(region)
                .WithConnectionModeGateway(maxConnections, webProxy)
                .WithRequestTimeout(requestTimeout)
                .WithApplicationName(userAgentSuffix)
                .AddCustomHandlers(preProcessHandler)
                .WithApiType(apiType)
                .WithThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests)
                .WithBulkExecution(true)
                .WithSerializerOptions(cosmosSerializerOptions)
                .WithConsistencyLevel(consistencyLevel)
                .WithPriorityLevel(priorityLevel);

            cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            clientOptions = cosmosClient.ClientOptions;
            clientOptions.EnableAdvancedReplicaSelectionForTcp = true;

            //Verify all the values are updated
            Assert.AreEqual(region, clientOptions.ApplicationRegion);
            Assert.IsNull(clientOptions.ApplicationPreferredRegions);
            Assert.AreEqual(connectionMode, clientOptions.ConnectionMode);
            Assert.AreEqual(maxConnections, clientOptions.GatewayModeMaxConnectionLimit);
            Assert.AreEqual(requestTimeout, clientOptions.RequestTimeout);
            Assert.AreEqual(userAgentSuffix, clientOptions.ApplicationName);
            Assert.AreEqual(preProcessHandler, clientOptions.CustomHandlers[0]);
            Assert.AreEqual(apiType, clientOptions.ApiType);
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, clientOptions.MaxRetryAttemptsOnRateLimitedRequests);
            Assert.AreEqual(maxRetryWaitTime, clientOptions.MaxRetryWaitTimeOnRateLimitedRequests);
            Assert.AreEqual(cosmosSerializerOptions.IgnoreNullValues, clientOptions.SerializerOptions.IgnoreNullValues);
            Assert.AreEqual(cosmosSerializerOptions.PropertyNamingPolicy, clientOptions.SerializerOptions.PropertyNamingPolicy);
            Assert.AreEqual(cosmosSerializerOptions.Indented, clientOptions.SerializerOptions.Indented);
            Assert.IsTrue(object.ReferenceEquals(webProxy, clientOptions.WebProxy));
            Assert.IsTrue(clientOptions.AllowBulkExecution);
            Assert.AreEqual(consistencyLevel, clientOptions.ConsistencyLevel);
            Assert.AreEqual(priorityLevel, clientOptions.PriorityLevel);
            Assert.IsFalse(clientOptions.EnablePartitionLevelFailover);
            Assert.IsTrue(clientOptions.EnableAdvancedReplicaSelectionForTcp.HasValue && clientOptions.EnableAdvancedReplicaSelectionForTcp.Value);

            //Verify GetConnectionPolicy returns the correct values
            policy = clientOptions.GetConnectionPolicy(clientId: 0);
            Assert.AreEqual(region, policy.PreferredLocations[0]);
            Assert.AreEqual(ConnectionMode.Gateway, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Https, policy.ConnectionProtocol);
            Assert.AreEqual(maxConnections, policy.MaxConnectionLimit);
            Assert.AreEqual(requestTimeout, policy.RequestTimeout);
            Assert.IsTrue(policy.UserAgentSuffix.Contains(userAgentSuffix));
            Assert.IsTrue(policy.UseMultipleWriteLocations);
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, policy.RetryOptions.MaxRetryAttemptsOnThrottledRequests);
            Assert.AreEqual((int)maxRetryWaitTime.TotalSeconds, policy.RetryOptions.MaxRetryWaitTimeInSeconds);
            Assert.AreEqual((Documents.ConsistencyLevel)consistencyLevel, clientOptions.GetDocumentsConsistencyLevel());
            Assert.IsFalse(policy.EnablePartitionLevelFailover);
            Assert.IsTrue(clientOptions.EnableAdvancedReplicaSelectionForTcp.Value);

            IReadOnlyList<string> preferredLocations = new List<string>() { Regions.AustraliaCentral, Regions.AustraliaCentral2 };
            ISet<Uri> regionalEndpoints = new HashSet<Uri>()
            {
                new Uri("https://testfed2.documents-test.windows-int.net:443/"),
                new Uri("https://testfed4.documents-test.windows-int.net:443/")
            };

            //Verify Direct Mode settings
            cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: key);
            cosmosClientBuilder.WithConnectionModeDirect(
                idleTcpConnectionTimeout,
                openTcpConnectionTimeout,
                maxRequestsPerTcpConnection,
                maxTcpConnectionsPerEndpoint,
                portReuseMode,
                enableTcpConnectionEndpointRediscovery)
                .WithApplicationPreferredRegions(preferredLocations)
                .WithCustomAccountEndpoints(regionalEndpoints)
                .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                {
                    DisableDistributedTracing = false,
                    CosmosThresholdOptions = new CosmosThresholdOptions()
                    {
                        PointOperationLatencyThreshold = TimeSpan.FromMilliseconds(100),
                        NonPointOperationLatencyThreshold = TimeSpan.FromMilliseconds(100)
                    }
                });

            cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            clientOptions = cosmosClient.ClientOptions;
            //Verify all the values are updated
            Assert.AreEqual(idleTcpConnectionTimeout, clientOptions.IdleTcpConnectionTimeout);
            Assert.AreEqual(openTcpConnectionTimeout, clientOptions.OpenTcpConnectionTimeout);
            Assert.AreEqual(maxRequestsPerTcpConnection, clientOptions.MaxRequestsPerTcpConnection);
            Assert.AreEqual(maxTcpConnectionsPerEndpoint, clientOptions.MaxTcpConnectionsPerEndpoint);
            Assert.AreEqual(portReuseMode, clientOptions.PortReuseMode);
            Assert.IsTrue(clientOptions.EnableTcpConnectionEndpointRediscovery);
            CollectionAssert.AreEqual(preferredLocations.ToArray(), clientOptions.ApplicationPreferredRegions.ToArray());
            CollectionAssert.AreEqual(regionalEndpoints.ToArray(), clientOptions.AccountInitializationCustomEndpoints.ToArray());
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), clientOptions.CosmosClientTelemetryOptions.CosmosThresholdOptions.PointOperationLatencyThreshold);
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), clientOptions.CosmosClientTelemetryOptions.CosmosThresholdOptions.NonPointOperationLatencyThreshold);
            Assert.IsFalse(clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing);

            //Verify GetConnectionPolicy returns the correct values
            policy = clientOptions.GetConnectionPolicy(clientId: 0);
            Assert.AreEqual(idleTcpConnectionTimeout, policy.IdleTcpConnectionTimeout);
            Assert.AreEqual(openTcpConnectionTimeout, policy.OpenTcpConnectionTimeout);
            Assert.AreEqual(maxRequestsPerTcpConnection, policy.MaxRequestsPerTcpConnection);
            Assert.AreEqual(maxTcpConnectionsPerEndpoint, policy.MaxTcpConnectionsPerEndpoint);
            Assert.AreEqual(portReuseMode, policy.PortReuseMode);
            Assert.IsTrue(policy.EnableTcpConnectionEndpointRediscovery);
            CollectionAssert.AreEqual(preferredLocations.ToArray(), policy.PreferredLocations.ToArray());
            CollectionAssert.AreEqual(regionalEndpoints.ToArray(), policy.AccountInitializationCustomEndpoints.ToArray());
        }

        /// <summary>
        /// Test to validate that when the partition level failover is enabled with the preferred regions list is missing, then the client
        /// initialization should throw an argument exception and fail. This should hold true for both environment variable and CosmosClientOptions.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Validate that when enevironment variable is used to enable PPAF, the outcome of the test should be same.")]
        [DataRow(false, DisplayName = "Validate that when CosmosClientOptions is used to enable PPAF, the outcome of the test should be same.")]
        public void CosmosClientOptions_WhenPartitionLevelFailoverEnabledAndPreferredRegionsNotSet_ShouldThrowArgumentException(bool useEnvironmentVariable)
        {
            try
            {
                if (useEnvironmentVariable)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelFailoverEnabled, "True");
                }

                string endpoint = AccountEndpoint;
                string key = MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey;
                TimeSpan requestTimeout = TimeSpan.FromDays(1);
                string userAgentSuffix = "testSuffix";
                RequestHandler preProcessHandler = new TestHandler();
                ApiType apiType = ApiType.Sql;
                int maxRetryAttemptsOnThrottledRequests = 9999;
                TimeSpan maxRetryWaitTime = TimeSpan.FromHours(6);
                CosmosSerializationOptions cosmosSerializerOptions = new CosmosSerializationOptions()
                {
                    IgnoreNullValues = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                };

                Cosmos.ConsistencyLevel consistencyLevel = Cosmos.ConsistencyLevel.ConsistentPrefix;
                Cosmos.PriorityLevel priorityLevel = Cosmos.PriorityLevel.Low;

                CosmosClientBuilder cosmosClientBuilder = new(
                    accountEndpoint: endpoint,
                    authKeyOrResourceToken: key);

                cosmosClientBuilder
                    .WithConnectionModeDirect()
                    .WithRequestTimeout(requestTimeout)
                    .WithApplicationName(userAgentSuffix)
                    .AddCustomHandlers(preProcessHandler)
                    .WithApiType(apiType)
                    .WithThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests)
                    .WithSerializerOptions(cosmosSerializerOptions)
                    .WithConsistencyLevel(consistencyLevel)
                    .WithPriorityLevel(priorityLevel);

                if (!useEnvironmentVariable)
                {
                    cosmosClientBuilder
                        .WithPartitionLevelFailoverEnabled();
                }

                ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => cosmosClientBuilder.Build());

                Assert.AreEqual(
                    expected: "ApplicationPreferredRegions is required when EnablePartitionLevelFailover is enabled.",
                    actual: exception.Message);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelFailoverEnabled, null);
            }
        }

        /// <summary>
        /// Test to validate that when the partition level failover is enabled with the preferred regions list is provided, then the client
        /// initialization should be successful. This holds true for both environment variable and CosmosClientOptions.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Validate that when enevironment variable is used to enable PPAF, the outcome of the test should be same.")]
        [DataRow(false, DisplayName = "Validate that when CosmosClientOptions is used to enable PPAF, the outcome of the test should be same.")]
        public void CosmosClientOptions_WhenPartitionLevelFailoverEnabledAndPreferredRegionsSet_ShouldInitializeSuccessfully(bool useEnvironmentVariable)
        {
            try
            {
                if (useEnvironmentVariable)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelFailoverEnabled, "True");
                }

                string endpoint = AccountEndpoint;
                string key = MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey;
                TimeSpan requestTimeout = TimeSpan.FromDays(1);
                string userAgentSuffix = "testSuffix";
                RequestHandler preProcessHandler = new TestHandler();
                ApiType apiType = ApiType.Sql;
                int maxRetryAttemptsOnThrottledRequests = 9999;
                TimeSpan maxRetryWaitTime = TimeSpan.FromHours(6);
                CosmosSerializationOptions cosmosSerializerOptions = new CosmosSerializationOptions()
                {
                    IgnoreNullValues = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                };

                Cosmos.ConsistencyLevel consistencyLevel = Cosmos.ConsistencyLevel.ConsistentPrefix;
                Cosmos.PriorityLevel priorityLevel = Cosmos.PriorityLevel.Low;
                CosmosClientBuilder cosmosClientBuilder = new(
                    accountEndpoint: endpoint,
                    authKeyOrResourceToken: key);

                cosmosClientBuilder
                    .WithConnectionModeDirect()
                    .WithRequestTimeout(requestTimeout)
                    .WithApplicationName(userAgentSuffix)
                    .AddCustomHandlers(preProcessHandler)
                    .WithApiType(apiType)
                    .WithThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests)
                    .WithSerializerOptions(cosmosSerializerOptions)
                    .WithConsistencyLevel(consistencyLevel)
                    .WithPriorityLevel(priorityLevel)
                    .WithPartitionLevelFailoverEnabled()
                    .WithApplicationPreferredRegions(
                        new List<string>()
                        {
                        Regions.NorthCentralUS,
                        Regions.WestUS,
                        Regions.EastAsia,
                        })
                    .WithCustomAccountEndpoints(
                        new HashSet<Uri>()
                        {
                        new Uri("https://testfed2.documents-test.windows-int.net:443/"),
                        new Uri("https://testfed3.documents-test.windows-int.net:443/"),
                        new Uri("https://testfed4.documents-test.windows-int.net:443/"),
                        });

                CosmosClientOptions clientOptions = cosmosClientBuilder.Build().ClientOptions;

                Assert.AreEqual(ConnectionMode.Direct, clientOptions.ConnectionMode);
                Assert.AreEqual(requestTimeout, clientOptions.RequestTimeout);
                Assert.AreEqual(userAgentSuffix, clientOptions.ApplicationName);
                Assert.AreEqual(preProcessHandler, clientOptions.CustomHandlers[0]);
                Assert.AreEqual(apiType, clientOptions.ApiType);
                Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, clientOptions.MaxRetryAttemptsOnRateLimitedRequests);
                Assert.AreEqual(maxRetryWaitTime, clientOptions.MaxRetryWaitTimeOnRateLimitedRequests);
                Assert.AreEqual(cosmosSerializerOptions.IgnoreNullValues, clientOptions.SerializerOptions.IgnoreNullValues);
                Assert.AreEqual(cosmosSerializerOptions.PropertyNamingPolicy, clientOptions.SerializerOptions.PropertyNamingPolicy);
                Assert.AreEqual(cosmosSerializerOptions.Indented, clientOptions.SerializerOptions.Indented);
                Assert.IsFalse(clientOptions.AllowBulkExecution);
                Assert.AreEqual(consistencyLevel, clientOptions.ConsistencyLevel);
                Assert.IsTrue(clientOptions.EnablePartitionLevelFailover);
                Assert.IsNotNull(clientOptions.ApplicationPreferredRegions);
                Assert.IsNotNull(clientOptions.AccountInitializationCustomEndpoints);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelFailoverEnabled, null);
            }
        }

        [TestMethod]
        public void VerifyConsisentencyLevels()
        {
            List<Cosmos.ConsistencyLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().ToList();
            List<Documents.ConsistencyLevel> documentLevels = Enum.GetValues(typeof(Documents.ConsistencyLevel)).Cast<Documents.ConsistencyLevel>().ToList();
            CollectionAssert.AreEqual(cosmosLevels, documentLevels, new EnumComparer(), "Document consistency level is different from cosmos consistency level");

            foreach (Cosmos.ConsistencyLevel consistencyLevel in cosmosLevels)
            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = consistencyLevel
                };

                Assert.AreEqual((int)consistencyLevel, (int)cosmosClientOptions.GetDocumentsConsistencyLevel());
                Assert.AreEqual(consistencyLevel.ToString(), cosmosClientOptions.GetDocumentsConsistencyLevel().ToString());
            }

            CosmosClientOptions cosmosClientOptionsNull = new CosmosClientOptions()
            {
                ConsistencyLevel = null
            };

            Assert.IsNull(cosmosClientOptionsNull.GetDocumentsConsistencyLevel());
        }

        [TestMethod]
        public void VerifyPriorityLevels()
        {
            List<Cosmos.PriorityLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.PriorityLevel)).Cast<Cosmos.PriorityLevel>().ToList();
            List<Documents.PriorityLevel> documentLevels = Enum.GetValues(typeof(Documents.PriorityLevel)).Cast<Documents.PriorityLevel>().ToList();
            CollectionAssert.AreEqual(cosmosLevels, documentLevels, new EnumComparer(), "Document priority level is different from cosmos priority level");

            foreach (Cosmos.PriorityLevel priorityLevel in cosmosLevels)
            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    PriorityLevel = priorityLevel
                };

                Assert.AreEqual(priorityLevel, cosmosClientOptions.PriorityLevel);
            }

            CosmosClientOptions cosmosClientOptionsNull = new CosmosClientOptions()
            {
                PriorityLevel = null
            };

            Assert.IsNull(cosmosClientOptionsNull.PriorityLevel);
        }

        [TestMethod]
        public void VerifyPortReuseModeIsSyncedWithDirect()
        {
            CollectionAssert.AreEqual(
                Enum.GetNames(typeof(PortReuseMode)).OrderBy(x => x).ToArray(),
                Enum.GetNames(typeof(Cosmos.PortReuseMode)).OrderBy(x => x).ToArray()
            );

            CollectionAssert.AreEqual(
                Enum.GetValues(typeof(PortReuseMode)).Cast<int>().ToArray(),
                Enum.GetValues(typeof(Cosmos.PortReuseMode)).Cast<int>().ToArray()
            );
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnBadDelegatingHandler()
        {
            RequestHandler handler = new TestHandler();
            RequestHandler innerHandler = new TestHandler();

            //Inner handler is required to be null to allow the client to connect it to other handlers
            handler.InnerHandler = innerHandler;
            new CosmosClientBuilder(CosmosClientOptionsUnitTests.AccountEndpoint, "testKey")
                .AddCustomHandlers(handler);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullEndpoint()
        {
            new CosmosClientBuilder(null, "testKey");
        }

        [TestMethod]
        public void UserAgentContainsEnvironmentInformation()
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            string expectedValue = "cosmos-netstandard-sdk/" + environmentInformation.ClientVersion;
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions();
            string userAgentSuffix = "testSuffix";
            cosmosClientOptions.ApplicationName = userAgentSuffix;
            Assert.AreEqual(userAgentSuffix, cosmosClientOptions.ApplicationName);
            Cosmos.UserAgentContainer userAgentContainer = cosmosClientOptions.CreateUserAgentContainerWithFeatures(clientId: 0);
            Assert.AreEqual(userAgentSuffix, userAgentContainer.Suffix);
            Assert.IsTrue(userAgentContainer.UserAgent.StartsWith(expectedValue));
            Assert.IsTrue(userAgentContainer.UserAgent.EndsWith(userAgentSuffix));

            ConnectionPolicy connectionPolicy = cosmosClientOptions.GetConnectionPolicy(clientId: 0);
            Assert.AreEqual(userAgentSuffix, connectionPolicy.UserAgentSuffix);
            Assert.IsTrue(connectionPolicy.UserAgentContainer.UserAgent.StartsWith(expectedValue));
            Assert.IsTrue(connectionPolicy.UserAgentContainer.UserAgent.EndsWith(userAgentSuffix));
        }

        [TestMethod]
        public void ValidateThatCustomSerializerGetsOverriddenWhenSTJSerializerEnabled()
        {
            CosmosClientOptions options = new CosmosClientOptions()
            {
                UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions()
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                }
            };

            CosmosClient client = new(
                "https://fake-account.documents.azure.com:443/",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                options
            );

            Assert.AreEqual(typeof(CosmosSystemTextJsonSerializer), client.ClientOptions.Serializer.GetType());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnSerializerOptionsWithCustomSerializer()
        {
            _ = new CosmosClientOptions
            {
                Serializer = new CosmosJsonDotNetSerializer(),
                SerializerOptions = new CosmosSerializationOptions()
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnCustomSerializerWithSerializerOptions()
        {
            _ = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions(),
                Serializer = new CosmosJsonDotNetSerializer()
            };
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test when the client options order is maintained")]
        [DataRow(true, DisplayName = "Test when the client options order is reversed")]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnCustomSerializerWithSTJSerializerEnabled(
            bool reverseOrder)
        {
            _ = reverseOrder
                ? new CosmosClientOptions()
                {
                    Serializer = new CosmosJsonDotNetSerializer(),
                    UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
                }
                : new CosmosClientOptions()
                {
                    UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
                    Serializer = new CosmosJsonDotNetSerializer(),
                };
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test when the client options order is maintained")]
        [DataRow(true, DisplayName = "Test when the client options order is reversed")]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnSerializerOptionsWithSTJSerializerEnabled(
            bool reverseOrder)
        {
            _ = reverseOrder
                ? new CosmosClientOptions()
                {
                    SerializerOptions = new CosmosSerializationOptions(),
                    UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
                }
                : new CosmosClientOptions()
                {
                    UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
                    SerializerOptions = new CosmosSerializationOptions(),
                };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullTokenCredential()
        {
            new CosmosClientBuilder(AccountEndpoint, tokenCredential: null);
        }

        [TestMethod]
        public void VerifyAuthorizationTokenProviderIsSet()
        {
            CosmosClient cosmosClient = new CosmosClientBuilder(
                AccountEndpoint, new Mock<TokenCredential>().Object).Build();
            Assert.IsNotNull(cosmosClient.AuthorizationTokenProvider);
        }

        [TestMethod]
        public void VerifyAccountEndpointIsSet()
        {
            CosmosClient cosmosClient = new CosmosClientBuilder(
                AccountEndpoint, new Mock<TokenCredential>().Object).Build();
            Assert.IsNotNull(cosmosClient.Endpoint);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullConnectionString()
        {
            new CosmosClientBuilder(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnMissingAccountKeyInConnectionString()
        {
            string invalidConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;";
            new CosmosClientBuilder(invalidConnectionString);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnMissingAccountEndpointInConnectionString()
        {
            string invalidConnectionString = "AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";
            new CosmosClientBuilder(invalidConnectionString);
        }

        [TestMethod]
        public void VerifyGetConnectionPolicyThrowIfDirectTcpSettingAreUsedInGatewayMode()
        {
            TimeSpan idleTcpConnectionTimeout = new TimeSpan(0, 10, 0);
            TimeSpan openTcpConnectionTimeout = new TimeSpan(0, 0, 5);
            int maxRequestsPerTcpConnection = 30;
            int maxTcpConnectionsPerEndpoint = 65535;

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway
            };

            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.IdleTcpConnectionTimeout = idleTcpConnectionTimeout);
            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.OpenTcpConnectionTimeout = openTcpConnectionTimeout);
            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.MaxRequestsPerTcpConnection = maxRequestsPerTcpConnection);
            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.MaxTcpConnectionsPerEndpoint = maxTcpConnectionsPerEndpoint);
        }

        [TestMethod]
        public void VerifyHttpClientFactoryBlockedWithConnectionLimit()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                GatewayModeMaxConnectionLimit = 42
            };

            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.HttpClientFactory = () => new HttpClient());

            cosmosClientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient()
            };

            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.GatewayModeMaxConnectionLimit = 42);
        }

        [TestMethod]
        public void VerifyHttpClientHandlerIsSet()
        {
            string endpoint = AccountEndpoint;
            string key = "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==";

            IWebProxy webProxy = new TestWebProxy();

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: key);
            cosmosClientBuilder.WithConnectionModeGateway(
                maxConnectionLimit: null,
                webProxy: webProxy);

            CosmosClient cosmosClient = cosmosClientBuilder.Build();
            CosmosHttpClient cosmosHttpClient = cosmosClient.DocumentClient.httpClient;
            SocketsHttpHandler handler = (SocketsHttpHandler)cosmosHttpClient.HttpMessageHandler;

            Assert.IsTrue(object.ReferenceEquals(webProxy, handler.Proxy));
        }

        [TestMethod]
        public void VerifyCorrectProtocolIsSet()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway };
            Assert.AreEqual(Protocol.Https, cosmosClientOptions.ConnectionProtocol);

            cosmosClientOptions = new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct };
            Assert.AreEqual(Protocol.Tcp, cosmosClientOptions.ConnectionProtocol);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void VerifyLimitToEndpointSettings()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions { ApplicationRegion = Regions.EastUS, LimitToEndpoint = true };

            // For invalid regions GetConnectionPolicy will throw exception
            cosmosClientOptions.GetConnectionPolicy(clientId: 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void VerifyLimitToEndpointSettingsWithPreferredRegions()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions { ApplicationPreferredRegions = new List<string>() { Regions.EastUS }, LimitToEndpoint = true };

            // For invalid regions GetConnectionPolicy will throw exception
            cosmosClientOptions.GetConnectionPolicy(clientId: 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void VerifyApplicationRegionSettingsWithPreferredRegions()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions { ApplicationPreferredRegions = new List<string>() { Regions.EastUS }, ApplicationRegion = Regions.EastUS };

            // For invalid regions GetConnectionPolicy will throw exception
            cosmosClientOptions.GetConnectionPolicy(clientId: 0);
        }

        [TestMethod]
        [DynamicData(nameof(GetPublicRegionNames), DynamicDataSourceType.Method)]
        public void VerifyApplicationRegionSettingsForAllPublicRegions(string regionName)
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions { ApplicationRegion = regionName };

            // For invalid regions GetConnectionPolicy will throw exception
            cosmosClientOptions.GetConnectionPolicy(clientId: 0);
        }

        private static IEnumerable<object[]> GetPublicRegionNames()
        {
            List<object[]> regionNames = new List<object[]>();

            // BindingFlags.FlattenHierarchy MUST for const fields 
            foreach (FieldInfo fieldInfo in typeof(Regions).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                string regionValue = fieldInfo.GetValue(null).ToString();
                regionNames.Add(new object[] { regionValue });
            }

            return regionNames;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void VerifyWebProxyHttpClientFactorySet()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                WebProxy = Mock.Of<WebProxy>(),
                HttpClientFactory = () => new HttpClient()
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void VerifyHttpClientFactoryWebProxySet()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(),
                WebProxy = Mock.Of<WebProxy>()
            };
        }

        [TestMethod]
        public void HttpClientFactoryBuildsConnectionPolicy()
        {
            string endpoint = AccountEndpoint;
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey)
                .WithHttpClientFactory(this.HttpClientFactoryDelegate);
            CosmosClient cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            CosmosClientOptions clientOptions = cosmosClient.ClientOptions;

            Assert.AreEqual(clientOptions.HttpClientFactory, this.HttpClientFactoryDelegate);
            ConnectionPolicy policy = clientOptions.GetConnectionPolicy(clientId: 0);
            Assert.AreEqual(policy.HttpClientFactory, this.HttpClientFactoryDelegate);
        }

        [TestMethod]
        public void WithLimitToEndpointAffectsEndpointDiscovery()
        {
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: AccountEndpoint,
                authKeyOrResourceToken: MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey);

            CosmosClientOptions cosmosClientOptions = cosmosClientBuilder.Build(new MockDocumentClient()).ClientOptions;
            Assert.IsFalse(cosmosClientOptions.LimitToEndpoint);

            ConnectionPolicy connectionPolicy = cosmosClientOptions.GetConnectionPolicy(clientId: 0);
            Assert.IsTrue(connectionPolicy.EnableEndpointDiscovery);

            cosmosClientBuilder
                .WithLimitToEndpoint(true);

            cosmosClientOptions = cosmosClientBuilder.Build(new MockDocumentClient()).ClientOptions;
            Assert.IsTrue(cosmosClientOptions.LimitToEndpoint);

            connectionPolicy = cosmosClientOptions.GetConnectionPolicy(clientId: 0);
            Assert.IsFalse(connectionPolicy.EnableEndpointDiscovery);
        }

        [TestMethod]
        public void WithUnrecognizedApplicationRegionThrows()
        {
            string notAValidAzureRegion = Guid.NewGuid().ToString();

            {
                CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                    accountEndpoint: AccountEndpoint,
                    authKeyOrResourceToken: MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey)
                    .WithApplicationRegion(notAValidAzureRegion);

                ArgumentException argumentException = Assert.ThrowsException<ArgumentException>(() => cosmosClientBuilder.Build());

                Assert.IsTrue(argumentException.Message.Contains(notAValidAzureRegion), $"Expected error message to contain {notAValidAzureRegion} but got: {argumentException.Message}");
            }

            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ApplicationRegion = notAValidAzureRegion
                };

                ArgumentException argumentException = Assert.ThrowsException<ArgumentException>(() => new CosmosClient(AccountEndpoint, MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey, cosmosClientOptions));

                Assert.IsTrue(argumentException.Message.Contains(notAValidAzureRegion), $"Expected error message to contain {notAValidAzureRegion} but got: {argumentException.Message}");
            }
        }

        [TestMethod]
        public void WithQuorumReadWithEventualConsistencyAccount()
        {
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: AccountEndpoint,
                authKeyOrResourceToken: MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey);

            CosmosClientOptions cosmosClientOptions = cosmosClientBuilder.Build(new MockDocumentClient()).ClientOptions;
            Assert.IsFalse(cosmosClientOptions.EnableUpgradeConsistencyToLocalQuorum);

            cosmosClientBuilder
                .AllowUpgradeConsistencyToLocalQuorum();

            cosmosClientOptions = cosmosClientBuilder.Build(new MockDocumentClient()).ClientOptions;
            Assert.IsTrue(cosmosClientOptions.EnableUpgradeConsistencyToLocalQuorum);
        }

        [TestMethod]
        public void VerifyRegionNameFormatConversionForApplicationRegion()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationRegion = "westus2"
            };

            ConnectionPolicy policy = cosmosClientOptions.GetConnectionPolicy(0);

            // Need to see Regions.WestUS2 in the list, but not "westus2"
            bool seenWestUS2 = false;
            bool seenNormalized = false;

            foreach (string region in policy.PreferredLocations)
            {
                if (region == "westus2")
                {
                    seenNormalized = true;
                }

                if (region == Regions.WestUS2)
                {
                    seenWestUS2 = true;
                }
            }
            Assert.IsTrue(seenWestUS2);
            Assert.IsFalse(seenNormalized);
        }

        [TestMethod]
        public void VerifyRegionNameFormatConversionBypassForApplicationRegion()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                // No conversion for expected format.
                ApplicationRegion = Regions.NorthCentralUS
            };

            ConnectionPolicy policy = cosmosClientOptions.GetConnectionPolicy(0);

            Assert.AreEqual(Regions.NorthCentralUS, policy.PreferredLocations[0]);

            // Ignore unknown values. 
            cosmosClientOptions.ApplicationRegion = null;

            policy = cosmosClientOptions.GetConnectionPolicy(0);

            Assert.AreEqual(0, policy.PreferredLocations.Count);

            cosmosClientOptions.ApplicationRegion = string.Empty;
            policy = cosmosClientOptions.GetConnectionPolicy(0);

            Assert.AreEqual(0, policy.PreferredLocations.Count);

            cosmosClientOptions.ApplicationRegion = "Invalid region";
            Assert.ThrowsException<ArgumentException>(() => cosmosClientOptions.GetConnectionPolicy(0));
        }

        [TestMethod]
        public void VerifyRegionNameFormatConversionForApplicationPreferredRegions()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationPreferredRegions = new List<string> { "westus2", "usdodcentral", Regions.ChinaNorth3 }
            };

            ConnectionPolicy policy = cosmosClientOptions.GetConnectionPolicy(0);

            bool seenUSDodCentral = false;
            bool seenWestUS2 = false;
            bool seenChinaNorth3 = false;
            bool seenNormalizedUSDodCentral = false;
            bool seenNormalizedWestUS2 = false;

            foreach (string region in policy.PreferredLocations)
            {
                if (region == Regions.USDoDCentral)
                {
                    seenUSDodCentral = true;
                }

                if (region == Regions.WestUS2)
                {
                    seenWestUS2 = true;
                }

                if (region == Regions.ChinaNorth3)
                {
                    seenChinaNorth3 = true;
                }

                if (region == "westus2")
                {
                    seenNormalizedWestUS2 = true;
                }

                if (region == "usdodcentral")
                {
                    seenNormalizedUSDodCentral = true;
                }
            }

            Assert.IsTrue(seenChinaNorth3);
            Assert.IsTrue(seenWestUS2);
            Assert.IsTrue(seenUSDodCentral);
            Assert.IsFalse(seenNormalizedUSDodCentral);
            Assert.IsFalse(seenNormalizedWestUS2);
        }

        [TestMethod]
        public void VerifyRegionNameFormatConversionBypassForInvalidApplicationPreferredRegions()
        {
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                // List contains valid and invalid values
                ApplicationPreferredRegions = new List<string>
            {
                null,
                string.Empty,
                Regions.JioIndiaCentral,
                "westus2",
                "Invalid region"
            }
            };

            ConnectionPolicy policy = cosmosClientOptions.GetConnectionPolicy(0);

            bool seenJioIndiaCentral = false;
            bool seenWestUS2 = false;
            bool seenNormalized = false;

            foreach (string region in policy.PreferredLocations)
            {
                if (region == Regions.JioIndiaCentral)
                {
                    seenJioIndiaCentral = true;
                }

                if (region == Regions.WestUS2)
                {
                    seenWestUS2 = true;
                }

                if (region == "westus2")
                {
                    seenNormalized = true;
                }
            }

            Assert.IsTrue(seenJioIndiaCentral);
            Assert.IsTrue(seenWestUS2);
            Assert.IsFalse(seenNormalized);
        }

        [TestMethod]
        public void RegionNameMappingTest()
        {
            RegionNameMapper mapper = new RegionNameMapper();

            // Test normalized name
            Assert.AreEqual(Regions.WestUS2, mapper.GetCosmosDBRegionName("westus2"));

            // Test with spaces
            Assert.AreEqual(Regions.WestUS2, mapper.GetCosmosDBRegionName("west us 2"));

            // Test for case insenstive
            Assert.AreEqual(Regions.WestUS2, mapper.GetCosmosDBRegionName("wEsTuS2"));
        }

        [TestMethod]
        public void InvalidApplicationNameCatchTest()
        {

            string[] illegalChars = new string[] { "<", ">", "\"", "{", "}", "\\", "[", "]", ";", ":", "@", "=", "(", ")", "," };
            string baseName = "illegal";

            foreach (string illegal in illegalChars)
            {
                Assert.ThrowsException<ArgumentException>(() => new CosmosClientOptions
                {
                    ApplicationName = baseName + illegal
                });


                Assert.ThrowsException<ArgumentException>(() => new CosmosClientOptions
                {
                    ApplicationName = illegal + baseName
                });

                Assert.ThrowsException<ArgumentException>(() => new CosmosClientOptions
                {
                    ApplicationName = illegal
                });
            }
        }

        [TestMethod]
        [DataRow(ConnectionString, false)]
        [DataRow(ConnectionString + "DisableServerCertificateValidation=true;", true)]
        [DataRow(ConnectionString + "DisableServerCertificateValidation=false;", false)]
        public void TestServerCertificatesValidationCallback(string connStr, bool expectedIgnoreCertificateFlag)
        {
            //Arrange
            X509Certificate2 x509Certificate2 = new CertificateRequest("cn=www.test", ECDsa.Create(), HashAlgorithmName.SHA256).CreateSelfSigned(DateTime.Now, DateTime.Now.AddYears(1));
            X509Chain x509Chain = new X509Chain();
            SslPolicyErrors sslPolicyErrors = new SslPolicyErrors();

            CosmosClient cosmosClient = new CosmosClient(connStr);

            if (expectedIgnoreCertificateFlag)
            {
                Assert.IsNull(cosmosClient.ClientOptions.ServerCertificateCustomValidationCallback);
                Assert.IsNull(cosmosClient.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback);
                Assert.IsTrue(cosmosClient.ClientOptions.DisableServerCertificateValidation);
                Assert.IsTrue(cosmosClient
                    .ClientOptions
                    .GetServerCertificateCustomValidationCallback()(x509Certificate2, x509Chain, sslPolicyErrors));


                CosmosHttpClient httpClient = cosmosClient.DocumentClient.httpClient;
                SocketsHttpHandler socketsHttpHandler = (SocketsHttpHandler)httpClient.HttpMessageHandler;

                RemoteCertificateValidationCallback httpClientRemoreCertValidationCallback = socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback;
                Assert.IsNotNull(httpClientRemoreCertValidationCallback);

                Assert.IsTrue(httpClientRemoreCertValidationCallback(this, x509Certificate2, x509Chain, sslPolicyErrors));
            }
            else
            {
                Assert.IsNull(cosmosClient.ClientOptions.ServerCertificateCustomValidationCallback);
                Assert.IsFalse(cosmosClient.ClientOptions.DisableServerCertificateValidation);

                Assert.IsNull(cosmosClient.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback);
            }
        }

        [TestMethod]
        [DataRow(ConnectionString + "DisableServerCertificateValidation=true;", true)]
        [DataRow(ConnectionString + "DisableServerCertificateValidation=true;", false)]
        public void TestServerCertificatesValidationWithDisableSSLFlagTrue(string connStr, bool setCallback)
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                ServerCertificateCustomValidationCallback = (certificate, chain, sslPolicyErrors) => true,
            };

            if (setCallback)
            {
                options.DisableServerCertificateValidationInvocationCallback = () => { };
            }

            CosmosClient cosmosClient = new CosmosClient(connStr, options);
            Assert.IsTrue(cosmosClient.ClientOptions.DisableServerCertificateValidation);
            Assert.AreEqual(cosmosClient.ClientOptions.ServerCertificateCustomValidationCallback, options.ServerCertificateCustomValidationCallback);
            Assert.AreEqual(cosmosClient.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback, options.ServerCertificateCustomValidationCallback);

            CosmosHttpClient httpClient = cosmosClient.DocumentClient.httpClient;
            SocketsHttpHandler socketsHttpHandler = (SocketsHttpHandler)httpClient.HttpMessageHandler;

#nullable enable
            RemoteCertificateValidationCallback? httpClientRemoreCertValidationCallback = socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback;
            Assert.IsNotNull(httpClientRemoreCertValidationCallback);
#nullable disable
        }

        private class TestWebProxy : IWebProxy
        {
            public ICredentials Credentials { get; set; }

            public Uri GetProxy(Uri destination)
            {
                return new Uri("https://www.test.com");
            }

            public bool IsBypassed(Uri host)
            {
                return false;
            }
        }

        private class EnumComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if ((int)x == (int)y &&
                    string.Equals(x.ToString(), y.ToString()))
                {
                    return 0;
                }

                return 1;
            }
        }
    }
}