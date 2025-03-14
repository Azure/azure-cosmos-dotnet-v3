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
        public void SessionRetryOptionsDefaultValuesTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                SessionRetryOptions = new SessionRetryOptions()
                {
                    RemoteRegionPreferred = true
                },
            };


            Assert.IsTrue(clientOptions.SessionRetryOptions.MinInRegionRetryTime == ConfigurationManager.GetMinRetryTimeInLocalRegionWhenRemoteRegionPreferred());
            Assert.IsTrue(clientOptions.SessionRetryOptions.MaxInRegionRetryCount == ConfigurationManager.GetMaxRetriesInLocalRegionWhenRemoteRegionPreferred());

        }

        [TestMethod]
        public void SessionRetryOptionsCustomValuesTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                SessionRetryOptions = new SessionRetryOptions()
                {
                    RemoteRegionPreferred = true,
                    MinInRegionRetryTime = TimeSpan.FromSeconds(1),
                    MaxInRegionRetryCount = 3

                },
            };

            Assert.IsTrue(clientOptions.SessionRetryOptions.MinInRegionRetryTime == TimeSpan.FromSeconds(1));
            Assert.IsTrue(clientOptions.SessionRetryOptions.MaxInRegionRetryCount == 3);

        }

        [TestMethod]
        public void SessionRetryOptionsMinMaxRetriesCountEnforcedTest()
        {

            ArgumentException argumentException = Assert.ThrowsException<ArgumentException>(() =>
              new CosmosClientOptions()
              {
                  SessionRetryOptions = new SessionRetryOptions()
                  {
                      RemoteRegionPreferred = true,
                      MaxInRegionRetryCount = 0

                  },
              }
            );
            Assert.IsNotNull(argumentException);

        }


        [TestMethod]
        public void SessionRetryOptionsMinMinRetryTimeEnforcedTest()
        {

            ArgumentException argumentException = Assert.ThrowsException<ArgumentException>(() =>
              new CosmosClientOptions()
              {
                  SessionRetryOptions = new SessionRetryOptions()
                  {
                      RemoteRegionPreferred = true,
                      MinInRegionRetryTime = TimeSpan.FromMilliseconds(99)

                  },
              }
            );
            Assert.IsNotNull(argumentException);

        }

    }
}
