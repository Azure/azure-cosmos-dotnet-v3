//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DistributedTransactionCommitterTests
    {
        // Known-valid collection resource ID that passes ResourceId.Parse.
        private const string TestCollectionResourceId = "ccZ1ANCszwk=";

        [TestMethod]
        public async Task CommitTransaction_SucceedsOnFirstAttempt()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnRetriableResponse_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnRequestTimeoutStatus_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(CreateTimeoutResponseMessage());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnCosmosExceptionTimeout_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromException<ResponseMessage>(CreateCosmosTimeoutException());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriableResponse_RetriesUntilCancelled()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () =>
                    {
                        callCount++;
                        if (callCount == 3)
                        {
                            cts.Cancel();
                        }

                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    });

                // Non-zero delay so Task.Delay honours the already-cancelled token.
                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(),
                    mockContext.Object,
                    TimeSpan.FromMilliseconds(1));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.CommitTransactionAsync(cts.Token));

                // Retries continue until the cancellation token fires; no fixed upper bound.
                Assert.AreEqual(3, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_CosmosTimeoutException_CancelledDuringRequest_PropagatesCosmosException()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () =>
                    {
                        callCount++;
                        cts.Cancel(); // Cancel while the request is in-flight.
                        return Task.FromException<ResponseMessage>(CreateCosmosTimeoutException());
                    });

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(),
                    mockContext.Object,
                    TimeSpan.FromMilliseconds(1));

                // When the token is cancelled during the request the retry guard
                // "when (!cancellationToken.IsCancellationRequested && ...)" is false,
                // so the CosmosException propagates immediately rather than being retried.
                CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => committer.CommitTransactionAsync(cts.Token));

                Assert.AreEqual(HttpStatusCode.RequestTimeout, ex.StatusCode);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_DoesNotRetryOnNonRetriableFailure()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateNonRetriableErrorResponseMessage());
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_DoesNotRetryOnNonRetriableServerError()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(
                        new ResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
                        });
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsFalse(response.IsRetriable);
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RespectsCancellationToken_PreCancelled()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () => throw new InvalidOperationException("Should not be called on a pre-cancelled token."));

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.CommitTransactionAsync(cts.Token));

                this.VerifyProcessResourceOperationCallCount(mockContext, Times.Never());
            }
        }

        [TestMethod]
        public async Task CommitTransaction_CancelledDuringRetryDelay_ThrowsOperationCanceledException()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                int callCount = 0;
                Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
                this.SetupProcessResourceOperation(
                    mockContext,
                    () =>
                    {
                        callCount++;
                        cts.Cancel(); // Cancel after the first call so the retry delay throws.
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    });

                // Non-zero delay so the retry path enters Task.Delay
                // the token is already cancelled synchronously in the callback, so it throws immediately.
                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    CreateTestOperations(),
                    mockContext.Object,
                    TimeSpan.FromMilliseconds(500));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => committer.CommitTransactionAsync(cts.Token));

                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_MixedExceptionAndRetriableResponse_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount switch
                    {
                        1 => Task.FromException<ResponseMessage>(CreateCosmosTimeoutException()),
                        2 => Task.FromResult(CreateRetriableErrorResponseMessage()),
                        _ => Task.FromResult(CreateSuccessResponseMessage(operationCount: 1)),
                    };
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(3, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_MultipleRetriesThenSuccessOnLastAttempt()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    if (callCount <= 3)
                    {
                        return Task.FromResult(CreateRetriableErrorResponseMessage());
                    }

                    return Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                // 3 retriable failures + 1 success = 4 total calls.
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(4, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_NonCosmosException_PropagatesImmediately()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromException<ResponseMessage>(new IOException("Network error"));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            IOException ex = await Assert.ThrowsExceptionAsync<IOException>(
                () => committer.CommitTransactionAsync(CancellationToken.None));

            Assert.AreEqual("Network error", ex.Message);
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public async Task CommitTransaction_NonTimeoutCosmosException_PropagatesImmediately()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    CosmosException notFound = new CosmosException(
                        "Not found",
                        HttpStatusCode.NotFound,
                        subStatusCode: 0,
                        activityId: null,
                        requestCharge: 0);
                    return Task.FromException<ResponseMessage>(notFound);
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => committer.CommitTransactionAsync(CancellationToken.None));

            Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public async Task CommitTransaction_SameIdempotencyTokenSentOnEveryRetryAttempt()
        {
            int callCount = 0;
            List<string> capturedTokens = new List<string>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperationWithEnricherCapture(
                mockContext,
                enricher =>
                {
                    RequestMessage request = new RequestMessage
                    {
                        ResourceType = ResourceType.DistributedTransactionBatch,
                        OperationType = OperationType.CommitDistributedTransaction,
                    };
                    enricher(request);
                    capturedTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                },
                () =>
                {
                    callCount++;
                    return callCount < 3
                        ? Task.FromResult(CreateRetriableErrorResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(3, callCount);
            }

            Assert.AreEqual(3, capturedTokens.Count);
            CollectionAssert.AllItemsAreNotNull(capturedTokens, "IdempotencyToken header must be set on every request.");
            Assert.AreEqual(1, new HashSet<string>(capturedTokens).Count, "The same idempotency token must be used on every retry attempt.");
        }

        // ---- Status-code-based retry tests (spec compliance) ----

        [TestMethod]
        public async Task CommitTransaction_RetriesOnRaceConflict449_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateRaceConflictResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnRaceConflict449_HonorsRetryAfterHeader()
        {
            int callCount = 0;
            TimeSpan retryAfterDelay = TimeSpan.FromMilliseconds(250);
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateRaceConflictResponseMessage(retryAfter: retryAfterDelay))
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) => { capturedDelays.Add(delay); return Task.CompletedTask; };

            // Large base delay — any retry that ignores Retry-After would produce a much larger value.
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                retryBaseDelay: TimeSpan.FromSeconds(60),
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }

            Assert.AreEqual(1, capturedDelays.Count, "Exactly one retry delay should have been applied.");
            Assert.AreEqual(retryAfterDelay, capturedDelays[0],
                "Retry delay must equal the Retry-After header value, not the exponential base.");
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnLedgerThrottled429_ThenSucceeds()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateLedgerThrottledResponseMessage())
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetriesOnLedgerThrottled429_HonorsRetryAfterHeader()
        {
            int callCount = 0;
            TimeSpan retryAfterDelay = TimeSpan.FromMilliseconds(250);
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateLedgerThrottledResponseMessage(retryAfter: retryAfterDelay))
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) => { capturedDelays.Add(delay); return Task.CompletedTask; };

            // Large base delay — any retry that ignores Retry-After would produce a much larger value.
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                retryBaseDelay: TimeSpan.FromSeconds(60),
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }

            Assert.AreEqual(1, capturedDelays.Count, "Exactly one retry delay should have been applied.");
            Assert.AreEqual(retryAfterDelay, capturedDelays[0],
                "Retry delay must equal the Retry-After header value, not the exponential base.");
        }

        [TestMethod]
        public async Task CommitTransaction_DoesNotRetryOn449WithNonRaceConflictSubStatus()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    // 449 with sub-status 0 — not a DTx race conflict, must not be retried.
                    return Task.FromResult(CreateEmptyResponseMessage((HttpStatusCode)StatusCodes.RetryWith, subStatusCode: 0));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual((HttpStatusCode)StatusCodes.RetryWith, response.StatusCode);
                Assert.AreEqual(1, callCount, "449 with non-5352 sub-status must not be retried.");
            }
        }

        [TestMethod]
        public async Task CommitTransaction_RetryAfterZero_PassesThroughAsZero()
        {
            int callCount = 0;
            List<TimeSpan> capturedDelays = new List<TimeSpan>();
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateRaceConflictResponseMessage(retryAfter: TimeSpan.Zero))
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            Func<TimeSpan, CancellationToken, Task> captureDelay = (delay, _) => { capturedDelays.Add(delay); return Task.CompletedTask; };
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                CreateTestOperations(),
                mockContext.Object,
                retryBaseDelay: TimeSpan.Zero,
                delayProvider: captureDelay);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }

            Assert.AreEqual(1, capturedDelays.Count);
            Assert.AreEqual(TimeSpan.Zero, capturedDelays[0],
                "A Retry-After of zero must be passed through as-is — the server is explicitly requesting an immediate retry.");
        }

        [DataTestMethod]
        [DataRow(DistributedTransactionConstants.DtcLedgerFailure, DisplayName = "500/5411 LedgerFailure")]
        [DataRow(DistributedTransactionConstants.DtcAccountConfigFailure, DisplayName = "500/5412 AccountConfigFailure")]
        [DataRow(DistributedTransactionConstants.DtcDispatchFailure, DisplayName = "500/5413 DispatchFailure")]
        public async Task CommitTransaction_RetriesOnInfraFailure500_ThenSucceeds(int subStatusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(CreateEmptyResponseMessage(HttpStatusCode.InternalServerError, subStatusCode))
                        : Task.FromResult(CreateSuccessResponseMessage(operationCount: 1));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(2, callCount);
            }
        }

        [TestMethod]
        public async Task CommitTransaction_DoesNotRetryOn500WithNonDtcSubStatus()
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateEmptyResponseMessage(HttpStatusCode.InternalServerError, subStatusCode: 0));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                Assert.AreEqual(1, callCount, "Generic 500 with non-DTC sub-status must not be retried.");
            }
        }

        [DataTestMethod]
        [DataRow(5405, DisplayName = "400/5405 ParseFailure")]
        [DataRow(5406, DisplayName = "400/5406 FeatureDisabled")]
        [DataRow(5407, DisplayName = "400/5407 MaxOpsExceeded")]
        [DataRow(5408, DisplayName = "400/5408 MissingIdempotencyToken")]
        [DataRow(5409, DisplayName = "400/5409 InvalidAccountName")]
        [DataRow(5410, DisplayName = "400/5410 InvalidOperation")]
        public async Task CommitTransaction_DoesNotRetryOnValidationFailure400(int subStatusCode)
        {
            int callCount = 0;
            Mock<CosmosClientContext> mockContext = this.CreateMockClientContext();
            this.SetupProcessResourceOperation(
                mockContext,
                () =>
                {
                    callCount++;
                    return Task.FromResult(CreateEmptyResponseMessage(HttpStatusCode.BadRequest, subStatusCode));
                });

            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(CreateTestOperations(), mockContext.Object, TimeSpan.Zero);

            using (DistributedTransactionResponse response = await committer.CommitTransactionAsync(CancellationToken.None))
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.AreEqual(1, callCount, $"Validation failure 400/{subStatusCode} must not be retried.");
            }
        }

        #region Helpers

        private Mock<CosmosClientContext> CreateMockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();

            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);

            mockContext.Setup(x => x.GetCachedContainerPropertiesAsync(
                It.IsAny<string>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ContainerProperties.CreateWithResourceId(TestCollectionResourceId));

            return mockContext;
        }

        private void SetupProcessResourceOperation(
            Mock<CosmosClientContext> mockContext,
            Func<Task<ResponseMessage>> responseFactory)
        {
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(responseFactory);
        }

        private void SetupProcessResourceOperationWithEnricherCapture(
            Mock<CosmosClientContext> mockContext,
            Action<Action<RequestMessage>> enricherCallback,
            Func<Task<ResponseMessage>> responseFactory)
        {
            mockContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, Cosmos.PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, _, enricher, _, _) => enricherCallback(enricher))
                .Returns(responseFactory);
        }

        private void VerifyProcessResourceOperationCallCount(
            Mock<CosmosClientContext> mockContext,
            Times times)
        {
            mockContext.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerInternal>(),
                It.IsAny<Cosmos.PartitionKey?>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), times);
        }

        private static IReadOnlyList<DistributedTransactionOperation> CreateTestOperations(int count = 1)
        {
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>(count);
            for (int i = 0; i < count; i++)
            {
                operations.Add(new DistributedTransactionOperation(
                    OperationType.Create,
                    i,
                    "testDb",
                    "testContainer",
                    Cosmos.PartitionKey.Null));
            }

            return operations;
        }

        private static ResponseMessage CreateSuccessResponseMessage(int operationCount)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"operationResponses\":[");
            for (int i = 0; i < operationCount; i++)
            {
                if (i > 0)
                {
                    json.Append(",");
                }

                json.Append($"{{\"index\":{i},\"statuscode\":200,\"substatuscode\":0}}");
            }

            json.Append("]}");

            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString()))
            };
        }

        private static ResponseMessage CreateRetriableErrorResponseMessage()
        {
            string json = "{\"isRetriable\":true}";
            return new ResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        private static ResponseMessage CreateTimeoutResponseMessage()
        {
            return new ResponseMessage(HttpStatusCode.RequestTimeout)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
            };
        }

        private static ResponseMessage CreateNonRetriableErrorResponseMessage()
        {
            return new ResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
            };
        }

        private static CosmosException CreateCosmosTimeoutException()
        {
            return new CosmosException(
                "Request timed out",
                HttpStatusCode.RequestTimeout,
                subStatusCode: 0,
                activityId: null,
                requestCharge: 0);
        }

        /// <summary>Creates a 449/5352 empty-body response (coordinator race conflict).</summary>
        private static ResponseMessage CreateRaceConflictResponseMessage(TimeSpan? retryAfter = null)
        {
            ResponseMessage message = CreateEmptyResponseMessage((HttpStatusCode)StatusCodes.RetryWith, DistributedTransactionConstants.DtcCoordinatorRaceConflict);
            if (retryAfter.HasValue)
            {
                message.Headers.RetryAfterLiteral = ((long)retryAfter.Value.TotalMilliseconds).ToString();
            }

            return message;
        }

        /// <summary>Creates a 429/3200 empty-body response (ledger throttled).</summary>
        private static ResponseMessage CreateLedgerThrottledResponseMessage(TimeSpan? retryAfter = null)
        {
            ResponseMessage message = CreateEmptyResponseMessage((HttpStatusCode)StatusCodes.TooManyRequests, DistributedTransactionConstants.DtcLedgerThrottled);
            if (retryAfter.HasValue)
            {
                message.Headers.RetryAfterLiteral = ((long)retryAfter.Value.TotalMilliseconds).ToString();
            }

            return message;
        }

        /// <summary>Creates an empty-body response with the given status and sub-status codes.</summary>
        private static ResponseMessage CreateEmptyResponseMessage(HttpStatusCode statusCode, int subStatusCode)
        {
            ResponseMessage message = new ResponseMessage(statusCode);
            message.Headers.SubStatusCodeLiteral = subStatusCode.ToString();
            return message;
        }

        #endregion
    }
}
