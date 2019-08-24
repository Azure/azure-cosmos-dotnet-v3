//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
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

        [TestMethod]
        public void VerifyCosmosConfigurationPropertiesGetUpdated()
        {
            string endpoint = AccountEndpoint;
            string key = Guid.NewGuid().ToString();
            string region = Regions.WestCentralUS;
            ConnectionMode connectionMode = ConnectionMode.Gateway;
            TimeSpan requestTimeout = TimeSpan.FromDays(1);
            int maxConnections = 9001;
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
            TimeSpan idleTcpConnectionTimeout = new TimeSpan(0, 10, 0);
            TimeSpan openTcpConnectionTimeout = new TimeSpan(0, 0, 5);
            int maxRequestsPerTcpConnection = 30;
            int maxTcpConnectionsPerEndpoint = 65535;

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: key);

            CosmosClient cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            CosmosClientOptions clientOptions = cosmosClient.ClientOptions;

            Assert.AreEqual(endpoint, cosmosClient.Endpoint.OriginalString, "AccountEndpoint did not save correctly");
            Assert.AreEqual(key, cosmosClient.AccountKey, "AccountKey did not save correctly");

            //Verify the default values are different from the new values
            Assert.AreNotEqual(region, clientOptions.ApplicationRegion);
            Assert.AreNotEqual(connectionMode, clientOptions.ConnectionMode);
            Assert.AreNotEqual(maxConnections, clientOptions.GatewayModeMaxConnectionLimit);
            Assert.AreNotEqual(requestTimeout, clientOptions.RequestTimeout);
            Assert.AreNotEqual(userAgentSuffix, clientOptions.ApplicationName);
            Assert.AreNotEqual(apiType, clientOptions.ApiType);
            Assert.AreEqual(0, clientOptions.CustomHandlers.Count);
            Assert.IsNull(clientOptions.SerializerOptions);
            Assert.IsNull(clientOptions.Serializer);

            //Verify GetConnectionPolicy returns the correct values for default
            ConnectionPolicy policy = clientOptions.GetConnectionPolicy();
            Assert.AreEqual(ConnectionMode.Direct, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Tcp, policy.ConnectionProtocol);
            Assert.AreEqual(clientOptions.GatewayModeMaxConnectionLimit, policy.MaxConnectionLimit);
            Assert.AreEqual(clientOptions.RequestTimeout, policy.RequestTimeout);
            Assert.IsNull(policy.IdleTcpConnectionTimeout);
            Assert.IsNull(policy.OpenTcpConnectionTimeout);
            Assert.IsNull(policy.MaxRequestsPerTcpConnection);
            Assert.IsNull(policy.MaxTcpConnectionsPerEndpoint);

            cosmosClientBuilder.WithApplicationRegion(region)
                .WithConnectionModeGateway(maxConnections)
                .WithRequestTimeout(requestTimeout)
                .WithApplicationName(userAgentSuffix)
                .AddCustomHandlers(preProcessHandler)
                .WithApiType(apiType)
                .WithThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests)
                .WithSerializerOptions(cosmosSerializerOptions);

            cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            clientOptions = cosmosClient.ClientOptions;

            //Verify all the values are updated
            Assert.AreEqual(region, clientOptions.ApplicationRegion);
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

            //Verify GetConnectionPolicy returns the correct values
            policy = clientOptions.GetConnectionPolicy();
            Assert.AreEqual(region, policy.PreferredLocations[0]);
            Assert.AreEqual(ConnectionMode.Gateway, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Https, policy.ConnectionProtocol);
            Assert.AreEqual(maxConnections, policy.MaxConnectionLimit);
            Assert.AreEqual(requestTimeout, policy.RequestTimeout);
            Assert.IsTrue(policy.UserAgentSuffix.Contains(userAgentSuffix));
            Assert.IsTrue(policy.UseMultipleWriteLocations);
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, policy.RetryOptions.MaxRetryAttemptsOnThrottledRequests);
            Assert.AreEqual((int)maxRetryWaitTime.TotalSeconds, policy.RetryOptions.MaxRetryWaitTimeInSeconds);

            //Verify Direct Mode settings
            cosmosClientBuilder = new CosmosClientBuilder(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: key);
            cosmosClientBuilder.WithConnectionModeDirect(
                idleTcpConnectionTimeout,
                openTcpConnectionTimeout,
                maxRequestsPerTcpConnection,
                maxTcpConnectionsPerEndpoint
            );

            cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            clientOptions = cosmosClient.ClientOptions;

            //Verify all the values are updated
            Assert.AreEqual(idleTcpConnectionTimeout, clientOptions.IdleTcpConnectionTimeout);
            Assert.AreEqual(openTcpConnectionTimeout, clientOptions.OpenTcpConnectionTimeout);
            Assert.AreEqual(maxRequestsPerTcpConnection, clientOptions.MaxRequestsPerTcpConnection);
            Assert.AreEqual(maxTcpConnectionsPerEndpoint, clientOptions.MaxTcpConnectionsPerEndpoint);

            //Verify GetConnectionPolicy returns the correct values
            policy = clientOptions.GetConnectionPolicy();
            Assert.AreEqual(idleTcpConnectionTimeout, policy.IdleTcpConnectionTimeout);
            Assert.AreEqual(openTcpConnectionTimeout, policy.OpenTcpConnectionTimeout);
            Assert.AreEqual(maxRequestsPerTcpConnection, policy.MaxRequestsPerTcpConnection);
            Assert.AreEqual(maxTcpConnectionsPerEndpoint, policy.MaxTcpConnectionsPerEndpoint);
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
            string expectedValue = environmentInformation.ToString();
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions();
            string userAgentSuffix = "testSuffix";
            cosmosClientOptions.ApplicationName = userAgentSuffix;
            Assert.AreEqual(userAgentSuffix, cosmosClientOptions.ApplicationName);
            Assert.AreEqual(userAgentSuffix, cosmosClientOptions.UserAgentContainer.Suffix);
            Assert.IsTrue(cosmosClientOptions.UserAgentContainer.UserAgent.StartsWith(expectedValue));
            Assert.IsTrue(cosmosClientOptions.UserAgentContainer.UserAgent.EndsWith(userAgentSuffix));

            ConnectionPolicy connectionPolicy = cosmosClientOptions.GetConnectionPolicy();
            Assert.AreEqual(userAgentSuffix, connectionPolicy.UserAgentSuffix);
            Assert.IsTrue(connectionPolicy.UserAgentContainer.UserAgent.StartsWith(expectedValue));
            Assert.IsTrue(connectionPolicy.UserAgentContainer.UserAgent.EndsWith(userAgentSuffix));
        }

        [TestMethod]
        public void GetCosmosSerializerWithWrapperOrDefaultTest()
        {
            CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer();
            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = serializer
            };

            CosmosSerializer cosmosSerializer = options.GetCosmosSerializerWithWrapperOrDefault();
            Assert.AreNotEqual(cosmosSerializer, options.PropertiesSerializer, "Serializer should be custom not the default");
            Assert.AreNotEqual(cosmosSerializer, serializer, "Serializer should be in the CosmosJsonSerializerWrapper");

            CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper = cosmosSerializer as CosmosJsonSerializerWrapper;
            Assert.IsNotNull(cosmosJsonSerializerWrapper);
            Assert.AreEqual(cosmosJsonSerializerWrapper.InternalJsonSerializer, serializer);
        }

        [TestMethod]
        public void GetCosmosSerializerWithWrapperOrDefaultWithOptionsTest()
        {
            CosmosSerializationOptions serializerOptions = new CosmosSerializationOptions();
            Assert.IsFalse(serializerOptions.IgnoreNullValues);
            Assert.IsFalse(serializerOptions.Indented);
            Assert.AreEqual(CosmosPropertyNamingPolicy.Default, serializerOptions.PropertyNamingPolicy);

            CosmosClientOptions options = new CosmosClientOptions()
            {
                SerializerOptions = new CosmosSerializationOptions()
                {
                    IgnoreNullValues = true,
                    Indented = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            CosmosSerializer cosmosSerializer = options.GetCosmosSerializerWithWrapperOrDefault();
            Assert.AreNotEqual(cosmosSerializer, options.PropertiesSerializer, "Serializer should be custom not the default");

            CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper = cosmosSerializer as CosmosJsonSerializerWrapper;
            Assert.IsNotNull(cosmosJsonSerializerWrapper);

            // Verify the custom settings are being honored
            dynamic testItem = new { id = "testid", description = (string)null, CamelCaseProperty = "TestCamelCase" };
            using (Stream stream = cosmosSerializer.ToStream<dynamic>(testItem))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    string jsonString = sr.ReadToEnd();
                    // Notice description is not included, camelCaseProperty starts lower case, the white space shows the indents
                    string expectedJsonString = $"{{{Environment.NewLine}  \"id\": \"testid\",{Environment.NewLine}  \"camelCaseProperty\": \"TestCamelCase\"{Environment.NewLine}}}";
                    Assert.AreEqual(expectedJsonString, jsonString);
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnSerializerOptionsWithCustomSerializer()
        {
            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer()
            };

            options.SerializerOptions = new CosmosSerializationOptions();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnCustomSerializerWithSerializerOptions()
        {
            CosmosClientOptions options = new CosmosClientOptions()
            {
                SerializerOptions = new CosmosSerializationOptions()
            };

            options.Serializer = new CosmosJsonDotNetSerializer();
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
        public void AssertJsonSerializer()
        {
            string connectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(connectionString);
            CosmosClient cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            Assert.IsInstanceOfType(cosmosClient.ClientOptions.GetCosmosSerializerWithWrapperOrDefault(), typeof(CosmosJsonSerializerWrapper));
            Assert.AreEqual(cosmosClient.ClientOptions.GetCosmosSerializerWithWrapperOrDefault(), cosmosClient.ClientOptions.PropertiesSerializer);

            CosmosSerializer defaultSerializer = cosmosClient.ClientOptions.PropertiesSerializer;
            CosmosSerializer mockJsonSerializer = new Mock<CosmosSerializer>().Object;
            cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer);
            CosmosClient cosmosClientCustom = cosmosClientBuilder.Build(new MockDocumentClient());
            Assert.AreEqual(defaultSerializer, cosmosClientCustom.ClientOptions.PropertiesSerializer);
            Assert.AreEqual(mockJsonSerializer, cosmosClientCustom.ClientOptions.Serializer);
            Assert.IsInstanceOfType(cosmosClientCustom.ClientOptions.GetCosmosSerializerWithWrapperOrDefault(), typeof(CosmosJsonSerializerWrapper));
            Assert.AreEqual(mockJsonSerializer, ((CosmosJsonSerializerWrapper)cosmosClientCustom.ClientOptions.GetCosmosSerializerWithWrapperOrDefault()).InternalJsonSerializer);
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
            
            Assert.ThrowsException<ArgumentException>(() => { cosmosClientOptions.IdleTcpConnectionTimeout = idleTcpConnectionTimeout; });
            Assert.ThrowsException<ArgumentException>(() => { cosmosClientOptions.OpenTcpConnectionTimeout = openTcpConnectionTimeout; });
            Assert.ThrowsException<ArgumentException>(() => { cosmosClientOptions.MaxRequestsPerTcpConnection = maxRequestsPerTcpConnection; });
            Assert.ThrowsException<ArgumentException>(() => { cosmosClientOptions.MaxTcpConnectionsPerEndpoint = maxTcpConnectionsPerEndpoint; });
        }
    }
}
