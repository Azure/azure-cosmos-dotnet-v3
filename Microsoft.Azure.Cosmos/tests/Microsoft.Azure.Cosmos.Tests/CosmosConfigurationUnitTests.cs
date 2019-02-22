//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosConfigurationUnitTests
    {
        public const string AccountEndpoint = "https://localhost:8081/";
        public const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";

        [TestMethod]
        public void VerifyCosmosConfigurationPropertiesGetUpdated()
        {
            string endpoint = AccountEndpoint;
            string key = Guid.NewGuid().ToString();
            string region = CosmosRegions.WestCentralUS;
            ConnectionMode connectionMode = ConnectionMode.Gateway;
            TimeSpan requestTimeout = TimeSpan.FromDays(1);
            int maxConnections = 9001;
            string userAgentSuffix = "testSuffix";
            CosmosRequestHandler preProcessHandler = new TestHandler();
            ApiType apiType = ApiType.Sql;
            int maxRetryAttemptsOnThrottledRequests = 9999;
            TimeSpan maxRetryWaitTime = TimeSpan.FromHours(6);

            CosmosConfiguration configuration = new CosmosConfiguration(
                accountEndPoint: endpoint,
                accountKey: key);

            Assert.AreEqual(endpoint, configuration.AccountEndPoint.OriginalString, "AccountEndPoint did not save correctly");
            Assert.AreEqual(key, configuration.AccountKey, "AccountKey did not save correctly");

            //Verify the default values are different from the new values
            Assert.AreNotEqual(region, configuration.CurrentRegion);
            Assert.AreNotEqual(connectionMode, configuration.ConnectionMode);
            Assert.AreNotEqual(maxConnections, configuration.MaxConnectionLimit);
            Assert.AreNotEqual(requestTimeout, configuration.RequestTimeout);
            Assert.AreNotEqual(userAgentSuffix, configuration.UserAgentSuffix);
            Assert.AreNotEqual(apiType, configuration.ApiType);
            Assert.IsNull(configuration.CustomHandlers);

            //Verify GetConnectionPolicy returns the correct values for default
            ConnectionPolicy policy = configuration.GetConnectionPolicy();
            Assert.AreEqual(ConnectionMode.Direct, policy.ConnectionMode);
            Assert.AreEqual(Protocol.Tcp, policy.ConnectionProtocol);
            Assert.AreEqual(configuration.MaxConnectionLimit, policy.MaxConnectionLimit);
            Assert.AreEqual(configuration.RequestTimeout, policy.RequestTimeout);

            configuration.UseCurrentRegion(region)
                .UseConnectionModeGateway(maxConnections)
                .UseRequestTimeout(requestTimeout)
                .UseUserAgentSuffix(userAgentSuffix)
                .AddCustomHandlers(preProcessHandler)
                .UseApiType(apiType)
                .UseThrottlingRetryOptions(maxRetryWaitTime, maxRetryAttemptsOnThrottledRequests);

            //Verify all the values are updated
            Assert.AreEqual(region, configuration.CurrentRegion);
            Assert.AreEqual(connectionMode, configuration.ConnectionMode);
            Assert.AreEqual(maxConnections, configuration.MaxConnectionLimit);
            Assert.AreEqual(requestTimeout, configuration.RequestTimeout);
            Assert.AreEqual(userAgentSuffix, configuration.UserAgentSuffix);
            Assert.AreEqual(preProcessHandler, configuration.CustomHandlers[0]);
            Assert.AreEqual(apiType, configuration.ApiType);
            Assert.AreEqual(maxRetryAttemptsOnThrottledRequests, configuration.MaxRetryAttemptsOnThrottledRequests);
            Assert.AreEqual(maxRetryWaitTime, configuration.MaxRetryWaitTimeOnThrottledRequests);

            //Verify GetConnectionPolicy returns the correct values
            policy = configuration.GetConnectionPolicy();
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
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnBadDelegatingHandler()
        {
            CosmosRequestHandler handler = new TestHandler();
            CosmosRequestHandler innerHandler = new TestHandler();

            //Inner handler is required to be null to allow the client to connect it to other handlers
            handler.InnerHandler = innerHandler;
            new CosmosConfiguration(CosmosConfigurationUnitTests.AccountEndpoint, "testKey")
                .AddCustomHandlers(handler);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullEndpoint()
        {
            new CosmosConfiguration(null, "testKey");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullConnectionString()
        {
            new CosmosConfiguration(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnMissingAccountKeyInConnectionString()
        {
            string invalidConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;";
            new CosmosConfiguration(invalidConnectionString);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowOnMissingAccountEndpointInConnectionString()
        {
            string invalidConnectionString = "AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";
            new CosmosConfiguration(invalidConnectionString);
        }

        [TestMethod]
        public void AssertJsonSerializer()
        {
            string connectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";
            var configuration = new CosmosConfiguration(connectionString);
            Assert.IsInstanceOfType(configuration.CosmosJsonSerializer, typeof(CosmosJsonSerializerWrapper));
        }

        [TestMethod]
        public void VerifySslVerificationDefaultsToFalse()
        {
            var configuration = new CosmosConfiguration(ConnectionString);

            //Verify GetConnectionPolicy returns the correct values for default
            ConnectionPolicy policy = configuration.GetConnectionPolicy();
            Assert.AreEqual(false, policy.DisableSslVerification, "DisableSslVerification should default to false");
        }

        [TestMethod]
        public void VerifyDisableSslVerificationIsConfigured()
        {
            var configuration = new CosmosConfiguration(ConnectionString)
            {
                DisableSslVerification = true
            };

            //Verify that ConnectionPolicy.DisableSslVerification is set by CosmosConfiguration
            ConnectionPolicy policy = configuration.GetConnectionPolicy();
            Assert.AreEqual(true, policy.DisableSslVerification, "DisableSslVerification should be set by CosmosConfiguration");
        }
    }
}
