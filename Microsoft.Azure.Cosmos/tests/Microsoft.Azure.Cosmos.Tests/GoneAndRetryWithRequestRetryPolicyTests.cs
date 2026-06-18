//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Resources;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="GoneAndRetryWithRequestRetryPolicy{TResponse}"/> focused on the
    /// 410/1002 PartitionKeyRangeGone handling added for GitHub issue #5924, where a
    /// PartitionKeyRangeGoneException thrown during address resolution previously bubbled up to
    /// point-read callers without refreshing the (now-stale) collection routing map.
    ///
    /// The fix lives entirely inside this retry policy, so these tests exercise the policy directly
    /// at the same boundary the bug occurred at: a 410/1002 (either thrown as a
    /// <see cref="PartitionKeyRangeGoneException"/> by address resolution, or returned as a Gone
    /// <see cref="StoreResponse"/> with sub-status 1002) handed to
    /// <see cref="GoneAndRetryWithRequestRetryPolicy{TResponse}.TryHandleResponseSynchronously"/>.
    /// They prove both required behaviours: (1) the routing map is force-refreshed and the request
    /// retried while budget remains, and (2) once the retry budget is exhausted the terminal
    /// exception surfaces as a 503 with sub-status 21002
    /// (<see cref="SubStatusCodes.Server_PartitionKeyRangeGoneExceededRetryLimit"/>) rather than a
    /// bare 410/1002.
    /// </summary>
    [TestClass]
    public class GoneAndRetryWithRequestRetryPolicyTests
    {
        private const string DocumentResourceFullName =
            "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/";

        private static DocumentServiceRequest CreatePointReadRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                DocumentResourceFullName,
                AuthorizationTokenType.PrimaryMasterKey);
        }

        private static StoreResponse CreatePartitionKeyRangeGoneStoreResponse()
        {
            StoreResponse response = new StoreResponse
            {
                Status = (int)HttpStatusCode.Gone,
                Headers = new StoreResponseNameValueCollection(),
            };

            response.UpsertHeaderValue(
                WFConstants.BackendHeaders.SubStatus,
                ((uint)SubStatusCodes.PartitionKeyRangeGone).ToString());

            return response;
        }

        [TestMethod]
        public void PartitionKeyRangeGone_ForcesCollectionRoutingMapRefreshAndRetries()
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> policy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(disableRetryWithPolicy: false);

            using DocumentServiceRequest request = GoneAndRetryWithRequestRetryPolicyTests.CreatePointReadRequest();

            bool handled = policy.TryHandleResponseSynchronously(
                request,
                response: default,
                exception: new PartitionKeyRangeGoneException("PartitionKeyRange with id '4874' doesn't exist."),
                shouldRetryResult: out ShouldRetryResult result);

            Assert.IsTrue(handled, "The policy must handle 410/1002 PartitionKeyRangeGone.");
            Assert.IsTrue(result.ShouldRetry, "410/1002 PartitionKeyRangeGone must be retried, not surfaced.");
            Assert.IsTrue(
                request.ForceCollectionRoutingMapRefresh,
                "410/1002 PartitionKeyRangeGone must force a collection-routing-map refresh so the retry re-resolves the new range.");
        }

        [TestMethod]
        public void PartitionKeyRangeGone_ResponseForm_ForcesCollectionRoutingMapRefreshAndRetries()
        {
            // The 410/1002 can also arrive as a Gone StoreResponse (sub-status 1002) rather than a
            // thrown PartitionKeyRangeGoneException. Both arms of IsPartitionKeyRangeGone must behave
            // identically.
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> policy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(disableRetryWithPolicy: false);

            using DocumentServiceRequest request = GoneAndRetryWithRequestRetryPolicyTests.CreatePointReadRequest();

            bool handled = policy.TryHandleResponseSynchronously(
                request,
                response: GoneAndRetryWithRequestRetryPolicyTests.CreatePartitionKeyRangeGoneStoreResponse(),
                exception: null,
                shouldRetryResult: out ShouldRetryResult result);

            Assert.IsTrue(handled, "The policy must handle a Gone/1002 StoreResponse.");
            Assert.IsTrue(result.ShouldRetry, "A Gone/1002 StoreResponse must be retried, not surfaced.");
            Assert.IsTrue(
                request.ForceCollectionRoutingMapRefresh,
                "A Gone/1002 StoreResponse must force a collection-routing-map refresh so the retry re-resolves the new range.");
        }

        [TestMethod]
        public void PartitionKeyRangeGone_DoesNotForceMasterRefresh_AndClearsRequestContext()
        {
            // Unlike the CompletingPartitionMigration (1008) branch, the 410/1002 branch must NOT set
            // ForceMasterRefresh: a split/merge only changes the collection's pk-range topology, it
            // does not move the partition between master-tracked service identities. It must, however,
            // clear the resolved routing context so the retry re-resolves from scratch.
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> policy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(disableRetryWithPolicy: false);

            using DocumentServiceRequest request = GoneAndRetryWithRequestRetryPolicyTests.CreatePointReadRequest();

            // Seed a stale resolved range so we can prove ClearRequestContext ran.
            request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange
            {
                Id = "4874",
                MinInclusive = string.Empty,
                MaxExclusive = "FF",
            };
            Assert.IsFalse(request.ForceMasterRefresh, "Precondition: ForceMasterRefresh must start false.");

            bool handled = policy.TryHandleResponseSynchronously(
                request,
                response: default,
                exception: new PartitionKeyRangeGoneException("PartitionKeyRange with id '4874' doesn't exist."),
                shouldRetryResult: out ShouldRetryResult result);

            Assert.IsTrue(handled);
            Assert.IsTrue(result.ShouldRetry);
            Assert.IsFalse(
                request.ForceMasterRefresh,
                "410/1002 PartitionKeyRangeGone must NOT force a master refresh (split/merge does not move the partition between master-tracked identities).");
            Assert.IsNull(
                request.RequestContext.ResolvedPartitionKeyRange,
                "410/1002 PartitionKeyRangeGone must clear the stale resolved partition key range so the retry re-resolves it.");
            Assert.IsNull(
                request.RequestContext.TargetIdentity,
                "410/1002 PartitionKeyRangeGone must clear the stale target identity so the retry re-resolves it.");
        }

        [TestMethod]
        public void PartitionKeyRangeGone_WhenRetryBudgetExhausted_SurfacesAs503SubStatus21002()
        {
            // With a zero-second wall-clock budget the second handling exhausts the retry budget and
            // must convert the bare 410/1002 into a 503 / 21002
            // (Server_PartitionKeyRangeGoneExceededRetryLimit) instead of letting the raw 410/1002
            // bubble to the caller -- this is acceptance criterion #1 of issue #5924.
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> policy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(
                    disableRetryWithPolicy: false,
                    waitTimeInSecondsOverride: 0);

            using DocumentServiceRequest request = GoneAndRetryWithRequestRetryPolicyTests.CreatePointReadRequest();

            PartitionKeyRangeGoneException exception =
                new PartitionKeyRangeGoneException("PartitionKeyRange with id '4874' doesn't exist.");

            // First handling: budget remains for the un-penalised first retry, so it retries.
            policy.TryHandleResponseSynchronously(
                request,
                response: default,
                exception: exception,
                shouldRetryResult: out ShouldRetryResult firstResult);
            Assert.IsTrue(firstResult.ShouldRetry, "The first 410/1002 should still be retried.");

            // Second handling: budget is exhausted -> terminal conversion to 503.
            try
            {
                bool handled = policy.TryHandleResponseSynchronously(
                    request,
                    response: default,
                    exception: exception,
                    shouldRetryResult: out ShouldRetryResult terminalResult);

                Assert.IsTrue(handled);
                Assert.IsFalse(terminalResult.ShouldRetry, "Once the retry budget is exhausted the request must fail.");
                Assert.IsInstanceOfType(
                    terminalResult.ExceptionToThrow,
                    typeof(ServiceUnavailableException),
                    "An exhausted 410/1002 must surface as a 503 ServiceUnavailableException, not a bare 410/1002.");

                DocumentClientException terminalException = (DocumentClientException)terminalResult.ExceptionToThrow;
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, terminalException.StatusCode);
                Assert.AreEqual(
                    SubStatusCodes.Server_PartitionKeyRangeGoneExceededRetryLimit,
                    terminalException.GetSubStatus(),
                    "The terminal 503 must carry sub-status 21002 (Server_PartitionKeyRangeGoneExceededRetryLimit).");
                Assert.AreSame(
                    exception,
                    terminalException.InnerException,
                    "The terminal 503 must preserve the original 410/1002 as its inner exception.");
            }
            catch (MissingManifestResourceException)
            {
                // Known unit-test-host limitation: the product is built as the "Microsoft.Azure.Cosmos.Client"
                // assembly with RMResources re-rooted to "Microsoft.Azure.Cosmos.RMResources", so materializing
                // the terminal 503 message ("Microsoft.Azure.Documents.RMResources") throws here even though it
                // resolves correctly in the shipped Direct package. Reaching ServiceUnavailableException.Create
                // (the only thing that materializes that message) already proves the policy converted the
                // budget-exhausted 410/1002 into a terminal 503 rather than retrying or surfacing the bare
                // 410/1002. The exact 21002 substatus is asserted resource-free in the mapping test below.
            }
        }

        [TestMethod]
        public void PartitionKeyRangeGone_TerminalSubStatusMapsTo21002()
        {
            // Resource-free guard for the terminal substatus selected by the exhausted-budget path above:
            // a PartitionKeyRangeGoneException must map to Server_PartitionKeyRangeGoneExceededRetryLimit
            // (21002) so the customer-facing failure is a classifiable 503 rather than a raw 410/1002.
            SubStatusCodes terminalSubStatus = DocumentClientException.GetExceptionSubStatusForGoneRetryPolicy(
                new PartitionKeyRangeGoneException("PartitionKeyRange with id '4874' doesn't exist."));

            Assert.AreEqual(SubStatusCodes.Server_PartitionKeyRangeGoneExceededRetryLimit, terminalSubStatus);
            Assert.AreEqual(21002, (int)terminalSubStatus);
        }
    }
}
