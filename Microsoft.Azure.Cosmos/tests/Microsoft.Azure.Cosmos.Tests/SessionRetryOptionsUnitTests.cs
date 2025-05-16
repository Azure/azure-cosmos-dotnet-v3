namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="SessionRetry"/>
    /// </summary>
    [TestClass]
    public class SessionRetryOptionsUnitTests
    {
        [TestMethod]
        public void SessionRetryOptionsValidValuesTest()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, "200");
            Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, "1");
            try
            {
                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    EnableRemoteRegionPreferredForSessionRetry = true,
                };

                Assert.IsTrue(clientOptions.SessionRetryOptions.MinInRegionRetryTime == TimeSpan.FromMilliseconds(200));
                Assert.IsTrue(clientOptions.SessionRetryOptions.MaxInRegionRetryCount == 1);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, null);
            }

        }

        [TestMethod]
        public void SessionRetryOptionsDefaultValuesTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                EnableRemoteRegionPreferredForSessionRetry = true,
            };

            Assert.IsTrue(clientOptions.SessionRetryOptions.MinInRegionRetryTime == TimeSpan.FromMilliseconds(500));
            Assert.IsTrue(clientOptions.SessionRetryOptions.MaxInRegionRetryCount == 1);

        }

        [TestMethod]
        public void SessionRetryOptionsInValidValuesTest()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, "50");
            Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, "0");
            try
            {
                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    EnableRemoteRegionPreferredForSessionRetry = true,
                };

                Assert.IsTrue(clientOptions.SessionRetryOptions.MinInRegionRetryTime == TimeSpan.FromMilliseconds(100));
                Assert.IsTrue(clientOptions.SessionRetryOptions.MaxInRegionRetryCount == 1);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, null);
            }

        }

    }
}