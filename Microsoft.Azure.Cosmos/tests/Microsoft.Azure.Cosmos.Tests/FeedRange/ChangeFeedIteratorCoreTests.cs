//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using ChangeFeedPage = Microsoft.Azure.Cosmos.ChangeFeed.Pagination.ChangeFeedPage;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ChangeFeedIteratorCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Container()
        {
            new ChangeFeedIteratorCore(
                documentContainer: null,
                changeFeedMode: ChangeFeedMode.Incremental,
                changeFeedRequestOptions: new ChangeFeedRequestOptions(),
                changeFeedStartFrom: ChangeFeedStartFrom.Beginning(),
                container: null,
                clientContext: this.MockClientContext());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Mode()
        {
            new ChangeFeedIteratorCore(
                documentContainer: Mock.Of<IDocumentContainer>(),
                changeFeedMode: null,
                changeFeedRequestOptions: new ChangeFeedRequestOptions(),
                changeFeedStartFrom: ChangeFeedStartFrom.Beginning(),
                container: null,
                clientContext: this.MockClientContext());
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(-1)]
        [DataRow(0)]
        public void ChangeFeedIteratorCore_ValidateOptions(int maxItemCount)
        {
            new ChangeFeedIteratorCore(
                Mock.Of<IDocumentContainer>(),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = maxItemCount
                },
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(),
                container: null);
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_HasMoreResultsDefault()
        {
            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                Mock.Of<IDocumentContainer>(),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(),
                container: null);
            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_ReadNextAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(),
                container: null);

            int count = 0;
            while (changeFeedIteratorCore.HasMoreResults)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                count += GetChanges(responseMessage.Content).Count;
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_UpdatesContinuation_On304()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 0);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.NotModified, responseMessage.StatusCode);
            string continuationToken = responseMessage.Headers.ContinuationToken;

            ResponseMessage responseMessage2 = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.NotModified, responseMessage.StatusCode);
            string continuationToken2 = responseMessage2.Headers.ContinuationToken;

            Assert.AreNotEqual(continuationToken, continuationToken2);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(inject429s: true, injectEmptyPages: true));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            int count = 0;
            int numIterations = 500;
            while (numIterations-- > 0)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (!(responseMessage.IsSuccessStatusCode || responseMessage.StatusCode == HttpStatusCode.NotModified))
                {
                    if (responseMessage.Headers.ContinuationToken != null)
                    {
                        Assert.Fail();
                    }
                }
                else
                {
                    if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                    {
                        count += GetChanges(responseMessage.Content).Count;
                    }
                }

                if (count > numItems)
                {
                    Assert.Fail();
                }
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_HandlesSplitsThroughPipeline()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            int seed = new Random().Next();
            Random random = new Random(seed);

            int count = 0;
            while (changeFeedIteratorCore.HasMoreResults)
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                count += GetChanges(responseMessage.Content).Count;

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                FeedRangeInternal randomRange = ranges[random.Next(ranges.Count)];
                await documentContainer.SplitAsync(randomRange, cancellationToken: default);
            }

            Assert.AreEqual(numItems, count, seed);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OnCosmosException_HasMoreResults()
        {
            CosmosException exception = CosmosExceptionFactory.CreateInternalServerErrorException("something's broken", new Headers());
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    returnFailure: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Now(),
                this.MockClientContext(), container: null);

            ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
            Assert.IsFalse(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OnRetriableCosmosException_HasMoreResults()
        {
            CosmosException exception = CosmosExceptionFactory.CreateThrottledException("retry", new Headers());
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    returnFailure: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.TooManyRequests, responseMessage.StatusCode);
            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OnNonCosmosExceptions_HasMoreResults()
        {
            Exception exception = new NotImplementedException();
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    returnFailure: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            try
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                Assert.Fail("Should have thrown");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(exception, ex);
                Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
            }
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OnTaskCanceledException_HasMoreResultsAndDiagnostics()
        {
            Exception exception = new TaskCanceledException();
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    throwException: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            try
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                Assert.Fail("Should have thrown");
            }
            catch (OperationCanceledException ex)
            {
                Assert.IsTrue(ex is CosmosOperationCanceledException);
                Assert.IsNotNull(((CosmosOperationCanceledException)ex).Diagnostics);
                Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
            }
        }

        /// <summary>
        /// If an unhandled exception occurs within the NetworkAttachedDocumentContainer, the exception is transmitted but it does not break the enumerators
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_OnUnhandledException_HasMoreResults()
        {
            Exception exception = new Exception("oh no");
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    throwException: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            try
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                Assert.Fail("Should have thrown");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(exception, ex);
                Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
            }

            // If read a second time, it should not throw any missing page errors related to enumerators
            try
            {
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                Assert.Fail("Should have thrown");
            }
            catch (Exception ex)
            {
                // TryCatch wraps any exception
                Assert.AreEqual(exception, ex);
                Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
            }
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_CancellationToken_FlowsThrough()
        {
            // Generate constant 429
            CosmosException exception = CosmosExceptionFactory.CreateThrottledException("retry", new Headers());
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems: 0,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    returnFailure: exception));

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions(),
                ChangeFeedStartFrom.Beginning(),
                this.MockClientContext(), container: null);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            try
            {
                // First request triggers initialization, we don't cancel it
                ResponseMessage responseMessage = await changeFeedIteratorCore.ReadNextAsync();
                Assert.AreEqual(HttpStatusCode.TooManyRequests, responseMessage.StatusCode);

                // Should be initialized, let's see if cancellation flows through
                await changeFeedIteratorCore.ReadNextAsync(cancellationTokenSource.Token);
                Assert.Fail("Should have thrown");
            }
            catch (OperationCanceledException)
            {
            }

            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public void TokenOnlyOptionsDefaultAndClone()
        {
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions();
            Assert.IsFalse(options.EnableContinuationTokenOnly);
            options.EnableContinuationTokenOnly = true;
            options.PageSizeHint = 0;
            ChangeFeedRequestOptions clone = options.Clone();
            Assert.IsTrue(clone.EnableContinuationTokenOnly);
            Assert.AreEqual(0, clone.PageSizeHint);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.PageSizeHint = -1);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow(1, false)]
        [DataRow(17, false)]
        [DataRow(null, true)]
        [DataRow(1, true)]
        [DataRow(17, true)]
        public void TokenOnlyRequiresExplicitZeroPageSize(int? pageSize, bool fullFidelity)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("db", "coll");
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions
            {
                PageSizeHint = pageSize,
                EnableContinuationTokenOnly = true,
            };
            ChangeFeedMode mode = fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion;
            ArgumentOutOfRangeException streamError = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                container.GetChangeFeedStreamIterator(ChangeFeedStartFrom.Now(), mode, options));
            ArgumentOutOfRangeException typedError = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                container.GetChangeFeedIterator<object>(ChangeFeedStartFrom.Now(), mode, options));
            Assert.AreEqual(nameof(ChangeFeedRequestOptions.PageSizeHint), streamError.ParamName);
            Assert.AreEqual(nameof(ChangeFeedRequestOptions.PageSizeHint), typedError.ParamName);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TokenOnlyZeroPageSizeAllowsEitherInitializerOrder(bool pageSizeFirst)
        {
            ChangeFeedRequestOptions options = pageSizeFirst
                ? new ChangeFeedRequestOptions { PageSizeHint = 0, EnableContinuationTokenOnly = true }
                : new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 };
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("db", "coll");
            using FeedIterator streamIterator = container.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Now(), ChangeFeedMode.LatestVersion, options);
            using FeedIterator<object> typedIterator = container.GetChangeFeedIterator<object>(
                ChangeFeedStartFrom.Now(), ChangeFeedMode.AllVersionsAndDeletes, options);
            Assert.IsTrue(streamIterator.HasMoreResults);
            Assert.IsTrue(typedIterator.HasMoreResults);
            Assert.AreEqual(0, options.PageSizeHint);
        }

        [TestMethod]
        public void ZeroPageSizeIsRejectedWithoutTokenOnlyMode()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            ContainerInternal container = (ContainerInternal)client.GetContainer("db", "coll");
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions { PageSizeHint = 0 };
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                container.GetChangeFeedStreamIterator(ChangeFeedStartFrom.Now(), ChangeFeedMode.LatestVersion, options));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                container.GetChangeFeedIterator<object>(ChangeFeedStartFrom.Now(), ChangeFeedMode.AllVersionsAndDeletes, options));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                container.GetChangeFeedAsyncEnumerable(ChangeFeedCrossFeedRangeState.CreateFromNow(), ChangeFeedMode.LatestVersion, options));
        }

        [TestMethod]
        public void TokenOnlyRejectsUnsupportedStartsAtPublicFactories()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            ContainerInternal container = (ContainerInternal)client.GetContainer("db", "coll");
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 };
            foreach (ChangeFeedStartFrom start in new[]
            {
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedStartFrom.Time(DateTime.UtcNow),
                ChangeFeedStartFrom.ContinuationToken("token"),
            })
            {
                Assert.ThrowsException<ArgumentException>(() =>
                    container.GetChangeFeedStreamIterator(start, ChangeFeedMode.LatestVersion, options));
                Assert.ThrowsException<ArgumentException>(() =>
                    container.GetChangeFeedIterator<object>(start, ChangeFeedMode.AllVersionsAndDeletes, options));
            }

            Assert.ThrowsException<ArgumentException>(() => container.GetChangeFeedAsyncEnumerable(
                ChangeFeedCrossFeedRangeState.CreateFromNow(), ChangeFeedMode.LatestVersion, options));
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TokenOnlySuppressesDocumentsAndAllowsExplicitRepeatedReads(bool fullFidelity, bool typed)
        {
            Mock<IDocumentContainer> documentContainer = CreateTokenOnlyDocumentContainer();
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions
            {
                EnableContinuationTokenOnly = true,
                PageSizeHint = 0,
            };
            using ChangeFeedIteratorCore core = new ChangeFeedIteratorCore(
                documentContainer.Object,
                fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion,
                options, ChangeFeedStartFrom.Now(), this.MockClientContext(), null);
            options.EnableContinuationTokenOnly = false;
            options.PageSizeHint = 17;
            using FeedIterator<object> typedIterator = new FeedIteratorCore<object>(
                core, response => ReadFeedResponse<object>.CreateResponse<object>(response, MockCosmosUtil.Serializer));
            Assert.IsTrue(core.HasMoreResults);
            string continuation = null;
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                if (typed)
                {
                    FeedResponse<object> response = await typedIterator.ReadNextAsync();
                    Assert.AreEqual(0, response.Count);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.AreEqual(3d, response.RequestCharge);
                    Assert.AreEqual("0", response.Headers[HttpConstants.HttpHeaders.ItemCount]);
                    continuation = response.ContinuationToken;
                    Assert.IsFalse(typedIterator.HasMoreResults);
                }
                else
                {
                    using ResponseMessage response = await core.ReadNextAsync();
                    Assert.AreEqual(0, GetChanges(response.Content).Count);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.AreEqual(3d, response.Headers.RequestCharge);
                    Assert.AreEqual("0", response.Headers[HttpConstants.HttpHeaders.ItemCount]);
                    continuation = response.Headers.ContinuationToken;
                }

                Assert.IsFalse(core.HasMoreResults);
                Assert.IsNotNull(continuation);
                Assert.AreEqual(17, options.PageSizeHint);
                VersionedAndRidCheckedCompositeToken versioned = VersionedAndRidCheckedCompositeToken
                    .MonadicCreateFromCosmosElement(CosmosElement.Parse(continuation)).Result;
                ChangeFeedCrossFeedRangeState state = ChangeFeedCrossFeedRangeState.Monadic
                    .CreateFromCosmosElement(versioned.ContinuationToken).Result;
                Assert.AreEqual(3, state.FeedRangeStates.Length);
                foreach (FeedRangeState<ChangeFeedState> range in state.FeedRangeStates.ToArray())
                {
                    Assert.IsInstanceOfType<ChangeFeedStateContinuation>(range.State);
                }

                documentContainer.Verify(container => container.MonadicChangeFeedAsync(
                    It.IsAny<FeedRangeState<ChangeFeedState>>(), It.IsAny<ChangeFeedExecutionOptions>(),
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Exactly(1 + (3 * cycle)));
                documentContainer.Verify(container => container.GetResourceIdentifierAsync(
                    It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Exactly(cycle));
            }

            using ChangeFeedIteratorCore resumed = new ChangeFeedIteratorCore(
                documentContainer.Object,
                fullFidelity ? ChangeFeedMode.AllVersionsAndDeletes : ChangeFeedMode.LatestVersion,
                null, ChangeFeedStartFrom.ContinuationToken(continuation), this.MockClientContext(), null);
            using ResponseMessage resumedPage = await resumed.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.OK, resumedPage.StatusCode);
            Assert.AreEqual(1, GetChanges(resumedPage.Content).Count);
            Assert.IsTrue(resumed.HasMoreResults);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TokenOnlyDoesNotCompleteInterruptedCycleAndSurfacesBufferedError(bool alreadyCompleted)
        {
            int calls = 0;
            Mock<IDocumentContainer> documentContainer = CreateTokenOnlyDocumentContainer(
                () => ++calls == (alreadyCompleted ? 5 : 2)
                    ? new CosmosException("throttled", HttpStatusCode.TooManyRequests, 0, "activity", 0) : null);
            using ChangeFeedIteratorCore core = new ChangeFeedIteratorCore(
                documentContainer.Object, ChangeFeedMode.LatestVersion,
                new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 },
                ChangeFeedStartFrom.Now(), this.MockClientContext(), null);
            if (alreadyCompleted)
            {
                using ResponseMessage first = await core.ReadNextAsync();
                Assert.IsFalse(core.HasMoreResults);
            }

            using ResponseMessage partial = await core.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.OK, partial.StatusCode);
            Assert.AreEqual(1d, partial.Headers.RequestCharge);
            Assert.AreEqual(!alreadyCompleted, core.HasMoreResults);
            using ResponseMessage error = await core.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.TooManyRequests, error.StatusCode);
            Assert.IsNull(error.Headers.ContinuationToken);
            Assert.AreEqual(!alreadyCompleted, core.HasMoreResults);
            using ResponseMessage complete = await core.ReadNextAsync();
            Assert.AreEqual(HttpStatusCode.OK, complete.StatusCode);
            Assert.IsFalse(core.HasMoreResults);
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.BadRequest)]
        public async Task TokenOnlyPreservesErrorLifecycle(HttpStatusCode status)
        {
            Mock<IDocumentContainer> documentContainer = CreateTokenOnlyDocumentContainer(
                () => new CosmosException("failure", status, 0, "activity", 0));
            using ChangeFeedIteratorCore core = new ChangeFeedIteratorCore(
                documentContainer.Object, ChangeFeedMode.LatestVersion,
                new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 },
                ChangeFeedStartFrom.Now(), this.MockClientContext(), null);
            using ResponseMessage error = await core.ReadNextAsync();
            Assert.AreEqual(status, error.StatusCode);
            Assert.IsNull(error.Headers.ContinuationToken);
            Assert.AreEqual(status == HttpStatusCode.TooManyRequests, core.HasMoreResults);
        }

        [TestMethod]
        public async Task TokenOnlyCancellationDoesNotCompleteIterator()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Mock<IDocumentContainer> documentContainer = CreateTokenOnlyDocumentContainer(() =>
            {
                cancellation.Cancel();
                return null;
            });
            using ChangeFeedIteratorCore core = new ChangeFeedIteratorCore(
                documentContainer.Object, ChangeFeedMode.LatestVersion,
                new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 },
                ChangeFeedStartFrom.Now(), this.MockClientContext(), null);
            await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(
                () => core.ReadNextAsync(cancellation.Token));
            Assert.IsTrue(core.HasMoreResults);
        }

        [TestMethod]
        public async Task TokenOnlyCannotBeEnabledAfterIteratorCreation()
        {
            Mock<IDocumentContainer> documentContainer = CreateTokenOnlyDocumentContainer();
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions();
            using ChangeFeedIteratorCore core = new ChangeFeedIteratorCore(
                documentContainer.Object, ChangeFeedMode.LatestVersion, options,
                ChangeFeedStartFrom.Beginning(), this.MockClientContext(), null);
            options.EnableContinuationTokenOnly = true;
            using ResponseMessage response = await core.ReadNextAsync();
            Assert.IsTrue(core.HasMoreResults);
            Assert.AreEqual(1, GetChanges(response.Content).Count);
            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.IsAny<FeedRangeState<ChangeFeedState>>(),
                It.Is<ChangeFeedExecutionOptions>(execution => !execution.EnableContinuationTokenOnly && execution.PageSizeLimit == null),
                It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        private static Mock<IDocumentContainer> CreateTokenOnlyDocumentContainer(Func<Exception> getFailure = null)
        {
            Mock<IDocumentContainer> container = new Mock<IDocumentContainer>();
            List<FeedRangeEpk> ranges = new List<FeedRangeEpk>
            {
                new FeedRangeEpk(new Documents.Routing.Range<string>("", "40", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("40", "80", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("80", "FF", true, false)),
            };
            container.Setup(c => c.GetChildRangeAsync(
                It.IsAny<FeedRangeInternal>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);
            container.Setup(c => c.GetResourceIdentifierAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("rid");
            int sequence = 0;
            container.Setup(c => c.MonadicChangeFeedAsync(
                It.IsAny<FeedRangeState<ChangeFeedState>>(), It.IsAny<ChangeFeedExecutionOptions>(),
                It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FeedRangeState<ChangeFeedState> state, ChangeFeedExecutionOptions execution, ITrace trace, CancellationToken token) =>
                {
                    token.ThrowIfCancellationRequested();
                    if (state.FeedRange.Equals(FeedRangeEpk.FullRange))
                    {
                        return TryCatch<ChangeFeedPage>.FromException(new CosmosException(
                            "split", HttpStatusCode.Gone, (int)SubStatusCodes.PartitionKeyRangeGone, string.Empty, 0));
                    }

                    Exception failure = getFailure?.Invoke();
                    if (failure != null)
                    {
                        return TryCatch<ChangeFeedPage>.FromException(failure);
                    }

                    return TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedSuccessPage(
                        new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Documents\":[{\"id\":\"visible-in-normal-mode\"}],\"_count\":1}")),
                        1, 1, "activity", null, ChangeFeedState.Continuation(CosmosString.Create($"etag-{++sequence}"))));
                });
            return container;
        }

        private static CosmosArray GetChanges(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                CosmosObject element = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!element.TryGetValue("Documents", out CosmosArray value))
                {
                    Assert.Fail();
                }

                return value;
            }
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                    {
                        "/pk"
                    },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }

        private CosmosClientContext MockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.OperationHelperAsync<ResponseMessage>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<ResponseMessage>>>(),
                It.IsAny<(string OperationName, Func<ResponseMessage, OpenTelemetryAttributes> GetAttributes)?>(),
                It.IsAny<ResourceType?>(),
                It.IsAny<TraceComponent>(),
                It.IsAny<TraceLevel>()))
               .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<ResponseMessage>>, (string OperationName, Func<ResponseMessage, OpenTelemetryAttributes> GetAttributes)?, ResourceType?, TraceComponent, TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) => func(NoOpTrace.Singleton));

            return mockContext.Object;
        }
    }
}