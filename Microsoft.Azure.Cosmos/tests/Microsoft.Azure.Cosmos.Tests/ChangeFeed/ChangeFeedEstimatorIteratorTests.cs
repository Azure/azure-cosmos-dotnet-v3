//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedEstimatorIteratorTests
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

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                requestedPKRanges.Add(lease.CurrentLeaseToken);
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            await remainingWorkEstimator.ReadNextAsync(default);
            CollectionAssert.AreEquivalent(expectedPKRanges, requestedPKRanges);
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

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                if (lease.CurrentLeaseToken == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            long estimation = 0;
            while (remainingWorkEstimator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> response = await remainingWorkEstimator.ReadNextAsync(default);
                estimation += response.Sum(e => e.EstimatedLag);
            }

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

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                if (lease.CurrentLeaseToken == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            long estimation = 0;
            while (remainingWorkEstimator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> response = await remainingWorkEstimator.ReadNextAsync(default);
                estimation += response.Sum(e => e.EstimatedLag);
            }

            Assert.AreEqual(expectedTotal, estimation);
        }

        [TestMethod]
        public async Task ShouldReturnAllLeasesInOnePage()
        {
            // no max item count
            await this.ShouldReturnAllLeasesInOnePage(null);

            // higher max item count
            await this.ShouldReturnAllLeasesInOnePage(new ChangeFeedEstimatorRequestOptions() { MaxItemCount = 10 });
        }

        private async Task ShouldReturnAllLeasesInOnePage(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions)
        {
            List<string> ranges = new List<string>() { "0", "1" };

            List<DocumentServiceLeaseCore> leases = ranges.Select(pkRangeId => new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            }).ToList();

            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                changeFeedEstimatorRequestOptions);

            FeedResponse<ChangeFeedProcessorState> response = await remainingWorkEstimator.ReadNextAsync(default);

            Assert.IsFalse(remainingWorkEstimator.HasMoreResults);
            Assert.AreEqual(ranges.Count, response.Count);
        }

        [TestMethod]
        public async Task ShouldReturnAllLeasesInPages()
        {
            const int pageSize = 1;
            List<string> ranges = new List<string>() { "0", "1" };

            List<DocumentServiceLeaseCore> leases = ranges.Select(pkRangeId => new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            }).ToList();

            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                new ChangeFeedEstimatorRequestOptions() { MaxItemCount = pageSize }); // Expect multiple pages

            FeedResponse<ChangeFeedProcessorState> firstResponse = await remainingWorkEstimator.ReadNextAsync(default);

            Assert.IsTrue(remainingWorkEstimator.HasMoreResults);
            Assert.AreEqual(pageSize, firstResponse.Count);

            FeedResponse<ChangeFeedProcessorState> secondResponse = await remainingWorkEstimator.ReadNextAsync(default);

            Assert.IsFalse(remainingWorkEstimator.HasMoreResults);
            Assert.AreEqual(pageSize, secondResponse.Count);
        }

        [TestMethod]
        public async Task ShouldAggregateRUAndDiagnostics()
        {
            List<string> ranges = new List<string>() { "0", "1" };

            List<DocumentServiceLeaseCore> leases = ranges.Select(pkRangeId => new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            }).ToList();

            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            FeedResponse<ChangeFeedProcessorState> response = await remainingWorkEstimator.ReadNextAsync(default);

            Assert.AreEqual(2, response.Headers.RequestCharge, "Should contain the sum of all RU charges for each partition read."); // Each request costs 1 RU

            Assert.AreEqual(2, response.Count, $"Should contain one result per range");
        }

        [TestMethod]
        public async Task ReportsInstanceNameAndToken()
        {
            string instanceName = Guid.NewGuid().ToString();
            string leaseToken = Guid.NewGuid().ToString();
            List<string> ranges = new List<string>() { leaseToken };

            List<DocumentServiceLeaseCore> leases = new List<DocumentServiceLeaseCore>() {
                new DocumentServiceLeaseCore()
                {
                    LeaseToken = leaseToken,
                    Owner = instanceName
                }
            };
            Mock<FeedIterator> mockIterator = new Mock<FeedIterator>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIterator feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                Mock.Of<ContainerInternal>(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            FeedResponse<ChangeFeedProcessorState> firstResponse = await remainingWorkEstimator.ReadNextAsync(default);

            ChangeFeedProcessorState remainingLeaseWork = firstResponse.First();

            Assert.AreEqual(instanceName, remainingLeaseWork.InstanceName);
            Assert.AreEqual(leaseToken, remainingLeaseWork.LeaseToken);
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseOldSessionToken()
        {
            string oldToken = "0:12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, ChangeFeedEstimatorIterator.ExtractLsnFromSessionToken(oldToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionToken()
        {
            string newToken = "0:-1#12345";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, ChangeFeedEstimatorIterator.ExtractLsnFromSessionToken(newToken));
        }

        [TestMethod]
        public void ExtractLsnFromSessionToken_ShouldParseNewSessionTokenWithMultipleRegionalLsn()
        {
            string newTokenWithRegionalLsn = "0:-1#12345#Region1=1#Region2=2";
            string expectedLsn = "12345";
            Assert.AreEqual(expectedLsn, ChangeFeedEstimatorIterator.ExtractLsnFromSessionToken(newTokenWithRegionalLsn));
        }

        private static ResponseMessage GetResponse(HttpStatusCode statusCode, string localLsn, string itemLsn = null)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.Headers.Add(Documents.HttpConstants.HttpHeaders.SessionToken, localLsn);
            message.Headers.Add(Documents.HttpConstants.HttpHeaders.RequestCharge, "1");
            if (!string.IsNullOrEmpty(itemLsn))
            {
                JObject firstDocument = new JObject
                {
                    ["_lsn"] = itemLsn
                };

                message.Content = new CosmosJsonDotNetSerializer().ToStream( new { Documents = new List<JObject>() { firstDocument } });
            }

            return message;
        }
    }
}
