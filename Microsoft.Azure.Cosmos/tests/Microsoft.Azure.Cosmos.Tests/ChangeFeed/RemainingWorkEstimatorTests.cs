//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    [TestClass]
    [TestCategory("ChangeFeed")]
    public class RemainingWorkEstimatorTests
    {
        // TODO: Add more tests when Feed Read goes through OM

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseOldSessionToken()
        {
            string oldToken = "0:12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, RemainingWorkEstimatorCore.ExtractLsnFromSessionToken(oldToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionToken()
        {
            string newToken = "0:-1#12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, RemainingWorkEstimatorCore.ExtractLsnFromSessionToken(newToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionTokenWithMultipleRegionalLsn()
        {
            string newTokenWithRegionalLsn = "0:-1#12345#Region1=1#Region2=2";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, RemainingWorkEstimatorCore.ExtractLsnFromSessionToken(newTokenWithRegionalLsn));
        }
    }
}
