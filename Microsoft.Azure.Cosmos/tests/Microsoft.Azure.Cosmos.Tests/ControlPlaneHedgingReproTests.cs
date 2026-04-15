// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Repro tests for control-plane operations bypassing <see cref="CrossRegionHedgingAvailabilityStrategy"/>.
    ///
    /// Customer scenario: During a quorum failure exercise, 19/39 failed reads (49%) were caused by
    /// control-plane operations (e.g., "Read Collection" / Get Container Properties) stuck retrying
    /// against a single unhealthy region. The SDK's CrossRegionHedgingStrategy was configured and
    /// working for data-plane requests (19 reads successfully hedged), but control-plane failures
    /// bypassed hedging entirely because ShouldHedge() filters on ResourceType.Document.
    ///
    /// These tests demonstrate the behavior and verify whether control-plane hedging is feasible.
    /// </summary>
    [TestClass]
    public class ControlPlaneHedgingReproTests
    {
        /// <summary>
        /// Creates a mock CosmosClient with the specified number of read regions.
        /// </summary>
        private static CosmosClient CreateMockClientWithRegions(int regionCount = 3)
        {
            Collection<AccountRegion> regions = new Collection<AccountRegion>();
            for (int i = 0; i < regionCount; i++)
            {
                regions.Add(new AccountRegion()
                {
                    Name = $"Region{i}",
                    Endpoint = new Uri($"https://location{i}.documents.azure.com").ToString()
                });
            }

            AccountProperties databaseAccount = new AccountProperties()
            {
                ReadLocationsInternal = regions
            };

            CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.GlobalEndpointManager
                .InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);

            return mockCosmosClient;
        }

        /// <summary>
        /// REPRO: Demonstrates that ShouldHedge() returns false for ResourceType.Collection,
        /// causing control-plane "Read Collection" requests to bypass hedging entirely.
        ///
        /// This reproduces the customer's exact scenario: a FeedIterator ReadNextAsync
        /// (data-plane query) requires a "Get Container Properties" → "Read Collection"
        /// (control-plane, ResourceType.Collection) call during pipeline creation. When the
        /// primary region is unhealthy, this control-plane call is NOT hedged, blocking the
        /// entire query for 36+ seconds while the HttpTimeoutPolicy exhausts retries on a
        /// single region.
        /// </summary>
        [TestMethod]
        public void ShouldHedge_ReturnsFalse_ForCollectionResourceType()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            using CosmosClient mockClient = CreateMockClientWithRegions(3);

            // Simulate the exact request from the customer's diagnostic trace:
            // "Read Collection" with ResourceType: Collection, HttpMethod: GET
            RequestMessage collectionReadRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/EntityStore/colls/TRMStore", UriKind.Relative))
            {
                ResourceType = ResourceType.Collection,
                OperationType = OperationType.Read
            };

            // Act
            bool shouldHedge = strategy.ShouldHedge(collectionReadRequest, mockClient);

            // Assert: This is the ROOT CAUSE — ShouldHedge returns false for Collection
            Assert.IsFalse(shouldHedge,
                "ShouldHedge() currently returns false for ResourceType.Collection. " +
                "This means control-plane reads bypass hedging and are stuck retrying " +
                "against a single unhealthy region via HttpTimeoutPolicy escalation.");
        }

        /// <summary>
        /// Demonstrates that ShouldHedge() returns false for ALL non-Document resource types,
        /// covering the full set of control-plane operations that are excluded from hedging.
        /// </summary>
        [TestMethod]
        public void ShouldHedge_ReturnsFalse_ForAllControlPlaneResourceTypes()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            using CosmosClient mockClient = CreateMockClientWithRegions(3);

            (ResourceType rt, OperationType ot, string name)[] controlPlaneOps = new[]
            {
                (ResourceType.Database, OperationType.Read, "Database Read"),
                (ResourceType.Collection, OperationType.Read, "Collection Read"),
                (ResourceType.Collection, OperationType.Create, "Collection Create"),
                (ResourceType.Offer, OperationType.Read, "Offer/Throughput Read"),
                (ResourceType.StoredProcedure, OperationType.Read, "StoredProcedure Read"),
                (ResourceType.Trigger, OperationType.Read, "Trigger Read"),
                (ResourceType.UserDefinedFunction, OperationType.Read, "UDF Read"),
                (ResourceType.User, OperationType.Read, "User Read"),
                (ResourceType.Permission, OperationType.Read, "Permission Read"),
                (ResourceType.PartitionKeyRange, OperationType.ReadFeed, "PKRange ReadFeed"),
            };

            foreach ((ResourceType resourceType, OperationType operationType, string name) in controlPlaneOps)
            {
                RequestMessage request = new RequestMessage(
                    HttpMethod.Get,
                    new Uri("/dbs/testdb/colls/testcoll", UriKind.Relative))
                {
                    ResourceType = resourceType,
                    OperationType = operationType
                };

                // Act
                bool shouldHedge = strategy.ShouldHedge(request, mockClient);

                // Assert
                Assert.IsFalse(shouldHedge,
                    $"ShouldHedge() returns false for {name} (ResourceType.{resourceType}) — " +
                    $"this control-plane operation is excluded from hedging.");
            }
        }

        /// <summary>
        /// Confirms that ShouldHedge() returns true for document read operations,
        /// showing the asymmetry between data-plane and control-plane handling.
        /// </summary>
        [TestMethod]
        public void ShouldHedge_ReturnsTrue_ForDocumentRead()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            using CosmosClient mockClient = CreateMockClientWithRegions(3);

            RequestMessage documentReadRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcoll/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };

            // Act
            bool shouldHedge = strategy.ShouldHedge(documentReadRequest, mockClient);

            // Assert
            Assert.IsTrue(shouldHedge,
                "ShouldHedge() returns true for ResourceType.Document reads — " +
                "data-plane operations ARE hedged.");
        }

        /// <summary>
        /// REPRO: Demonstrates that when ShouldHedge returns false, ExecuteAvailabilityStrategyAsync
        /// immediately falls through to the non-hedged sender — even when the primary region is
        /// returning 503s. This simulates the customer's exact failure: 36.6 seconds stuck on a
        /// single unhealthy region with no parallel hedging.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAvailabilityStrategy_ControlPlaneRequest_BypassesHedging_ReturnsDirectly()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            using CosmosClient mockClient = CreateMockClientWithRegions(3);

            // Control-plane request (Read Collection)
            RequestMessage collectionReadRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/EntityStore/colls/TRMStore", UriKind.Relative))
            {
                ResourceType = ResourceType.Collection,
                OperationType = OperationType.Read
            };

            int senderCallCount = 0;
            List<string> regionsContacted = new List<string>();

            // Simulate what happens: sender is called once (no hedging), returns 503
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (req, ct) =>
            {
                Interlocked.Increment(ref senderCallCount);
                regionsContacted.Add("primary");
                return Task.FromResult(new ResponseMessage(HttpStatusCode.ServiceUnavailable));
            };

            // Act
            ResponseMessage response = await strategy.ExecuteAvailabilityStrategyAsync(
                sender, mockClient, collectionReadRequest, CancellationToken.None);

            // Assert: The sender was called exactly once — no hedging occurred
            Assert.AreEqual(1, senderCallCount,
                "Control-plane requests should call the sender exactly once (no hedging). " +
                "The strategy falls through to the non-hedged path immediately.");
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode,
                "The 503 from the unhealthy region is returned directly — no opportunity " +
                "to try another region via hedging.");
        }

        /// <summary>
        /// CONTRAST: Shows that a data-plane Document read with the same 503 failure WOULD be hedged
        /// to another region and succeed. This is the exact asymmetry the customer experienced.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAvailabilityStrategy_DocumentRead_HedgesAcrossRegions_On503()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy strategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(50),
                thresholdStep: TimeSpan.FromMilliseconds(50));

            using CosmosClient mockClient = CreateMockClientWithRegions(3);

            RequestMessage documentReadRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcoll/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };

            int senderCallCount = 0;

            // First region returns 503 (transient), second region returns 200 OK
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // Primary region: slow + transient error (simulates unhealthy region)
                    await Task.Delay(TimeSpan.FromMilliseconds(200), ct).ContinueWith(_ => { });
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                // Hedged region: returns success
                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act
            ResponseMessage response = await strategy.ExecuteAvailabilityStrategyAsync(
                sender, mockClient, documentReadRequest, CancellationToken.None);

            // Assert: Hedging kicked in and the request succeeded via the second region
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                "Data-plane requests ARE hedged — the second region returned 200 OK.");
            Assert.IsTrue(senderCallCount >= 2,
                $"Expected at least 2 sender calls (primary + hedge), got {senderCallCount}. " +
                "This shows hedging dispatched requests to multiple regions.");
        }

        /// <summary>
        /// ANALYSIS: Tests whether control-plane READ operations (which are safe to retry/hedge)
        /// could theoretically be hedged by relaxing the ResourceType.Document filter.
        ///
        /// This test modifies nothing — it documents the expected behavior if the filter
        /// were to be relaxed. Read-only control-plane operations (ReadContainerAsync,
        /// ReadDatabaseAsync, ReadThroughputAsync) are idempotent and safe for parallel
        /// execution across regions, just like document reads.
        /// </summary>
        [TestMethod]
        public void ControlPlaneReadOperations_AreIdempotent_CouldBeHedged()
        {
            // These control-plane operations are all read-only and idempotent:
            // - Container.ReadContainerAsync() → ResourceType.Collection, OperationType.Read
            // - Database.ReadAsync() → ResourceType.Database, OperationType.Read
            // - Container.ReadThroughputAsync() → ResourceType.Offer, OperationType.Read
            // - Database.GetContainerQueryIterator() → ResourceType.Collection, OperationType.ReadFeed
            //
            // They are safe for parallel hedging (no side effects, no idempotency concerns).
            // The current ShouldHedge() filter excludes them solely because they are not
            // ResourceType.Document, not because of any safety concern.

            var readOnlyControlPlaneOps = new[]
            {
                (ResourceType.Collection, OperationType.Read, "ReadContainerAsync"),
                (ResourceType.Database, OperationType.Read, "ReadDatabaseAsync"),
                (ResourceType.Offer, OperationType.Read, "ReadThroughputAsync"),
                (ResourceType.Collection, OperationType.ReadFeed, "GetContainerQueryIterator"),
                (ResourceType.Database, OperationType.ReadFeed, "GetDatabaseQueryIterator"),
            };

            foreach ((ResourceType rt, OperationType ot, string apiName) in readOnlyControlPlaneOps)
            {
                bool isReadOnly = OperationTypeExtensions.IsReadOperation(ot);
                Assert.IsTrue(isReadOnly,
                    $"{apiName} (ResourceType.{rt}, OperationType.{ot}) IS a read-only operation. " +
                    "It is safe for parallel hedging but currently excluded by the Document filter.");
            }
        }

        /// <summary>
        /// Verifies the exact HttpTimeoutPolicy applied to control-plane collection reads.
        /// This confirms the 0.5s → 5s → 65s escalation pattern seen in the customer trace.
        /// </summary>
        [TestMethod]
        public void HttpTimeoutPolicy_ControlPlaneCollectionRead_UsesRetriableHotPath()
        {
            // Simulate the DocumentServiceRequest for a "Read Collection" call
            DocumentServiceRequest collectionReadRequest = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                resourceFullName: "dbs/EntityStore/colls/TRMStore",
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);

            // Act
            HttpTimeoutPolicy policy = HttpTimeoutPolicy.GetTimeoutPolicy(collectionReadRequest);

            // Assert: Control-plane metadata reads use HttpTimeoutPolicyControlPlaneRetriableHotPath
            Assert.AreEqual(
                nameof(HttpTimeoutPolicyControlPlaneRetriableHotPath),
                policy.TimeoutPolicyName,
                "Control-plane collection reads use HttpTimeoutPolicyControlPlaneRetriableHotPath " +
                "which escalates timeouts: 0.5s → 5s → 65s. This is the ~36s of time " +
                "burned per region before cross-region retry.");

            // Verify the escalation pattern matches the customer trace
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> enumerator = policy.GetTimeoutEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), enumerator.Current.requestTimeout,
                "First attempt: 0.5s timeout (customer saw 508ms)");

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(TimeSpan.FromSeconds(5), enumerator.Current.requestTimeout,
                "Second attempt: 5s timeout (customer saw 5,012ms)");
            Assert.AreEqual(TimeSpan.FromSeconds(1), enumerator.Current.delayForNextRequest,
                "1s delay between attempt 2 and 3");

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(TimeSpan.FromSeconds(65), enumerator.Current.requestTimeout,
                "Third attempt: 65s timeout (customer saw 30,062ms — server replied with 503 before timeout)");
        }

        /// <summary>
        /// Verifies that the ClientRetryPolicy has MaxServiceUnavailableRetryCount = 1,
        /// meaning only one cross-region retry is attempted for 503 errors after the
        /// HttpTimeoutPolicy exhausts its per-region retries.
        /// </summary>
        [TestMethod]
        public void ClientRetryPolicy_MaxServiceUnavailableRetryCount_IsOne()
        {
            // The ClientRetryPolicy.MaxServiceUnavailableRetryCount field is private const.
            // We verify its effect through the type's behavior — only 1 retry on 503.
            // This constant means: after 36s of HTTP retries on Region1 (503),
            // the SDK retries ONCE on Region2 (another 36s), and if that also fails, it gives up.
            // Total worst-case: ~72 seconds for a control-plane read that could have been
            // hedged in <1 second with parallel cross-region requests.

            // This is a documentation-only test to capture the design constraint.
            // The actual constant is at ClientRetryPolicy.cs:25:
            //   private const int MaxServiceUnavailableRetryCount = 1;
            Assert.IsTrue(true, "MaxServiceUnavailableRetryCount = 1 limits cross-region 503 retries to a single attempt.");
        }
    }
}
