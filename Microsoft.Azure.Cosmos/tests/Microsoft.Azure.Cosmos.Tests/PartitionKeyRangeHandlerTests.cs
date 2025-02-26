//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;
    using static Microsoft.Azure.Documents.RntbdConstants;

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
            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(MockCosmosUtil.CreateMockCosmosClient(), partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(async (request, cancellationToken) =>
            {
                ResponseMessage errorResponse = await TestHandler.ReturnStatusCode(HttpStatusCode.Gone);
                errorResponse.Headers.Remove(HttpConstants.HttpHeaders.Continuation); //Clobber original continuation
                return errorResponse;
            });
            partitionKeyRangeHandler.InnerHandler = testHandler;

            //Pass valid collections path because it is required by DocumentServiceRequest's constructor. This can't be mocked because ToDocumentServiceRequest() is an extension method
            RequestMessage initialRequest = new RequestMessage(HttpMethod.Get, new Uri($"{Paths.DatabasesPathSegment}/test/{Paths.CollectionsPathSegment}/test", UriKind.Relative))
            {
                OperationType = OperationType.ReadFeed
            };
            initialRequest.Headers.Add(HttpConstants.HttpHeaders.Continuation, Continuation);
            ResponseMessage response = await partitionKeyRangeHandler.SendAsync(initialRequest, CancellationToken.None);

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
                It.IsAny<ITrace>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).ThrowsAsync(new DocumentClientException("error", HttpStatusCode.ServiceUnavailable, SubStatusCodes.Unknown));

            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(MockCosmosUtil.CreateMockCosmosClient(), partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(async (request, cancellationToken) =>
            {
                ResponseMessage successResponse = await TestHandler.ReturnSuccess();
                successResponse.Headers.Remove(HttpConstants.HttpHeaders.Continuation); //Clobber original continuation
                return successResponse;
            });
            partitionKeyRangeHandler.InnerHandler = testHandler;

            //Pass valid collections path because it is required by DocumentServiceRequest's constructor. This can't be mocked because ToDocumentServiceRequest() is an extension method
            RequestMessage initialRequest = new RequestMessage(HttpMethod.Get, new Uri($"{Paths.DatabasesPathSegment}/test/{Paths.CollectionsPathSegment}/test", UriKind.Relative))
            {
                OperationType = OperationType.ReadFeed
            };
            initialRequest.Headers.Add(HttpConstants.HttpHeaders.Continuation, Continuation);
            ResponseMessage response = await partitionKeyRangeHandler.SendAsync(initialRequest, CancellationToken.None);

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
            RequestNameValueCollection headers = new()
            {
                { HttpConstants.HttpHeaders.Continuation, continuation }
            };
            Range<string> range = partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(headers, out _);
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
                It.IsAny<ITrace>(),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();


            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                NoOpTrace.Singleton,
                RntdbEnumerationDirection.Reverse);
            Assert.AreEqual(overlappingRanges.Last().Id, resolvedRangeInfo.ResolvedRange.Id);
            CollectionAssert.AreEqual(suppliedTokens, resolvedRangeInfo.ContinuationTokens);
            routingMapProvider.Verify();

            //Forward
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == range.Min),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)overlappingRanges.Take(1).ToList())).Verifiable();
            resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                NoOpTrace.Singleton,
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
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == range.Min),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)overlappingRanges.Take(1).ToList())).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                NoOpTrace.Singleton,
                RntdbEnumerationDirection.Reverse);
            Assert.AreEqual(overlappingRanges.First().Id, resolvedRangeInfo.ResolvedRange.Id);
            CollectionAssert.AreEqual(suppliedTokens, resolvedRangeInfo.ContinuationTokens);
            routingMapProvider.Verify();
        }

        [TestMethod]
        public async Task GetTargetRangeFromContinuationTokenNonExistentContainer()
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
            routingMapProvider
                .SetupSequence(m => m.TryGetOverlappingRangesAsync(
                    It.IsAny<string>(),
                    It.Is<Range<string>>(x => x.Min == range.Min),
                    It.IsAny<ITrace>(),
                    It.IsAny<bool>()))
                .Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)overlappingRanges.Skip(1).ToList()))
                .Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)null));

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                range,
                suppliedTokens,
                NoOpTrace.Singleton,
                RntdbEnumerationDirection.Reverse);

            Assert.IsNotNull(resolvedRangeInfo);
            Assert.IsNull(resolvedRangeInfo.ResolvedRange);
            Assert.IsNull(resolvedRangeInfo.ContinuationTokens);
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
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == rangeFromContinuationToken.Min),
                It.IsAny<ITrace>(),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)overlappingRanges.Take(1).ToList())).Verifiable();
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(x => x.Min == rangeFromContinuationToken.Min && x.Max == rangeFromContinuationToken.Max),
                It.IsAny<ITrace>(),
                It.Is<bool>(x => x == true)
            )).Returns(Task.FromResult(replacedRanges)).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            ResolvedRangeInfo resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                rangeFromContinuationToken,
                suppliedTokens,
                NoOpTrace.Singleton,
                RntdbEnumerationDirection.Reverse);

            routingMapProvider.Verify();
            Assert.IsTrue(replacedRanges.Last().Equals(resolvedRangeInfo.ResolvedRange));
            List<PartitionKeyRange> reversedReplacedRanges = new List<PartitionKeyRange>(replacedRanges);
            reversedReplacedRanges.Reverse();
            Assert.AreEqual(replacedRanges.Count, resolvedRangeInfo.ContinuationTokens.Count);
            Assert.AreEqual(resolvedRangeInfo.ContinuationTokens[0].Token, Token);

            for (int i = 0; i < resolvedRangeInfo.ContinuationTokens.Count; i++)
            {
                Assert.IsTrue(reversedReplacedRanges[i].ToRange().Equals(resolvedRangeInfo.ContinuationTokens[i].Range));
            }

            //Forward
            partitionRoutingHelper = new PartitionRoutingHelper();
            resolvedRangeInfo = await partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                rangeFromContinuationToken,
                suppliedTokens,
                NoOpTrace.Singleton,
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
                It.IsAny<ITrace>(),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();

            //Reverse
            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            RequestNameValueCollection headers = new();
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                NoOpTrace.Singleton,
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
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)overlappingRanges.Skip(2).ToList())).Verifiable();
            headers = new RequestNameValueCollection();
            result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                 headers,
                 providedRanges,
                 routingMapProvider.Object,
                 CollectionId,
                 currentPartitionKeyRange,
                 NoOpTrace.Singleton,
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
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult<IReadOnlyList<PartitionKeyRange>>(null)).Verifiable();

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            RequestNameValueCollection headers = new()
            {
                { HttpConstants.HttpHeaders.Continuation, "something" }
            };
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                null,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                NoOpTrace.Singleton,
                RntdbEnumerationDirection.Reverse
            );
            Assert.IsTrue(true);
            routingMapProvider.Verify(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            ), Times.Never);
        }

        [TestMethod]
        public async Task AddPartitionKeyRangeToContinuationTokenOnSplit()
        {
            const string BackendToken = "backendToken";
            RequestNameValueCollection headers = new();
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
                NoOpTrace.Singleton,
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
                NoOpTrace.Singleton,
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
                It.IsAny<ITrace>(),
                It.Is<bool>(x => x == false)
            )).Returns(Task.FromResult(overlappingRanges)).Verifiable();

            PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
            RequestNameValueCollection headers = new();
            bool result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                headers,
                providedRanges,
                routingMapProvider.Object,
                CollectionId,
                currentPartitionKeyRange,
                NoOpTrace.Singleton,
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
            routingMapProvider.Setup(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            )).Returns(Task.FromResult(overlappingRanges));
            headers = new RequestNameValueCollection();

            result = await partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                 headers,
                 providedRanges,
                 routingMapProvider.Object,
                 CollectionId,
                 currentPartitionKeyRange,
                 NoOpTrace.Singleton,
                 RntdbEnumerationDirection.Forward
             );

            Assert.IsTrue(result);
            routingMapProvider.Verify(m => m.TryGetOverlappingRangesAsync(
                It.IsAny<string>(),
                It.Is<Range<string>>(e => e.IsMaxInclusive),
                It.IsAny<ITrace>(),
                It.IsAny<bool>()
            ), Times.Never);
            expectedContinuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = null,
                Range = overlappingRanges.Last().ToRange(),
            });
            Assert.IsNull(headers.Get(HttpConstants.HttpHeaders.Continuation));
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneRetryPolicyWithNextRetryPolicy()
        {
            Mock<IDocumentClientRetryPolicy> nextRetryPolicyMock = new Mock<IDocumentClientRetryPolicy>();
            nextRetryPolicyMock
                .Setup(m => m.ShouldRetryAsync(It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ShouldRetryResult>(ShouldRetryResult.RetryAfter(TimeSpan.FromDays(1))))
                .Verifiable();

            nextRetryPolicyMock
                .Setup(m => m.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ShouldRetryResult>(ShouldRetryResult.RetryAfter(TimeSpan.FromDays(1))))
                .Verifiable();

            PartitionKeyRangeGoneRetryPolicy retryPolicy = new PartitionKeyRangeGoneRetryPolicy(null, null, null, nextRetryPolicyMock.Object);

            ShouldRetryResult exceptionResult = await retryPolicy.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsTrue(exceptionResult.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromDays(1), exceptionResult.BackoffTime);

            ShouldRetryResult messageResult = await retryPolicy.ShouldRetryAsync(new ResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsTrue(exceptionResult.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromDays(1), exceptionResult.BackoffTime);
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneRetryPolicyWithoutNextRetryPolicy()
        {
            PartitionKeyRangeGoneRetryPolicy retryPolicy = new PartitionKeyRangeGoneRetryPolicy(null, null, null, null);

            ShouldRetryResult exceptionResult = await retryPolicy.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsFalse(exceptionResult.ShouldRetry);
            _ = await retryPolicy.ShouldRetryAsync(new ResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsFalse(exceptionResult.ShouldRetry);
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneTracePlumbingTest()
        {
            ITrace trace = Trace.GetRootTrace("TestTrace");
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("pk");

            const string collectionRid = "DvZRAOvLgDM=";
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(collectionRid);
            containerProperties.Id = "TestContainer";
            containerProperties.PartitionKey = partitionKeyDefinition;

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            using GlobalEndpointManager endpointManager = new(mockDocumentClient.Object, new ConnectionPolicy());

            Mock<Common.CollectionCache> collectionCache = new Mock<Common.CollectionCache>(MockBehavior.Strict);
            collectionCache.Setup(c => c.ResolveCollectionAsync(It.IsAny<DocumentServiceRequest>(), default, trace))
                .ReturnsAsync(containerProperties);

            CollectionRoutingMap collectionRoutingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(new List<Tuple<PartitionKeyRange, ServiceIdentity>>(), collectionRid);
            Mock<PartitionKeyRangeCache> partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                MockBehavior.Strict,
                new Mock<ICosmosAuthorizationTokenProvider>().Object,
                new Mock<IStoreModel>().Object,
                collectionCache.Object,
                endpointManager);
            partitionKeyRangeCache.Setup(c => c.TryLookupAsync(collectionRid, null, It.IsAny<DocumentServiceRequest>(), trace))
                .ReturnsAsync(collectionRoutingMap);

            string collectionLink = "dbs/DvZRAA==/colls/DvZRAOvLgDM=/";
            PartitionKeyRangeGoneRetryPolicy policy = new PartitionKeyRangeGoneRetryPolicy(
                collectionCache.Object,
                partitionKeyRangeCache.Object,
                collectionLink,
                null,
                trace);

            ShouldRetryResult shouldRetryResult = await policy.ShouldRetryAsync(
                new DocumentClientException("partition gone", HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone),
                default);

            Assert.IsNotNull(shouldRetryResult);
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
            Assert.AreEqual(0, shouldRetryResult.BackoffTime.Ticks);
        }

        [TestMethod]
        public async Task InvalidPartitionRetryPolicyWithNextRetryPolicy()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<IDocumentClientRetryPolicy> nextRetryPolicyMock = new Mock<IDocumentClientRetryPolicy>();

            nextRetryPolicyMock
                .Setup(m => m.ShouldRetryAsync(It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ShouldRetryResult>(ShouldRetryResult.RetryAfter(TimeSpan.FromDays(1))))
                .Verifiable();

            nextRetryPolicyMock
                .Setup(m => m.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ShouldRetryResult>(ShouldRetryResult.RetryAfter(TimeSpan.FromDays(1))))
                .Verifiable();

            InvalidPartitionExceptionRetryPolicy retryPolicyMock = new InvalidPartitionExceptionRetryPolicy(nextRetryPolicyMock.Object);

            ShouldRetryResult exceptionResult = await retryPolicyMock.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsTrue(exceptionResult.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromDays(1), exceptionResult.BackoffTime);

            ShouldRetryResult messageResult = await retryPolicyMock.ShouldRetryAsync(new ResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsTrue(exceptionResult.ShouldRetry);
            Assert.AreEqual(TimeSpan.FromDays(1), exceptionResult.BackoffTime);
        }

        [TestMethod]
        public async Task InvalidPartitionRetryPolicyWithoutNextRetryPolicy()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            InvalidPartitionExceptionRetryPolicy retryPolicyMock = new InvalidPartitionExceptionRetryPolicy(null);

            ShouldRetryResult exceptionResult = await retryPolicyMock.ShouldRetryAsync(new Exception("", null), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsFalse(exceptionResult.ShouldRetry);

            ShouldRetryResult messageResult = await retryPolicyMock.ShouldRetryAsync(new ResponseMessage(), CancellationToken.None);
            Assert.IsNotNull(exceptionResult);
            Assert.IsFalse(exceptionResult.ShouldRetry);
        }

        private Mock<PartitionRoutingHelper> GetPartitionRoutingHelperMock()
        {
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = new Mock<PartitionRoutingHelper>();
            partitionRoutingHelperMock.Setup(
                m => m.ExtractPartitionKeyRangeFromContinuationToken(It.IsAny<INameValueCollection>(), out It.Ref<List<CompositeContinuationToken>>.IsAny
            )).Returns(new Range<string>("A", "B", true, false));
            partitionRoutingHelperMock.Setup(m => m.TryGetTargetRangeFromContinuationTokenRangeAsync(
                It.IsAny<IReadOnlyList<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<List<CompositeContinuationToken>>(),
                It.IsAny<ITrace>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(new ResolvedRangeInfo(new PartitionKeyRange { Id = PartitionRangeKeyId }, new List<CompositeContinuationToken>())));
            partitionRoutingHelperMock.Setup(m => m.TryAddPartitionKeyRangeToContinuationTokenAsync(
                It.IsAny<INameValueCollection>(),
                It.IsAny<List<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<ResolvedRangeInfo>(),
                It.IsAny<ITrace>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(true));
            return partitionRoutingHelperMock;
        }
    }
}