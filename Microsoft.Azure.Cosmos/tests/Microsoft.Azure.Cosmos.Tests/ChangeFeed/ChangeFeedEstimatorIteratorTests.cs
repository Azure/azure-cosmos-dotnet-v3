//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
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

            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            List<string> requestedPKRanges = new List<string>();

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                requestedPKRanges.Add(lease.CurrentLeaseToken);
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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

            Mock<FeedIteratorInternal> mockIteratorPKRange0 = new Mock<FeedIteratorInternal>();
            mockIteratorPKRange0.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:" + globalLsnPKRange0.ToString()));

            Mock<FeedIteratorInternal> mockIteratorPKRange1 = new Mock<FeedIteratorInternal>();
            mockIteratorPKRange1.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "1:" + globalLsnPKRange1.ToString()));

            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                if (lease.CurrentLeaseToken == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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

            Mock<FeedIteratorInternal> mockIteratorPKRange0 = new Mock<FeedIteratorInternal>();
            mockIteratorPKRange0.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.OK, "0:" + globalLsnPKRange0.ToString(), processedLsnPKRange0.ToString()));

            Mock<FeedIteratorInternal> mockIteratorPKRange1 = new Mock<FeedIteratorInternal>();
            mockIteratorPKRange1.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetResponse(HttpStatusCode.OK, "1:" + globalLsnPKRange1.ToString(), processedLsnPKRange1.ToString()));

            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                if (lease.CurrentLeaseToken == "0")
                {
                    return mockIteratorPKRange0.Object;
                }

                return mockIteratorPKRange1.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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

            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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

            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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

            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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
            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponse(HttpStatusCode.NotModified, "0:1"));
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
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
        [Owner("philipthomas-MSFT")]
        [Description("Testing that estimated lag returns a value of 1 when a 410/1002 status/subStatus occurs.")]
        public async Task ShouldReturnEstimatedLagIsOneWhenGoneCosmosException()
        {
            // Arrange
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
            Mock<FeedIteratorInternal> mockIterator = new Mock<FeedIteratorInternal>();
            mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(GetResponseWithGoneStatusCosmosException);
            Mock<DocumentServiceLeaseContainer> mockContainer = new Mock<DocumentServiceLeaseContainer>();
            mockContainer.Setup(c => c.GetAllLeasesAsync()).ReturnsAsync(leases);

            FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return mockIterator.Object;
            }

            // Act

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                ChangeFeedEstimatorIteratorTests.GetMockedContainer(),
                Mock.Of<ContainerInternal>(),
                mockContainer.Object,
                feedCreator,
                null);

            // Assert

            FeedResponse<ChangeFeedProcessorState> firstResponse = await remainingWorkEstimator.ReadNextAsync(default);

            ChangeFeedProcessorState remainingLeaseWork = firstResponse.First();

            Assert.AreEqual(expected: instanceName, actual: remainingLeaseWork.InstanceName);
            Assert.AreEqual(expected: leaseToken, actual: remainingLeaseWork.LeaseToken);
            Assert.AreEqual(expected: 1, actual: remainingLeaseWork.EstimatedLag);
        }

        [TestMethod]
        public async Task ShouldInitializeDocumentLeaseContainer()
        {
            static FeedIteratorInternal feedCreator(DocumentServiceLease lease, string continuationToken, bool startFromBeginning)
            {
                return Mock.Of<FeedIteratorInternal>();
            }

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>(MockBehavior.Strict);
            mockedContext.Setup(c => c.Client).Returns(MockCosmosUtil.CreateMockCosmosClient());
            mockedContext.Setup(x => x.OperationHelperAsync<FeedResponse<ChangeFeedProcessorState>>(
                It.Is<string>(str => str.Contains("Change Feed Estimator")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<FeedResponse<ChangeFeedProcessorState>>>>(),
                It.IsAny<Tuple<string, Func<FeedResponse<ChangeFeedProcessorState>, OpenTelemetryAttributes>>>(),
                It.IsAny<ResourceType?>(),
                It.Is<TraceComponent>(tc => tc == TraceComponent.ChangeFeed),
                It.IsAny<TraceLevel>()))
               .Returns<string, string, string, Documents.OperationType, RequestOptions, Func<ITrace, Task<FeedResponse<ChangeFeedProcessorState>>>, Tuple<string, Func<FeedResponse<ChangeFeedProcessorState>, OpenTelemetryAttributes>>, ResourceType?, TraceComponent, TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) =>
                {
                    using (ITrace trace = Trace.GetRootTrace(operationName, comp, level))
                    {
                        return func(trace);
                    }
                });

            string monitoredContainerRid = "V4lVAMl0wuQ=";
            string databaseRid = Documents.ResourceId.Parse(monitoredContainerRid).DatabaseId.ToString();
            Mock<ContainerInternal> mockedMonitoredContainer = new Mock<ContainerInternal>(MockBehavior.Strict);
            mockedMonitoredContainer.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(monitoredContainerRid);
            mockedMonitoredContainer.Setup(c => c.ClientContext).Returns(mockedContext.Object);
            mockedMonitoredContainer.Setup(c => c.Database.Id).Returns("databaseId");
            mockedMonitoredContainer.Setup(c => c.Id).Returns("containerId");

            Mock<FeedIteratorInternal> leaseFeedIterator = new Mock<FeedIteratorInternal>();
            leaseFeedIterator.Setup(i => i.HasMoreResults).Returns(false);

            Mock<ContainerInternal> mockedLeaseContainer = new Mock<ContainerInternal>(MockBehavior.Strict);
            mockedLeaseContainer.Setup(c => c.GetCachedContainerPropertiesAsync(It.Is<bool>(b => b == false), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ContainerProperties());
            mockedLeaseContainer.Setup(c => c.GetItemQueryStreamIterator(It.Is<string>(queryText => queryText.Contains($"{databaseRid}_{monitoredContainerRid}")), It.Is<string>(continuation => continuation == null), It.IsAny<QueryRequestOptions>()))
                .Returns(leaseFeedIterator.Object);

            ChangeFeedEstimatorIterator remainingWorkEstimator = new ChangeFeedEstimatorIterator(
                mockedMonitoredContainer.Object,
                mockedLeaseContainer.Object,
                documentServiceLeaseContainer: default,
                monitoredContainerFeedCreator: feedCreator,
                changeFeedEstimatorRequestOptions: default);

            await remainingWorkEstimator.ReadNextAsync(default);
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

                message.Content = new CosmosJsonDotNetSerializer().ToStream(new { Documents = new List<JObject>() { firstDocument } });
            }

            return message;
        }

        private static ResponseMessage GetResponseWithGoneStatusCosmosException()
        {
            return new ResponseMessage(
                statusCode: HttpStatusCode.Gone,
                requestMessage: new RequestMessage(
                    method: System.Net.Http.HttpMethod.Get,
                    requestUriString: default,
                    trace: NoOpTrace.Singleton),
                headers: new Headers() { SubStatusCode = SubStatusCodes.PartitionKeyRangeGone },
                cosmosException: default,
                trace: NoOpTrace.Singleton);
        }

        private static ContainerInternal GetMockedContainer()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>(MockBehavior.Strict);
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>(MockBehavior.Strict);
            mockContext.Setup(x => x.Client).Returns(mockClient.Object);
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.Id).Returns("containerId");
            containerMock.Setup(c => c.Database.Id).Returns("databaseId");

            mockContext.Setup(x => x.OperationHelperAsync<FeedResponse<ChangeFeedProcessorState>>(
                It.Is<string>(str => str.Contains("Change Feed Estimator")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<FeedResponse<ChangeFeedProcessorState>>>>(),
                It.IsAny<Tuple<string, Func<FeedResponse<ChangeFeedProcessorState>, OpenTelemetryAttributes>>>(),
                It.IsAny<ResourceType?>(),
                It.Is<TraceComponent>(tc => tc == TraceComponent.ChangeFeed),
                It.IsAny<TraceLevel>()))
               .Returns<string, string, string, Documents.OperationType, RequestOptions, Func<ITrace, Task<FeedResponse<ChangeFeedProcessorState>>>, Tuple<string, Func<FeedResponse<ChangeFeedProcessorState>, OpenTelemetryAttributes>>, ResourceType?, TraceComponent, TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) =>
                {
                    using (ITrace trace = Trace.GetRootTrace(operationName, comp, level))
                    {
                        return func(trace);
                    }
                });
            return containerMock.Object;
        }
    }
}