// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class GoneAndRetryWithRequestRetryPolicyTests
    {
        private const int RetryWaitTimeMilliseconds = 2000;

        [TestMethod]
        [DataRow(3, 2, true)]
        [DataRow(3, 1, false)]
        [DataRow(null, 2, false)]
        public async Task ConnectivityDetectionUsesResolvedReplicaSetSize(
            int? targetReplicaSetSize,
            int failedReplicaCount,
            bool expectedConnectivityDetection)
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> retryPolicy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(
                    disableRetryWithPolicy: false,
                    waitTimeInSecondsOverride: 1,
                    detectConnectivityIssues: true);

            HashSet<TransportAddressUri> failedReplicas = new HashSet<TransportAddressUri>();
            for (int i = 0; i < failedReplicaCount; i++)
            {
                failedReplicas.Add(new TransportAddressUri(new Uri($"http://replica{i}.com")));
            }

            Mock<IClientSideRequestStatistics> statistics = new Mock<IClientSideRequestStatistics>();
            statistics.SetupGet(s => s.FailedReplicas).Returns(failedReplicas);
            statistics.SetupGet(s => s.RegionsContacted).Returns(new HashSet<(string, Uri)>());

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.ClientRequestStatistics = statistics.Object;
            request.RequestContext.ResolvedPartitionTargetReplicaSetSize = targetReplicaSetSize;

            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(retryPolicy.TryHandleResponseSynchronously(
                    request,
                    new StoreResponse(),
                    new GoneException("message", SubStatusCodes.Unknown),
                    out ShouldRetryResult initialResult));
                Assert.IsTrue(initialResult.ShouldRetry);
            }

            await Task.Delay(RetryWaitTimeMilliseconds);

            Assert.IsTrue(retryPolicy.TryHandleResponseSynchronously(
                request,
                new StoreResponse(),
                new GoneException("message", SubStatusCodes.Unknown),
                out ShouldRetryResult retryResult));
            Assert.IsFalse(retryResult.ShouldRetry);
            Assert.IsInstanceOfType(retryResult.ExceptionToThrow, typeof(ServiceUnavailableException));
            Assert.AreEqual(
                expectedConnectivityDetection,
                retryResult.ExceptionToThrow.Message.Contains("unable to establish connections"));
        }

        [TestMethod]
        [Owner("askagarw")]
        public async Task ServiceUnavailableExceptionHasSubStatus()
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> retryPolicy = new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(false, waitTimeInSecondsOverride: 1);
            await Task.Delay(2000); // wait out the entire waittime to throw exception the second time.
            DocumentServiceRequest dsr = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            GoneException goneException = new GoneException(new Exception(), SubStatusCodes.TransportGenerated410);
            retryPolicy.TryHandleResponseSynchronously(dsr, null, goneException, out ShouldRetryResult shouldRetryResult); // first try

            retryPolicy.TryHandleResponseSynchronously(dsr, null, goneException, out shouldRetryResult);
            Assert.AreEqual(shouldRetryResult.ShouldRetry, false);
            Assert.IsTrue(shouldRetryResult.ExceptionToThrow is ServiceUnavailableException);
            ServiceUnavailableException sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.TransportGenerated410);

            PartitionKeyRangeIsSplittingException pkRangeSplitEx = new PartitionKeyRangeIsSplittingException();
            retryPolicy.TryHandleResponseSynchronously(dsr, null, pkRangeSplitEx, out shouldRetryResult);
            sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.Server_CompletingSplitExceededRetryLimit);

            InvalidPartitionException invalidPartitionEx = new InvalidPartitionException();
            retryPolicy.TryHandleResponseSynchronously(dsr, null, invalidPartitionEx, out shouldRetryResult);
            sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.Server_NameCacheIsStaleExceededRetryLimit);

            PartitionIsMigratingException partitionMigratingEx = new PartitionIsMigratingException();
            retryPolicy.TryHandleResponseSynchronously(dsr, null, partitionMigratingEx, out shouldRetryResult);
            sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.Server_CompletingPartitionMigrationExceededRetryLimit);

            LeaseNotFoundException leaseNotFoundEx = new LeaseNotFoundException();
            retryPolicy.TryHandleResponseSynchronously(dsr, null, leaseNotFoundEx, out shouldRetryResult);
            sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.LeaseNotFound);

            ArchivalPartitionNotPresentException archivalPartitionNotPresentEx = new ArchivalPartitionNotPresentException();
            retryPolicy.TryHandleResponseSynchronously(dsr, null, archivalPartitionNotPresentEx, out shouldRetryResult);
            sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.ArchivalPartitionNotPresent);
        }

        [TestMethod]
        [Owner("askagarw")]
        public async Task PreviousExceptionThrownOnTimeout()
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> retryPolicy = new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(false, waitTimeInSecondsOverride: 1);
            await Task.Delay(2000); // wait out the entire waittime to throw exception the second time.
            DocumentServiceRequest dsr = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
            GoneException goneException = new GoneException(new Exception(), SubStatusCodes.TransportGenerated410);
            retryPolicy.TryHandleResponseSynchronously(dsr, null, goneException, out ShouldRetryResult shouldRetryResult); // first try

            // Timeout Gone Exception
            goneException = new GoneException(new Exception(), SubStatusCodes.TimeoutGenerated410);
            retryPolicy.TryHandleResponseSynchronously(dsr, null, goneException, out shouldRetryResult);
            ServiceUnavailableException sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
            Assert.AreEqual(sue.GetSubStatus(), SubStatusCodes.TransportGenerated410); // The first excception is thrown
        }

        [TestMethod]
        [Owner("aavasthy")]

        [DataRow(false, true)] // disableArchivalPartitionNotFoundRetry = false, Should retry
        [DataRow(true, false)] // disableArchivalPartitionNotFoundRetry = true, Should not retry
        public async Task ArchivalPartitionNotPresentRetryBehavior(bool disableArchivalPartitionNotFoundRetry, bool expectedRetry)
        {
            GoneAndRetryWithRequestRetryPolicy<StoreResponse> retryPolicy =
                new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(false, waitTimeInSecondsOverride: 1);

            DocumentServiceRequest dsr = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            dsr.DisableArchivalPartitionNotFoundRetry = disableArchivalPartitionNotFoundRetry;
            ArchivalPartitionNotPresentException archivalEx = new ArchivalPartitionNotPresentException();

            // First attempt
            retryPolicy.TryHandleResponseSynchronously(dsr, null, archivalEx, out ShouldRetryResult shouldRetryResult);
            Assert.AreEqual(shouldRetryResult.ShouldRetry, expectedRetry, $"Expected retry behavior mismatch for disableArchivalPartitionNotFoundRetry = {disableArchivalPartitionNotFoundRetry}");

            if (expectedRetry)
            {
                // Wait for the retry time to expire
                await Task.Delay(RetryWaitTimeMilliseconds);

                // Second attempt should fail
                retryPolicy.TryHandleResponseSynchronously(dsr, null, archivalEx, out shouldRetryResult);
                Assert.IsFalse(shouldRetryResult.ShouldRetry, "Should not retry after max retry time");
                Assert.IsInstanceOfType(shouldRetryResult.ExceptionToThrow, typeof(ServiceUnavailableException));
                ServiceUnavailableException sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
                Assert.AreEqual(SubStatusCodes.ArchivalPartitionNotPresent, sue.GetSubStatus());
            }
            else
            {
                Assert.IsInstanceOfType(shouldRetryResult.ExceptionToThrow, typeof(ServiceUnavailableException));
                ServiceUnavailableException sue = shouldRetryResult.ExceptionToThrow as ServiceUnavailableException;
                Assert.AreEqual(SubStatusCodes.ArchivalPartitionNotPresent, sue.GetSubStatus());
            }
        }
    }
}