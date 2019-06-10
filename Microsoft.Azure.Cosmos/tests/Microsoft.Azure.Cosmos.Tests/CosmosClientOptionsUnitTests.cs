//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
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
            string region = CosmosRegions.WestCentralUS;
            ConnectionMode connectionMode = ConnectionMode.Gateway;
            TimeSpan requestTimeout = TimeSpan.FromDays(1);
            int maxConnections = 9001;
            string userAgentSuffix = "testSuffix";
            CosmosRequestHandler preProcessHandler = new TestHandler();
            ApiType apiType = ApiType.Sql;
            int maxRetryAttemptsOnThrottledRequests = 9999;
            TimeSpan maxRetryWaitTime = TimeSpan.FromHours(6);

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
                accountEndPoint: endpoint,
                accountKey: MockCosmosUtil.MockAccountKey);

            CosmosClient cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            CosmosClientOptions clientOptions = cosmosClient.ClientOptions;

            Assert.AreEqual(endpoint, clientOptions.EndPoint.OriginalString, "AccountEndPoint did not save correctly");
            Assert.AreEqual(MockCosmosUtil.MockAccountKey, clientOptions.AccountKey.Key, "AccountKey did not save correctly");

            //Verify the default values are different from the new values
            Assert.AreNotEqual(region, clientOptions.ApplicationRegion);
            Assert.AreNotEqual(connectionMode, clientOptions.ConnectionMode);
            Assert.AreNotEqual(maxConnections, clientOptions.GatewayModeMaxConnectionLimit);
            Assert.AreNotEqual(requestTimeout, clientOptions.RequestTimeout);
            Assert.AreNotEqual(userAgentSuffix, clientOptions.ApplicationName);
            Assert.AreNotEqual(apiType, clientOptions.ApiType);
            Assert.IsNull(clientOptions.CustomHandlers);

            //Verify GetConnectionPolicy returns the correct values for default
            ConnectionPolicy policy = clientOptions.GetConnectionPolicy();
            Assert.AreEqual(ConnectionMode.Direct, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Tcp, policy.ConnectionProtocol);
            Assert.AreEqual(clientOptions.GatewayModeMaxConnectionLimit, policy.MaxConnectionLimit);
            Assert.AreEqual(clientOptions.RequestTimeout, policy.RequestTimeout);

            cosmosClientBuilder.WithApplicationRegion(region)
                .WithConnectionModeGateway(maxConnections)
                .WithRequestTimeout(requestTimeout)
                .WithApplicationName(userAgentSuffix)
                .AddCustomHandlers(preProcessHandler)
                .WithApiType(apiType)
                .WithThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests);

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
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, clientOptions.MaxRetryAttemptsOnThrottledRequests);
            Assert.AreEqual(maxRetryWaitTime, clientOptions.MaxRetryWaitTimeOnThrottledRequests);

            //Verify GetConnectionPolicy returns the correct values
            policy = clientOptions.GetConnectionPolicy();
            Assert.AreEqual(region, policy.PreferredLocations[0]);
            Assert.AreEqual(ConnectionMode.Gateway, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Https, policy.ConnectionProtocol);
            Assert.AreEqual(maxConnections, policy.MaxConnectionLimit);
            Assert.AreEqual(requestTimeout, policy.RequestTimeout);
            Assert.AreEqual(userAgentSuffix, policy.UserAgentSuffix);
            Assert.IsTrue(policy.UseMultipleWriteLocations);
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, policy.RetryOptions.MaxRetryAttemptsOnThrottledRequests);
            Assert.AreEqual((int)maxRetryWaitTime.TotalSeconds, policy.RetryOptions.MaxRetryWaitTimeInSeconds);
        }

        [TestMethod]
        public void VerifyCosmosClientOptionsHasNonePublicNonVirtualSetMethods()
        {
            // All of the public properties and methods should be virtual to allow users to 
            // create unit tests by mocking the different types.
            Type type = typeof(CosmosClientOptions);


            System.Collections.Generic.List<PropertyInfo> publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetSetMethod() != null && x.GetSetMethod().IsPublic && (!x.GetMethod.IsVirtual || !x.SetMethod.IsVirtual)).ToList();

            Assert.IsFalse(publicProperties.Any(), $"CosmosClientOptions should be read only. These are public {string.Join(";", publicProperties.Select(x => x.Name))}");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnBadDelegatingHandler()
        {
            CosmosRequestHandler handler = new TestHandler();
            CosmosRequestHandler innerHandler = new TestHandler();

            //Inner handler is required to be null to allow the client to connect it to other handlers
            handler.InnerHandler = innerHandler;
            new CosmosClientBuilder(CosmosClientOptionsUnitTests.AccountEndpoint, MockCosmosUtil.MockAccountKey)
                .AddCustomHandlers(handler);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullEndpoint()
        {
            new CosmosClientBuilder(null, "testKey");
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
            var cosmosClientBuilder = new CosmosClientBuilder(connectionString);
            var cosmosClient = cosmosClientBuilder.Build(new MockDocumentClient());
            Assert.IsInstanceOfType(cosmosClient.ClientOptions.CosmosSerializerWithWrapperOrDefault, typeof(CosmosJsonSerializerWrapper));
            Assert.AreEqual(cosmosClient.ClientOptions.CosmosSerializerWithWrapperOrDefault, cosmosClient.ClientOptions.SettingsSerializer);

            CosmosJsonSerializer defaultSerializer = cosmosClient.ClientOptions.SettingsSerializer;
            CosmosJsonSerializer mockJsonSerializer = new Mock<CosmosJsonSerializer>().Object;
            cosmosClientBuilder.WithCustomJsonSerializer(mockJsonSerializer);
            var cosmosClientCustom = cosmosClientBuilder.Build(new MockDocumentClient());
            Assert.AreEqual(defaultSerializer, cosmosClientCustom.ClientOptions.SettingsSerializer);
            Assert.AreEqual(mockJsonSerializer, cosmosClientCustom.ClientOptions.CosmosSerializer);
            Assert.IsInstanceOfType(cosmosClientCustom.ClientOptions.CosmosSerializerWithWrapperOrDefault, typeof(CosmosJsonSerializerWrapper));
            Assert.AreEqual(mockJsonSerializer, ((CosmosJsonSerializerWrapper)cosmosClientCustom.ClientOptions.CosmosSerializerWithWrapperOrDefault).InternalJsonSerializer);
        }
    }
}
