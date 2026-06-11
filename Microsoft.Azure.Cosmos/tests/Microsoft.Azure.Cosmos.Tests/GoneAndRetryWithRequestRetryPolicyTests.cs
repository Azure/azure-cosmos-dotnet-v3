//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="GoneAndRetryWithRequestRetryPolicy{TResponse}"/> focused on the
    /// 410/1002 PartitionKeyRangeGone handling added for GitHub issue #5924, where a
    /// PartitionKeyRangeGoneException thrown during address resolution previously bubbled up to
    /// point-read callers without refreshing the (now-stale) collection routing map.
    /// </summary>
    [TestClass]
    public class GoneAndRetryWithRequestRetryPolicyTests
    {
        private const string DocumentResourceFullName =
            "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/";

        [TestMethod]
        public void PartitionKeyRangeGone_ForcesCollectionRoutingMapRefreshAndRetries()
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> policy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(disableRetryWithPolicy: false);

            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                DocumentResourceFullName,
                AuthorizationTokenType.PrimaryMasterKey);

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
    }
}
