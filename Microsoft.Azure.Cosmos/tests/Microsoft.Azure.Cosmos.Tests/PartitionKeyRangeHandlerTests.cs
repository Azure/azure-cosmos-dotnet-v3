//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Internal.RntbdConstants;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;

    [TestClass]
    public class PartitionKeyRangeHandlerTests
    {
        private const string Endpoint = "https://test";
        private const string Key = "test";
        private const string CollectionId = "test";
        private const string PartitionRangeKeyId = "138";
        const string Continuation = "continuation";

        [TestMethod]
        public async Task VerifySendUsesOringianlContinuationOnNonSuccessfulResponse()
        {
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = this.GetPartitionRoutingHelperMock();
            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(MockDocumentClient.CreateMockCosmosClient(), partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(async (request, cancellationToken) => {
                CosmosResponseMessage errorResponse = await TestHandler.ReturnStatusCode(HttpStatusCode.Gone);
                errorResponse.Headers.Remove(HttpConstants.HttpHeaders.Continuation); //Clobber original continuation
                return errorResponse;
            });
            partitionKeyRangeHandler.InnerHandler = testHandler;

            //Pass valid collections path because it is required by DocumentServiceRequest's constructor. This can't be mocked because ToDocumentServiceRequest() is an extension method
            CosmosRequestMessage initialRequest = new CosmosRequestMessage(HttpMethod.Get, new Uri($"{Paths.DatabasesPathSegment}/test/{Paths.CollectionsPathSegment}/test", UriKind.Relative));
            initialRequest.OperationType = OperationType.ReadFeed;
            initialRequest.Headers.Add(HttpConstants.HttpHeaders.Continuation, Continuation);
            CosmosResponseMessage response = await partitionKeyRangeHandler.SendAsync(initialRequest, CancellationToken.None);

            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(System.Net.HttpStatusCode.Gone, response.StatusCode);
            //Check if original continuation was restored
            Assert.AreEqual(Continuation, response.Headers.GetValues(HttpConstants.HttpHeaders.Continuation).First());
            Assert.AreEqual(Continuation, initialRequest.Headers.GetValues(HttpConstants.HttpHeaders.Continuation).First());
        }

        [TestMethod]
        public async Task VerifySendUsesOringianlContinuationOnDocumentClientExceptionAfterRetry()
        {
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = this.GetPartitionRoutingHelperMock();

            //throw a DocumentClientException
            partitionRoutingHelperMock.Setup(m => m.TryAddPartitionKeyRangeToContinuationTokenAsync(
                It.IsAny<INameValueCollection>(),
                It.IsAny<List<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.Is<string>(x => x == CollectionId),
                It.IsAny<ResolvedRangeInfo>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).ThrowsAsync(new DocumentClientException("error", HttpStatusCode.ServiceUnavailable, SubStatusCodes.Unknown));

            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(MockDocumentClient.CreateMockCosmosClient(), partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(async (request, cancellationToken) => {
                CosmosResponseMessage successResponse = await TestHandler.ReturnSuccess();
                successResponse.Headers.Remove(HttpConstants.HttpHeaders.Continuation); //Clobber original continuation
                return successResponse;
            });
            partitionKeyRangeHandler.InnerHandler = testHandler;

            //Pass valid collections path because it is required by DocumentServiceRequest's constructor. This can't be mocked because ToDocumentServiceRequest() is an extension method
            CosmosRequestMessage initialRequest = new CosmosRequestMessage(HttpMethod.Get, new Uri($"{Paths.DatabasesPathSegment}/test/{Paths.CollectionsPathSegment}/test", UriKind.Relative));
            initialRequest.OperationType = OperationType.ReadFeed;
            initialRequest.Headers.Add(HttpConstants.HttpHeaders.Continuation, Continuation);
            CosmosResponseMessage response = await partitionKeyRangeHandler.SendAsync(initialRequest, CancellationToken.None);

            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
            //Check if original continuation was restored
            Assert.AreEqual(Continuation, response.Headers.GetValues(HttpConstants.HttpHeaders.Continuation).First());
            Assert.AreEqual(Continuation, initialRequest.Headers.GetValues(HttpConstants.HttpHeaders.Continuation).First());
        }

        [TestMethod]
        public void CompositeContinuationTokenIsNotPassedToBackend()
        {
            Range<string> expectedRange = new Range<string>("A", "B", true, false);
            string expectedToken = "someToken";
            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken { Range = expectedRange, Token = expectedToken };
            string continuation = JsonConvert.SerializeObject(compositeContinuationToken);
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            StringKeyValueCollection headers = new StringKeyValueCollection();
            headers.Add(HttpConstants.HttpHeaders.Continuation, continuation);
            Range<string> range = partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(headers, out List<CompositeContinuationToken> compositeContinuationTokens);
            Assert.IsTrue(expectedRange.Equals(range));
            Assert.AreEqual(expectedToken, headers.Get(HttpConstants.HttpHeaders.Continuation)); //not a composite token
        }

        [TestMethod]
        public async Task GetTargetRangeFromContinuationTokenWhenEmpty()
        {
            List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

            //Empty
            Range<string> range = new Range<string>("", "", true, false);
            List<CompositeContinuationToken> suppliedTokens = new List<CompositeContinuationToken>
                {
                    new CompositeContinuationToken{ Range = range }
                };

            IReadOnlyList<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B" },
                new PartitionKeyRange { Id = "1", MinInclusive = "B", MaxExclusive = "C" }
            }.AsReadOnly();
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();
            

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                providedRanges, 
                routingMapProvider.Object, 
                CollectionId, 
                range, 
                suppliedTokens, 
                RntdbEnumerationDirection.Reverse);
            Assert.AreEqual(overlappingRanges.Last().Id, resolvedRangeInfo.ResolvedRange.Id);
            CollectionAssert.AreEqual(suppliedTokens, resolvedRangeInfo.ContinuationTokens);
            routingMapProvider.Verify();

            //Forward
            routingMapProvider.Setup(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == range.Min)
            )).Returns(Task.FromResult(overlappingRanges.First())).Verifiable();
            resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                RntdbEnumerationDirection.Forward);
            Assert.AreEqual(overlappingRanges.First().Id, resolvedRangeInfo.ResolvedRange.Id);
            CollectionAssert.AreEqual(suppliedTokens, resolvedRangeInfo.ContinuationTokens);
            routingMapProvider.Verify();
        }

        [TestMethod]
        public async Task GetTargetRangeFromContinuationTokenWhenNotEmpty()
        {
            List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

            //Not empty
            Range<string> range = new Range<string>("A", "B", true, false);
            List<CompositeContinuationToken> suppliedTokens = new List<CompositeContinuationToken>
                {
                    new CompositeContinuationToken{ Range = range }
                };

            IReadOnlyList<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B" },
                new PartitionKeyRange { Id = "1", MinInclusive = "B", MaxExclusive = "C" }
            }.AsReadOnly();
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == range.Min)
            )).Returns(Task.FromResult(overlappingRanges.First())).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                RntdbEnumerationDirection.Reverse);
            Assert.AreEqual(overlappingRanges.First().Id, resolvedRangeInfo.ResolvedRange.Id);
            CollectionAssert.AreEqual(suppliedTokens, resolvedRangeInfo.ContinuationTokens);
            routingMapProvider.Verify();
        }

        [TestMethod]
        public async Task GetTargetRangeFromContinuationTokenOnSplit()
        {
            const string Token = "token";

            List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

            Range<string> rangeFromContinuationToken = new Range<string>("A", "C", true, false);
            List<CompositeContinuationToken> suppliedTokens = new List<CompositeContinuationToken>
                {
                    new CompositeContinuationToken{ Token = Token, Range = rangeFromContinuationToken }
                };

            IReadOnlyList<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "2", MinInclusive = "A", MaxExclusive = "B" },
                new PartitionKeyRange { Id = "3", MinInclusive = "B", MaxExclusive = "C" },
                new PartitionKeyRange { Id = "1", MinInclusive = "C", MaxExclusive = "D" }
            }.AsReadOnly();
            IReadOnlyList<PartitionKeyRange> replacedRanges = overlappingRanges.Take(2).ToList().AsReadOnly();
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == rangeFromContinuationToken.Min)
            )).Returns(Task.FromResult(overlappingRanges.First())).Verifiable();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == rangeFromContinuationToken.Min && x.Max == rangeFromContinuationToken.Max),
                It.Is<bool>(x => x == true)
            )).Returns(Task.FromResult(replacedRanges)).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                rangeFromContinuationToken,
                suppliedTokens,
                RntdbEnumerationDirection.Reverse);

            routingMapProvider.Verify();
            Assert.IsTrue(replacedRanges.Last().Equals(resolvedRangeInfo.ResolvedRange));
            List<PartitionKeyRange> reversedReplacedRanges = new List<PartitionKeyRange>(replacedRanges);
            reversedReplacedRanges.Reverse();
            Assert.AreEqual(replacedRanges.Count, resolvedRangeInfo.ContinuationTokens.Count);
            Assert.AreEqual(resolvedRangeInfo.ContinuationTokens[0].Token, Token);

            for(int i = 0; i < resolvedRangeInfo.ContinuationTokens.Count; i++)
            {
                Assert.IsTrue(reversedReplacedRanges[i].ToRange().Equals(resolvedRangeInfo.ContinuationTokens[i].Range));
            }

            //Forward
            partitionRoutingHelper = new PartitionRoutingHelper();
            resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                rangeFromContinuationToken,
                suppliedTokens,
                RntdbEnumerationDirection.Forward);

            routingMapProvider.Verify();
            Assert.IsTrue(replacedRanges.First().Equals(resolvedRangeInfo.ResolvedRange));
            Assert.AreEqual(replacedRanges.Count, resolvedRangeInfo.ContinuationTokens.Count);
            Assert.AreEqual(resolvedRangeInfo.ContinuationTokens[0].Token, Token);

            for (int i = 0; i < resolvedRangeInfo.ContinuationTokens.Count; i++)
            {
                Assert.IsTrue(replacedRanges[i].ToRange().Equals(resolvedRangeInfo.ContinuationTokens[i].Range));
            }
        }

        [TestMethod]
        public async Task AddPartitionKeyRangeToContinuationTokenOnNullBackendContinuation()
        {
            List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        "A",
                        "D",
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };
            ResolvedRangeInfo currentPartitionKeyRange = new ResolvedRangeInfo(new PartitionKeyRange { Id = "1", MinInclusive = "B", MaxExclusive = "C" }, null);

            IReadOnlyList<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B"},
                currentPartitionKeyRange.ResolvedRange,
                new PartitionKeyRange { Id = "3", MinInclusive = "C", MaxExclusive = "D" }
            }.AsReadOnly();
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == providedRanges.Single().Min && x.Max == providedRanges.Single().Max),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            StringKeyValueCollection headers = new StringKeyValueCollection();
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                RntdbEnumerationDirection.Reverse
            );
            Assert.IsTrue(result);
            routingMapProvider.Verify();
            string expectedContinuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = null,
                Range = overlappingRanges.First().ToRange(),
            });
            Assert.AreEqual(expectedContinuationToken, headers.Get(HttpConstants.HttpHeaders.Continuation));

            //Forward
            routingMapProvider.Setup(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == currentPartitionKeyRange.ResolvedRange.MaxExclusive)
            )).Returns(Task.FromResult(overlappingRanges.Last())).Verifiable();
            headers = new StringKeyValueCollection();
            result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                 headers,
                 providedRanges,
                 routingMapProvider.Object,
                 CollectionId,
                 currentPartitionKeyRange,
                 RntdbEnumerationDirection.Forward
             );
            Assert.IsTrue(result);
            routingMapProvider.Verify();
            expectedContinuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = null,
                Range = overlappingRanges.Last().ToRange(),
            });
            Assert.AreEqual(expectedContinuationToken, headers.Get(HttpConstants.HttpHeaders.Continuation));
        }

        [TestMethod]
        public async Task AddPartitionKeyRangeToContinuationTokenOnNotNullBackendContinuation()
        {
            ResolvedRangeInfo currentPartitionKeyRange = new ResolvedRangeInfo(new PartitionKeyRange { Id = "1", MinInclusive = "B", MaxExclusive = "C" }, null);
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult<IReadOnlyList<PartitionKeyRange>>(null)).Verifiable();

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            StringKeyValueCollection headers = new StringKeyValueCollection();
            headers.Add(HttpConstants.HttpHeaders.Continuation, "something");
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                null,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                RntdbEnumerationDirection.Reverse
            );
            Assert.IsTrue(true);
            routingMapProvider.Verify(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<bool>()
            ), Times.Never);
        }

        [TestMethod]
        public async Task AddPartitionKeyRangeToContinuationTokenOnSplit()
        {
            const string BackendToken = "backendToken";
            StringKeyValueCollection headers = new StringKeyValueCollection();
            List<CompositeContinuationToken> compositeContinuationTokensFromSplit = new List<CompositeContinuationToken>
            {
                new CompositeContinuationToken{ Token = "someToken", Range = new Range<string>("A", "B", true, false) },
                new CompositeContinuationToken{ Token = "anotherToken", Range = new Range<string>("B", "C", true, false) }
            };

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();

            //With backend header
            headers.Add(HttpConstants.HttpHeaders.Continuation, BackendToken);
            ResolvedRangeInfo resolvedRangeInfo = new ResolvedRangeInfo(new PartitionKeyRange(), new List<CompositeContinuationToken>(compositeContinuationTokensFromSplit));
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                null,
                null,
                null,
                resolvedRangeInfo,
                RntdbEnumerationDirection.Reverse);
            List<CompositeContinuationToken> compositeContinuationTokens = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(headers.Get(HttpConstants.HttpHeaders.Continuation));
            Assert.IsTrue(result);
            Assert.AreEqual(compositeContinuationTokensFromSplit.Count, compositeContinuationTokens.Count);
            Assert.AreEqual(BackendToken, compositeContinuationTokens.First().Token);
            Assert.AreNotEqual(BackendToken, compositeContinuationTokens.Last().Token);

            //Without backend header
            headers.Remove(HttpConstants.HttpHeaders.Continuation);
            resolvedRangeInfo = new ResolvedRangeInfo(new PartitionKeyRange(), new List<CompositeContinuationToken>(compositeContinuationTokensFromSplit));
            result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                null,
                null,
                null,
                resolvedRangeInfo,
                RntdbEnumerationDirection.Reverse);
            compositeContinuationTokens = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(headers.Get(HttpConstants.HttpHeaders.Continuation));
            Assert.IsTrue(result);
            Assert.IsTrue(compositeContinuationTokens.Count == compositeContinuationTokensFromSplit.Count - 1);
            Assert.AreEqual(compositeContinuationTokensFromSplit.Last().Token, compositeContinuationTokens.First().Token);
        }

        [TestMethod]
        public async Task AddPartitionKeyRangeToContinuationTokenOnBoundry()
        {
            List<Range<string>> providedRanges = new List<Range<string>> {
                    new Range<string>(
                        "A",
                        "D",
                        isMinInclusive: true,
                        isMaxInclusive: false)
                };

            //Reverse
            ResolvedRangeInfo currentPartitionKeyRange = new ResolvedRangeInfo(new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B" }, null);
            IReadOnlyList<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B"},
            }.AsReadOnly();
            Mock<IRoutingMapProvider> routingMapProvider = new Mock<IRoutingMapProvider>();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == providedRanges.Single().Min && x.Max == providedRanges.Single().Max),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            StringKeyValueCollection headers = new StringKeyValueCollection();
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                RntdbEnumerationDirection.Reverse
            );

            Assert.IsTrue(result);
            routingMapProvider.Verify();
            string expectedContinuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = null,
                Range = overlappingRanges.First().ToRange(),
            });
            Assert.IsNull(headers.Get(HttpConstants.HttpHeaders.Continuation));

            //Forward
            currentPartitionKeyRange = new ResolvedRangeInfo(new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "D" }, null);
            overlappingRanges = new List<PartitionKeyRange> {
                new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "D"},
            }.AsReadOnly();
            routingMapProvider.Setup(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == currentPartitionKeyRange.ResolvedRange.MaxExclusive)
            )).Returns(Task.FromResult(overlappingRanges.Last()));
            headers = new StringKeyValueCollection();

            result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                 headers,
                 providedRanges,
                 routingMapProvider.Object,
                 CollectionId,
                 currentPartitionKeyRange,
                 RntdbEnumerationDirection.Forward
             );

            Assert.IsTrue(result);
            routingMapProvider.Verify(m => m.TryGetRangeByEffectivePartitionKey(
                It.IsAny<string>(),
                It.Is<string>(x => x == currentPartitionKeyRange.ResolvedRange.MaxExclusive)
            ), Times.Never);
            expectedContinuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = null,
                Range = overlappingRanges.Last().ToRange(),
            });
            Assert.IsNull(headers.Get(HttpConstants.HttpHeaders.Continuation));
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneRetryPolicyNextRetryPolicyDoesNotReturnNull()
        {
            Mock<IDocumentClientRetryPolicy> nextRetryPolicyMock = new Mock<IDocumentClientRetryPolicy>();
            nextRetryPolicyMock.Setup(m => m.ShouldRetryAsync(It.IsAny<CosmosResponseMessage>(), It.IsAny<CancellationToken>())).Returns<Task<ShouldRetryResult>>(null);
            Mock<PartitionKeyRangeGoneRetryPolicy> retryPolicyMock = new Mock<PartitionKeyRangeGoneRetryPolicy>(null, null, null, nextRetryPolicyMock.Object);

            ShouldRetryResult exceptionResult = await retryPolicyMock.Object.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);

            ShouldRetryResult messageResult = await retryPolicyMock.Object.ShouldRetryAsync(new CosmosResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(messageResult);
        }

        [TestMethod]
        public async Task InvalidPartitionRetryPolicyNextRetryPolicyDoesNotReturnNull()
        {
            Mock<CollectionCache> cache = new Mock<CollectionCache>();
            Mock<IDocumentClientRetryPolicy> nextRetryPolicyMock = new Mock<IDocumentClientRetryPolicy>();
            nextRetryPolicyMock.Setup(m => m.ShouldRetryAsync(It.IsAny<CosmosResponseMessage>(), It.IsAny<CancellationToken>())).Returns<Task<ShouldRetryResult>>(null);
            Mock<InvalidPartitionExceptionRetryPolicy> retryPolicyMock = new Mock<InvalidPartitionExceptionRetryPolicy>(cache.Object, nextRetryPolicyMock.Object);

            ShouldRetryResult exceptionResult = await retryPolicyMock.Object.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);

            retryPolicyMock = new Mock<InvalidPartitionExceptionRetryPolicy>(cache.Object, null);
            ShouldRetryResult messageResult = await retryPolicyMock.Object.ShouldRetryAsync(new CosmosResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(messageResult);
        }

        private Mock<PartitionRoutingHelper> GetPartitionRoutingHelperMock()
        {
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = new Mock<PartitionRoutingHelper>();
            partitionRoutingHelperMock.Setup(
                m => m.ExtractPartitionKeyRangeFromContinuationToken(It.IsAny<INameValueCollection>(), out It.Ref<List<CompositeContinuationToken>>.IsAny
            )).Returns(new Range<string>("A", "B", true, false));
            partitionRoutingHelperMock.Setup(m => m.TryGetTargetRangeFromContinuationTokenRange(
                It.IsAny<IReadOnlyList<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<List<CompositeContinuationToken>>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(new ResolvedRangeInfo(new PartitionKeyRange { Id = PartitionRangeKeyId }, new List<CompositeContinuationToken>())));
            partitionRoutingHelperMock.Setup(m => m.TryAddPartitionKeyRangeToContinuationTokenAsync(
                It.IsAny<INameValueCollection>(),
                It.IsAny<List<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<ResolvedRangeInfo>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(true));
            return partitionRoutingHelperMock;
        }
    }
}