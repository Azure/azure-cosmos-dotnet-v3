//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
using Microsoft.Azure.Cosmos.Query.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    [TestClass]
    [TestCategory("ChangeFeed")]
    public class RemainingWorkEstimatorTests
    {
        [TestMethod]
        public async Task ShouldRequestForAllPartitionKeyRanges()
        {
            List<string> expectedPKRanges = new List<string>() { "0", "1" };

            List<DocumentServiceLeaseCore> leases = expectedPKRanges.Select(pkRangeId => new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            }).ToList();

            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            List<string> requestedPKRanges = new List<string>();

            Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
            {
                requestedPKRanges.Add(partitionKeyRangeId);
                return mockIterator.Object;
            };

            FeedManagement.ChangeFeedEstimatorCore remainingWorkEstimator = new FeedManagement.ChangeFeedEstimatorCore(
               mockContainer.Object,
               feedCreator,
               1);

            await remainingWorkEstimator.GetEstimatedRemainingWorkAsync(CancellationToken.None);
            CollectionAssert.AreEqual(expectedPKRanges, requestedPKRanges);

        }

        [TestMethod]
        public async Task ShouldReturnOneWhenNoLeases()
        {
            long expectedTotal = 1;

            List<DocumentServiceLeaseCore> leases = new List<DocumentServiceLeaseCore>();

            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
            {
                return mockIterator.Object;
            };

            FeedManagement.ChangeFeedEstimatorCore remainingWorkEstimator = new FeedManagement.ChangeFeedEstimatorCore(
               mockContainer.Object,
               feedCreator,
               1);

            long estimation = await remainingWorkEstimator.GetEstimatedRemainingWorkAsync(CancellationToken.None);

            Assert.AreEqual(expectedTotal, estimation);
        }

        [TestMethod]
        public async Task ShouldReturnZeroWhenNoItems()
        {
            long globalLsnPKRange0 = 10;
            long globalLsnPKRange1 = 30;
            long expectedTotal = 0;

            List<DocumentServiceLeaseCore> leases = new List<DocumentServiceLeaseCore>(){
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0"
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = "1"
                }
            };

            Mock<FeedIterator> mockIteratorPKRange0 = new Mock<FeedIterator>();
            mockIteratorPKRange0.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:" + globalLsnPKRange0.ToString()));

            Mock<FeedIterator> mockIteratorPKRange1 = new Mock<FeedIterator>();
            mockIteratorPKRange1.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "1:" + globalLsnPKRange1.ToString()));

            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
            {
                if (partitionKeyRangeId == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            };

            FeedManagement.ChangeFeedEstimatorCore remainingWorkEstimator = new FeedManagement.ChangeFeedEstimatorCore(
               mockContainer.Object,
               feedCreator,
               1);

            long estimation = await remainingWorkEstimator.GetEstimatedRemainingWorkAsync(CancellationToken.None);

            Assert.AreEqual(expectedTotal, estimation);
        }

        [TestMethod]
        public async Task ShouldReturnEstimationFromLSNWhenResponseContainsItems()
        {
            long globalLsnPKRange0 = 10;
            long processedLsnPKRange0 = 5;
            long globalLsnPKRange1 = 30;
            long processedLsnPKRange1 = 15;
            long expectedTotal = globalLsnPKRange0 - processedLsnPKRange0 + globalLsnPKRange1 - processedLsnPKRange1 + 2; /* 2 because it doesnt take into consideration the current one */

            List<DocumentServiceLeaseCore> leases = new List<DocumentServiceLeaseCore>(){
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = "0"
                },
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = "1"
                }
            };

            Mock<FeedIterator> mockIteratorPKRange0 = new Mock<FeedIterator>();
            mockIteratorPKRange0.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.OK, "0:" + globalLsnPKRange0.ToString(), processedLsnPKRange0.ToString()));

            Mock<FeedIterator> mockIteratorPKRange1 = new Mock<FeedIterator>();
            mockIteratorPKRange1.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.OK, "1:" + globalLsnPKRange1.ToString(), processedLsnPKRange1.ToString()));

            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
            {
                if (partitionKeyRangeId == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            };

            FeedManagement.ChangeFeedEstimatorCore remainingWorkEstimator = new FeedManagement.ChangeFeedEstimatorCore(
               mockContainer.Object,
               feedCreator,
               1);

            long estimation = await remainingWorkEstimator.GetEstimatedRemainingWorkAsync(CancellationToken.None);

            Assert.AreEqual(expectedTotal, estimation);
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseOldSessionToken()
        {
            string oldToken = "0:12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, FeedManagement.ChangeFeedEstimatorCore.ExtractLsnFromSessionToken(oldToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionToken()
        {
            string newToken = "0:-1#12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, FeedManagement.ChangeFeedEstimatorCore.ExtractLsnFromSessionToken(newToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionTokenWithMultipleRegionalLsn()
        {
            string newTokenWithRegionalLsn = "0:-1#12345#Region1=1#Region2=2";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, FeedManagement.ChangeFeedEstimatorCore.ExtractLsnFromSessionToken(newTokenWithRegionalLsn));
        }

        private static ResponseMessage GetResponse(HttpStatusCode statusCode, string localLsn, string itemLsn = null)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.Headers.Add(Documents.HttpConstants.HttpHeaders.SessionToken, localLsn);
            if (!string.IsNullOrEmpty(itemLsn))
            {
                JObject firstDocument = new JObject();
                firstDocument["_lsn"] = itemLsn;

                message.Content = new CosmosJsonDotNetSerializer().ToStream( new { Documents = new List<JObject>() { firstDocument } });
            }

            return message;
        }
    }
}
