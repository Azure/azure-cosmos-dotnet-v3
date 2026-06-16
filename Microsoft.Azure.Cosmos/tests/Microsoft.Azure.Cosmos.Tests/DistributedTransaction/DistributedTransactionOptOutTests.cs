// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Regression tests confirming that distributed transactions are excluded from
    /// PPAF (Per-Partition Automatic Failover), PPCB (Per-Partition Circuit Breaker),
    /// and Cross-Region Hedging. All three features gate on ResourceType.Document;
    /// DTx uses ResourceType.DistributedTransactionBatch and must never activate them.
    /// </summary>
    [TestClass]
    public sealed class DistributedTransactionOptOutTests
    {
        [TestMethod]
        [Description("ShouldHedge returns false for write DTx because ResourceType is not Document.")]
        public void ShouldHedge_WriteDtxRequest_ReturnsFalse()
        {
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            RequestMessage request = new RequestMessage(
                HttpMethod.Post,
                new Uri("https://test.documents.azure.com/operations/dtc"))
            {
                ResourceType = ResourceType.DistributedTransactionBatch,
                OperationType = OperationType.CommitDistributedTransaction,
            };

            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();

            bool shouldHedge = strategy.ShouldHedge(request, mockClient.Object);

            Assert.IsFalse(shouldHedge, "DTx requests must not be hedged — ResourceType is not Document.");
        }

        [TestMethod]
        [Description("ShouldHedge returns false for read DTx with CommitDistributedReadTransaction.")]
        public void ShouldHedge_ReadDtxRequest_ReturnsFalse()
        {
            // Read DTx uses CommitDistributedReadTransaction; ResourceType still blocks hedging.
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            RequestMessage request = new RequestMessage(
                HttpMethod.Post,
                new Uri("https://test.documents.azure.com/operations/dtc"))
            {
                ResourceType = ResourceType.DistributedTransactionBatch,
                OperationType = DistributedTransactionConstants.CommitDistributedReadTransaction,
            };

            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();

            bool shouldHedge = strategy.ShouldHedge(request, mockClient.Object);

            Assert.IsFalse(shouldHedge, "Read DTx must not be hedged — ResourceType is not Document.");
        }

        [TestMethod]
        [Description("TryMarkEndpointUnavailableForPartitionKeyRange and TryAddPartitionLevelLocationOverride return false for DTx.")]
        public void PartitionLevelFailover_DtxResourceType_ReturnsFalse()
        {
            // CanUsePartitionLevelFailoverLocations (which gates both PPAF and PPCB) requires
            // ResourceType.Document. DTx uses DistributedTransactionBatch — must be excluded.
            Mock<IGlobalEndpointManager> mockGem = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);
            mockGem.Setup(x => x.ReadEndpoints).Returns(
                () => new ReadOnlyCollection<Uri>(new List<Uri>()
                {
                    new Uri("https://location1.documents.azure.com/"),
                    new Uri("https://location2.documents.azure.com/"),
                }));

            GlobalPartitionEndpointManagerCore partitionManager = new GlobalPartitionEndpointManagerCore(
                mockGem.Object,
                isPartitionLevelFailoverEnabled: true);

            using DocumentServiceRequest dtxRequest = DocumentServiceRequest.Create(
                OperationType.CommitDistributedTransaction,
                ResourceType.DistributedTransactionBatch,
                AuthorizationTokenType.PrimaryMasterKey);

            dtxRequest.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange { Id = "0" };

            bool markResult = partitionManager.TryMarkEndpointUnavailableForPartitionKeyRange(dtxRequest);
            bool overrideResult = partitionManager.TryAddPartitionLevelLocationOverride(dtxRequest);

            Assert.IsFalse(markResult, "DTx must not mark partition-level endpoints unavailable (PPCB) — ResourceType is not Document.");
            Assert.IsFalse(overrideResult, "DTx must not add PPAF partition-level location overrides — ResourceType is not Document.");
        }
    }
}
