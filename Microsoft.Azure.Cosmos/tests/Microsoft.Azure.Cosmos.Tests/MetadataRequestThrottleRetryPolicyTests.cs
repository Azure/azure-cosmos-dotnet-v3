//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.MetadataRequestThrottleRetryPolicy;

    /// <summary>
    /// Unit tests for <see cref="MetadataRequestThrottleRetryPolicy"/>.
    /// </summary>
    [TestClass]
    public class MetadataRequestThrottleRetryPolicyTests
    {
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Test when a response message.")]
        [DataRow(false, DisplayName = "Test when an exception was thrown.")]
        public async Task ShouldRetryAsync_WithValidAndInvalidSubStatusCodes_ShouldIncrementLocationIndexOrSkip(
            bool useResponseMessage)
        {
            // Arrange.
            ShouldRetryResult retryResult;
            string collectionRid = "test-collection";
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");
            Uri routedServiceEndpoint = new("https://default-endpoint-region2.net/");

            Documents.Collections.INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();

            headers.Set(HttpConstants.HttpHeaders.PageSize, "10");
            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.ReadFeed,
                    collectionRid,
                    Documents.ResourceType.PartitionKeyRange,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = new();
            mockedGlobalEndpointManager
                .SetupSequence(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(primaryServiceEndpoint)
                .Returns(routedServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            Assert.AreEqual(primaryServiceEndpoint, request.RequestContext.LocationEndpointToRoute);

            // Act.
            if (useResponseMessage)
            {
                Headers responseHeaders = new()
                {
                    SubStatusCode = SubStatusCodes.TransportGenerated503
                };

                ResponseMessage responseMessage = new(
                    statusCode: HttpStatusCode.ServiceUnavailable,
                    requestMessage: null,
                    headers: responseHeaders,
                    cosmosException: null,
                    trace: NoOpTrace.Singleton);

                retryResult = await policy.ShouldRetryAsync(responseMessage, default);
            }
            else
            {
                CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                                                message: "Service Unavailable at the moment.",
                                                headers: new Headers()
                                                {
                                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                    SubStatusCode = SubStatusCodes.TransportGenerated503
                                                },
                                                trace: NoOpTrace.Singleton,
                                                innerException: null);


                retryResult = await policy.ShouldRetryAsync(exception, default);
            }

            policy.OnBeforeSendRequest(request);

            // Assert.
            FieldInfo fieldInfo = policy
                .GetType()
                .GetField(
                    name: "retryContext",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            MetadataRetryContext retryContext = (MetadataRetryContext)fieldInfo
                .GetValue(
                    obj: policy);

            Assert.IsNotNull(retryResult);
            Assert.AreEqual(true, retryResult.ShouldRetry, "MetadataRequestThrottleRetryPolicy should return true since the sub status code indicates to retry the request in the next preferred read region.");
            Assert.AreEqual(1, retryContext.RetryLocationIndex, "Indicates that the retry location index was incremented.");
            Assert.AreEqual(routedServiceEndpoint, request.RequestContext.LocationEndpointToRoute);
        }

        // ---------------------------------------------------------------------
        // Stage 2 — coordination with MetadataHedgingContext
        // (docs/PPAF_Metadata_Hedging_ColdStart_Design.md §5.7.3 / §5.7.4)
        // ---------------------------------------------------------------------

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task ShouldRetryAsync_HedgeContextAttached_SkipsAttemptedEndpointAndAdvancesToFreshIndex()
        {
            // Arrange — 3 read endpoints (A, B, C). Hedge already attempted A + B; on a 503
            // the bounded probe loop must advance past indices 1 (B) and land on index 2 (C).
            Uri endpointA = new("https://default-endpoint-region1.net/");
            Uri endpointB = new("https://default-endpoint-region2.net/");
            Uri endpointC = new("https://default-endpoint-region3.net/");

            Mock<IGlobalEndpointManager> gem = new(MockBehavior.Strict);
            gem.Setup(g => g.PreferredLocationCount).Returns(3);
            gem.Setup(g => g.ReadEndpoints).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Uri>(new[] { endpointA, endpointB, endpointC }));

            // ResolveServiceEndpoint follows the index threaded through RouteToLocation.
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
               .Returns<DocumentServiceRequest>(req =>
               {
                   int idx = req.RequestContext.LocationIndexToRoute ?? 0;
                   return idx switch
                   {
                       0 => endpointA,
                       1 => endpointB,
                       _ => endpointC,
                   };
               });

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);

            MetadataHedgingContext hedgeContext = new()
            {
                IsColdStart = true,
            };
            hedgeContext.AttemptedEndpoints.TryAdd(endpointA.AbsoluteUri, 0);
            hedgeContext.AttemptedEndpoints.TryAdd(endpointB.AbsoluteUri, 0);

            MetadataRequestThrottleRetryPolicy policy = new(gem.Object, 0);
            policy.AttachHedgeContext(hedgeContext);
            policy.OnBeforeSendRequest(request);

            // Act — simulate a 503 response from the primary attempt.
            Headers responseHeaders = new() { SubStatusCode = SubStatusCodes.TransportGenerated503 };
            ResponseMessage responseMessage = new(
                statusCode: HttpStatusCode.ServiceUnavailable,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert — should retry, and the retry-context's RetryLocationIndex points to C (index 2), not B (index 1).
            MetadataRetryContext retryContext = ReadRetryContext(policy);
            Assert.IsTrue(retryResult.ShouldRetry, "Policy should retry because the probe loop must skip A+B and land on C.");
            Assert.AreEqual(2, retryContext.RetryLocationIndex, "Probe loop must advance past index 1 (B) since it is in AttemptedEndpoints.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task ShouldRetryAsync_HedgeContextAttached_AllPreferredRegionsAttempted_DoesNotRetry()
        {
            // Arrange — 2 read endpoints, both already attempted by the hedge.
            // The bounded probe loop should exhaust without finding a fresh region and return false.
            Uri endpointA = new("https://default-endpoint-region1.net/");
            Uri endpointB = new("https://default-endpoint-region2.net/");

            Mock<IGlobalEndpointManager> gem = new(MockBehavior.Strict);
            gem.Setup(g => g.PreferredLocationCount).Returns(2);
            gem.Setup(g => g.ReadEndpoints).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Uri>(new[] { endpointA, endpointB }));
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
               .Returns<DocumentServiceRequest>(req => (req.RequestContext.LocationIndexToRoute ?? 0) == 0 ? endpointA : endpointB);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);

            MetadataHedgingContext hedgeContext = new()
            {
                IsColdStart = true,
            };
            hedgeContext.AttemptedEndpoints.TryAdd(endpointA.AbsoluteUri, 0);
            hedgeContext.AttemptedEndpoints.TryAdd(endpointB.AbsoluteUri, 0);

            MetadataRequestThrottleRetryPolicy policy = new(gem.Object, 0);
            policy.AttachHedgeContext(hedgeContext);
            policy.OnBeforeSendRequest(request);

            // Act — simulate a 503.
            Headers responseHeaders = new() { SubStatusCode = SubStatusCodes.TransportGenerated503 };
            ResponseMessage responseMessage = new(
                statusCode: HttpStatusCode.ServiceUnavailable,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert — must not retry (no fresh region left); ResourceThrottleRetryPolicy returns ShouldRetry == false for non-throttle status codes.
            Assert.IsFalse(retryResult.ShouldRetry, "Policy must not retry when all preferred regions are in AttemptedEndpoints.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public async Task ShouldRetryAsync_NoHedgeContextAttached_BehavesAsLegacyMonotonicCounter()
        {
            // Arrange — verify the no-hedge case collapses to a single iteration matching the legacy behavior.
            Uri endpointA = new("https://default-endpoint-region1.net/");
            Uri endpointB = new("https://default-endpoint-region2.net/");

            Mock<IGlobalEndpointManager> gem = new(MockBehavior.Strict);
            gem.Setup(g => g.PreferredLocationCount).Returns(2);
            gem.Setup(g => g.ReadEndpoints).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Uri>(new[] { endpointA, endpointB }));
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(endpointA);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);

            MetadataRequestThrottleRetryPolicy policy = new(gem.Object, 0);
            // NOTE: AttachHedgeContext deliberately not called.
            policy.OnBeforeSendRequest(request);

            // Act.
            Headers responseHeaders = new() { SubStatusCode = SubStatusCodes.TransportGenerated503 };
            ResponseMessage responseMessage = new(
                statusCode: HttpStatusCode.ServiceUnavailable,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert — single advance to index 1; ResolveServiceEndpoint is called once per BackoffRetryUtility iteration.
            MetadataRetryContext retryContext = ReadRetryContext(policy);
            Assert.IsTrue(retryResult.ShouldRetry);
            Assert.AreEqual(1, retryContext.RetryLocationIndex);
            gem.Verify(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()), Times.Once, "Without a hedge context, the probe loop must not call ResolveServiceEndpoint.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        [DataRow((int)HttpStatusCode.ServiceUnavailable, (int)SubStatusCodes.TransportGenerated503, true)]
        [DataRow((int)HttpStatusCode.InternalServerError, (int)SubStatusCodes.Unknown, true)]
        [DataRow((int)HttpStatusCode.Gone, (int)SubStatusCodes.LeaseNotFound, true)]
        [DataRow((int)HttpStatusCode.Forbidden, (int)SubStatusCodes.DatabaseAccountNotFound, true)]
        [DataRow((int)HttpStatusCode.Gone, (int)SubStatusCodes.Unknown, false)]
        [DataRow((int)HttpStatusCode.Forbidden, (int)SubStatusCodes.Unknown, false)]
        [DataRow((int)HttpStatusCode.NotFound, (int)SubStatusCodes.Unknown, false)]
        public async Task ShouldRetryAsync_StatusCodeMatrix_MatchesRetryUtilityIsRegionalFailure(
            int statusCodeInt,
            int subStatusInt,
            bool expectRetry)
        {
            HttpStatusCode status = (HttpStatusCode)statusCodeInt;
            SubStatusCodes subStatus = (SubStatusCodes)subStatusInt;
            // Arrange — confirm the policy now routes regional-failure classification through
            // RetryUtility.IsRegionalFailure (single source of truth, §5.7.2) and preserves
            // the existing 4-case set.
            Uri endpointA = new("https://default-endpoint-region1.net/");
            Uri endpointB = new("https://default-endpoint-region2.net/");

            Mock<IGlobalEndpointManager> gem = new(MockBehavior.Loose);
            gem.Setup(g => g.PreferredLocationCount).Returns(1);
            gem.Setup(g => g.ReadEndpoints).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Uri>(new[] { endpointA, endpointB }));
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(endpointA);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey);

            MetadataRequestThrottleRetryPolicy policy = new(gem.Object, 0);
            policy.OnBeforeSendRequest(request);

            Headers responseHeaders = new() { SubStatusCode = subStatus };
            ResponseMessage responseMessage = new(
                statusCode: status,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: NoOpTrace.Singleton);

            // Act.
            ShouldRetryResult retryResult = await policy.ShouldRetryAsync(responseMessage, default);

            // Assert.
            Assert.AreEqual(expectRetry, retryResult.ShouldRetry,
                $"Status {status}/{subStatus} expected ShouldRetry={expectRetry}.");
        }

        [TestMethod]
        [Owner("kundadebdatta")]
        public void AttachHedgeContext_Null_IsSafeNoOp()
        {
            // Arrange.
            Mock<IGlobalEndpointManager> gem = new(MockBehavior.Loose);
            gem.Setup(g => g.PreferredLocationCount).Returns(1);
            gem.Setup(g => g.ReadEndpoints).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Uri>(new[] { new Uri("https://r1/") }));

            MetadataRequestThrottleRetryPolicy policy = new(gem.Object, 0);

            // Act + Assert — attaching, then detaching to null, must not throw.
            policy.AttachHedgeContext(new MetadataHedgingContext());
            policy.AttachHedgeContext(null);
        }

        private static MetadataRetryContext ReadRetryContext(MetadataRequestThrottleRetryPolicy policy)
        {
            FieldInfo fieldInfo = typeof(MetadataRequestThrottleRetryPolicy)
                .GetField("retryContext", BindingFlags.Instance | BindingFlags.NonPublic);
            return (MetadataRetryContext)fieldInfo.GetValue(policy);
        }
    }
}