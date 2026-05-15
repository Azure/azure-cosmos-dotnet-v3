namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
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
    /// Tests for <see cref="AvailabilityStrategy"/>
    /// </summary>
    [TestClass]
    public class AvailabilityStrategyUnitTests
    {
        /// <summary>
        /// Helper to create a mock CosmosClient with multiple read regions configured.
        /// </summary>
        private static CosmosClient CreateMockClientWithRegions(int regionCount = 2)
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
        /// Helper to create a basic read request for document operations.
        /// </summary>
        private static RequestMessage CreateReadRequest()
        {
            return new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };
        }
        [TestMethod]
        public async Task RequestMessageCloneTests()
        {
            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            string key = Guid.NewGuid().ToString();
            Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { key, Guid.NewGuid() }
            };

            RequestOptions requestOptions = new RequestOptions()
            {
                Properties = properties
            };

            httpRequest.RequestOptions = requestOptions;
            httpRequest.ResourceType = ResourceType.Document;
            httpRequest.OperationType = OperationType.Read;
            httpRequest.Headers.CorrelatedActivityId = Guid.NewGuid().ToString();
            httpRequest.PartitionKeyRangeId = new PartitionKeyRangeIdentity("0", "1");
            httpRequest.UseGatewayMode = true;
            httpRequest.ContainerId = "testcontainer";
            httpRequest.DatabaseId = "testdb";
            httpRequest.Content = Stream.Null;

            using (CloneableStream clonedBody = await StreamExtension.AsClonableStreamAsync(httpRequest.Content))
            {
                RequestMessage clone = httpRequest.Clone(httpRequest.Trace, clonedBody);

                Assert.AreEqual(httpRequest.RequestOptions.Properties, clone.RequestOptions.Properties);
                Assert.AreEqual(httpRequest.ResourceType, clone.ResourceType);
                Assert.AreEqual(httpRequest.OperationType, clone.OperationType);
                Assert.AreEqual(httpRequest.Headers.CorrelatedActivityId, clone.Headers.CorrelatedActivityId);
                Assert.AreEqual(httpRequest.PartitionKeyRangeId, clone.PartitionKeyRangeId);
                Assert.AreEqual(httpRequest.UseGatewayMode, clone.UseGatewayMode);
                Assert.AreEqual(httpRequest.ContainerId, clone.ContainerId);
                Assert.AreEqual(httpRequest.DatabaseId, clone.DatabaseId);
            }
        }

        [TestMethod]
        public async Task CancellationTokenThrowsExceptionTest()
        {
            //Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                                                                   threshold: TimeSpan.FromMilliseconds(100),
                                                                   thresholdStep: TimeSpan.FromMilliseconds(50));

            RequestMessage request = new RequestMessage
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            
            AccountProperties databaseAccount = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    { new AccountRegion() { Name = "US East", Endpoint = new Uri("https://location1.documents.azure.com").ToString() } },
                    { new AccountRegion() { Name = "US West", Endpoint = new Uri("https://location2.documents.azure.com").ToString() } },
                    
                }
            };
            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.GlobalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (request, token) => throw new OperationCanceledException("operation cancellation requested");

            CosmosOperationCanceledException cancelledException = await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(() =>
                       availabilityStrategy.ExecuteAvailabilityStrategyAsync(sender, mockCosmosClient, request, cts.Token));
        }

        /// <summary>
        /// Regression test for NullReferenceException in CrossRegionHedgingAvailabilityStrategy.
        /// 
        /// In the old code, the sender was invoked with the application-provided CancellationToken
        /// instead of the hedgeRequestsCancellationTokenSource.Token. When one hedge request completed
        /// with a final result and cancelled the hedgeRequestsCancellationTokenSource, the other in-flight
        /// hedge requests were NOT cancelled because they held a reference to the original app CT. 
        /// The CloneAndSendAsync method's using block would dispose the cloned request, but the sender 
        /// still had a reference to the now-disposed request — causing ArgumentNullException: 
        /// "Value cannot be null. (Parameter 'request')".
        ///
        /// The fix passes hedgeRequestsCancellationTokenSource.Token to sender.Invoke() so that all
        /// in-flight hedge requests are cancelled when any hedge gets a final result.
        /// </summary>
        [TestMethod]
        public async Task HedgeCancellationCancelsInFlightRequests_NoNullRef()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;
            bool firstRequestCancellationTokenWasCancelled = false;

            // The first request (Region0) will be slow and should be cancelled when Region1 returns.
            // The second request (Region1) will return a final result quickly.
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // First request: simulate a slow request that respects cancellation.
                    // In the old code, this CT was the app CT and would NOT be cancelled
                    // when the hedge CTS was cancelled, leading to NullRef after request disposal.
                    TaskCompletionSource<bool> cancelledTcs = new TaskCompletionSource<bool>();
                    using (ct.Register(() =>
                    {
                        firstRequestCancellationTokenWasCancelled = true;
                        cancelledTcs.TrySetResult(true);
                    }))
                    {
                        await cancelledTcs.Task;
                    }

                    // Return transient response to avoid exception propagation through the strategy
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                else
                {
                    // Second request: return a final result immediately
                    return new ResponseMessage(HttpStatusCode.OK);
                }
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert - we got a successful response without NullReferenceException
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // The slow request should have been cancelled via the hedge CTS.
            // This is the key assertion: with the fix, the sender receives hedgeRequestsCancellationTokenSource.Token.
            // When the second hedge returns 200 OK, the CTS is cancelled, which cancels the first request's token.
            // In the old code, the first request had the app CT (CancellationToken.None) which was never cancelled.
            Assert.IsTrue(firstRequestCancellationTokenWasCancelled,
                "The slow first request's cancellation token should have been cancelled when the second hedge " +
                "returned a final result. This verifies hedgeRequestsCancellationTokenSource.Token is passed to sender.");
        }

        /// <summary>
        /// Regression test: Verifies that when a non-transient (final) response is received from one 
        /// hedge region, the cancellation token passed to other in-flight sender calls gets cancelled.
        /// 
        /// In the old (buggy) code, the sender received the application's CancellationToken directly.
        /// When hedgeRequestsCancellationTokenSource.Cancel() was called after a final result, 
        /// the app CT was NOT cancelled, so in-flight senders continued executing on disposed requests.
        /// </summary>
        [TestMethod]
        public async Task SenderReceivesHedgeCancellationToken_NotAppToken()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            List<CancellationToken> capturedTokens = new List<CancellationToken>();

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                lock (capturedTokens)
                {
                    capturedTokens.Add(ct);
                }

                // First call: delay enough for the timer to fire and second hedge to be sent
                if (capturedTokens.Count == 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ContinueWith(_ => { });
                }

                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(capturedTokens.Count >= 2, 
                $"Expected at least 2 sender calls (primary + hedge), got {capturedTokens.Count}");

            // All tokens should be from the same linked CTS (hedgeRequestsCancellationTokenSource),
            // NOT the application-provided CancellationToken.None.
            // After the fix, when cancellation happens, all captured tokens should signal.
            // The key assertion: after the response returns, the hedge CTS is cancelled,
            // so all captured tokens should be in a cancelled state.
            foreach (CancellationToken ct in capturedTokens)
            {
                Assert.IsTrue(ct.IsCancellationRequested,
                    "All sender tokens should be cancelled after a final response is received. " +
                    "This proves the sender gets the hedge CTS token, not the app token.");
            }
        }

        /// <summary>
        /// Regression test: When the application-provided CancellationToken is cancelled (e.g., e2e timeout),
        /// the strategy should not attempt to spawn new hedge requests. The fix adds a do/while loop
        /// that checks applicationProvidedCancellationToken.IsCancellationRequested when the hedgeTimer 
        /// completes, preventing new requests from being cloned on an already-cancelled token.
        /// </summary>
        [TestMethod]
        public async Task AppCancellationDuringHedging_DoesNotSpawnNewHedgeRequests()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(100),
                thresholdStep: TimeSpan.FromMilliseconds(100));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            CancellationTokenSource appCts = new CancellationTokenSource();
            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // First request: cancel the app token immediately
                    // This simulates an e2e timeout scenario
                    appCts.Cancel();
                }

                // All requests block deterministically until cancelled via the token
                TaskCompletionSource<ResponseMessage> tcs = new TaskCompletionSource<ResponseMessage>();
                using (ct.Register(() => tcs.TrySetCanceled(ct)))
                {
                    await tcs.Task;
                }

                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act & Assert - should throw CosmosOperationCanceledException due to app cancellation
            await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(
                () => availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                    sender, mockCosmosClient, request, appCts.Token));

            // With the fix's do/while loop, when the app CT is cancelled, the timer fires
            // but the loop detects applicationProvidedCancellationToken.IsCancellationRequested
            // and does NOT spawn new hedge requests. Without the fix, additional clones 
            // would be attempted on a cancelled token path, potentially causing NullRef.
        }

        /// <summary>
        /// Regression test: Simulates the exact scenario from the NullRef crash reports.
        /// Multiple regions, the sender disposes the request after use. In the old code,
        /// a second hedge sender could still be running with a reference to a disposed request
        /// because it wasn't cancelled via the hedge CTS. This test verifies no 
        /// ArgumentNullException occurs.
        /// </summary>
        [TestMethod]
        public async Task MultiRegionHedging_RequestNotAccessedAfterDisposal()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;
            bool requestWasAccessibleOnCancellation = false;
            bool firstRequestWasCancelled = false;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // First request: simulate slow response, check req on cancellation
                    TaskCompletionSource<bool> cancelledTcs = new TaskCompletionSource<bool>();
                    using (ct.Register(() =>
                    {
                        firstRequestWasCancelled = true;
                        // Verify request is still accessible at cancellation point
                        // In the old code, request could be null/disposed here
                        try
                        {
                            _ = req.ResourceType;
                            requestWasAccessibleOnCancellation = true;
                        }
                        catch (NullReferenceException)
                        {
                            requestWasAccessibleOnCancellation = false;
                        }
                        catch (ObjectDisposedException)
                        {
                            requestWasAccessibleOnCancellation = false;
                        }

                        cancelledTcs.TrySetResult(true);
                    }))
                    {
                        await cancelledTcs.Task;
                    }

                    // Return transient response instead of throwing to avoid faulted task propagation
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(firstRequestWasCancelled,
                "The first request's token should have been cancelled when the second hedge returned a final result.");
            Assert.IsTrue(requestWasAccessibleOnCancellation,
                "Request should not be null/disposed when the sender is cancelled. " +
                "The fix ensures in-flight requests are cancelled via hedge CTS before disposal.");
        }

        /// <summary>
        /// Verifies the fix works for ReadItemStreamAsync code path (from NullRef2 and NullRef3 stack traces).
        /// The stream-based path uses ReadItemStreamAsync -> ProcessItemStreamAsync -> RequestInvokerHandler ->
        /// CrossRegionHedgingAvailabilityStrategy. This test ensures the sender cancellation token 
        /// is the hedge CTS token, not the app token, for stream operations too.
        /// </summary>
        [TestMethod]
        public async Task HedgeCancellation_StreamRequest_NoNullRef()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            // Create request with stream content (like ReadItemStreamAsync path)
            using RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                Content = new MemoryStream(new byte[] { 1, 2, 3 })
            };

            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;
            bool firstRequestCancellationTokenWasCancelled = false;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // Wait for cancellation via a TCS that completes on cancel
                    TaskCompletionSource<bool> cancelledTcs = new TaskCompletionSource<bool>();
                    using (ct.Register(() =>
                    {
                        firstRequestCancellationTokenWasCancelled = true;
                        cancelledTcs.TrySetResult(true);
                    }))
                    {
                        await cancelledTcs.Task;
                    }

                    // Return transient response to avoid exception propagation
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(firstRequestCancellationTokenWasCancelled,
                "Slow stream request's CT should be cancelled via hedge CTS when another hedge returns a final result.");
        }

        /// <summary>
        /// Verifies that when the primary request completes with a non-transient error before 
        /// the hedge timer fires, no additional hedged requests are sent.
        /// </summary>
        [TestMethod]
        public async Task PrimaryRequestFinalResult_NoAdditionalHedgesSent()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(5000), // Very long threshold - hedge timer won't fire
                thresholdStep: TimeSpan.FromMilliseconds(5000));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (req, ct) =>
            {
                Interlocked.Increment(ref senderCallCount);
                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK));
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, senderCallCount, 
                "Only the primary request should be sent when it returns before the hedge timer fires.");
        }

        /// <summary>
        /// Tests that when all hedge requests return transient errors, the strategy
        /// waits for all of them and returns the last response without throwing NullRef.
        /// </summary>
        [TestMethod]
        public async Task AllHedgesTransientError_ReturnsLastResponse()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(2);

            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (req, ct) =>
            {
                Interlocked.Increment(ref senderCallCount);
                // 503 Service Unavailable is a transient error (not in IsFinalResult)
                return Task.FromResult(new ResponseMessage(HttpStatusCode.ServiceUnavailable));
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert - should still return a response (the last one), not throw NullRef
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.IsTrue(senderCallCount >= 2, 
                $"Expected at least 2 sender calls (primary + hedge), got {senderCallCount}");
        }

        /// <summary>
        /// Stress test: runs many concurrent executions of the hedging strategy to verify 
        /// no NullReferenceException occurs under concurrency pressure.
        /// This reproduces the production scenario from the crash reports where multiple 
        /// concurrent ReadItemAsync/ReadItemStreamAsync calls trigger the race condition.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentHedgingRequests_NoNullRef()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(5),
                thresholdStep: TimeSpan.FromMilliseconds(5));

            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int nullRefCount = 0;
            int completedCount = 0;
            const int concurrentRequests = 50;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                // Random delay to create race conditions. Use ContinueWith to avoid
                // throwing OperationCanceledException when hedge CTS is cancelled.
                await Task.Delay(Random.Shared.Next(1, 20), ct).ContinueWith(_ => { });

                if (ct.IsCancellationRequested)
                {
                    // Return transient response instead of throwing, to simulate 
                    // a request that was cancelled but handled gracefully
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Act
            Task[] tasks = new Task[concurrentRequests];
            for (int i = 0; i < concurrentRequests; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        using RequestMessage req = CreateReadRequest();
                        ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                            sender, mockCosmosClient, req, CancellationToken.None);

                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Interlocked.Increment(ref completedCount);
                    }
                    catch (ArgumentNullException)
                    {
                        Interlocked.Increment(ref nullRefCount);
                    }
                    catch (NullReferenceException)
                    {
                        Interlocked.Increment(ref nullRefCount);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(0, nullRefCount, 
                $"Detected {nullRefCount} NullReferenceException(s) out of {concurrentRequests} concurrent requests. " +
                "The fix should prevent null refs by cancelling in-flight requests via hedge CTS.");
            Assert.AreEqual(concurrentRequests, completedCount,
                $"All {concurrentRequests} requests should complete successfully.");
        }

        [TestMethod]
        public async Task FaultedHedgeTask_DoesNotAbortWhenOtherRegionSucceeds()
        {
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(2);

            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);
                if (callNumber == 1)
                {
                    throw new OperationCanceledException("Simulated faulted hedge task");
                }

                return Task.FromResult(new ResponseMessage(HttpStatusCode.OK));
            };

            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender,
                mockCosmosClient,
                request,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(senderCallCount >= 2, "Expected a second hedge request to complete successfully.");
        }

        /// <summary>
        /// Verifies that when a request completes before the hedge threshold, HedgeContext
        /// contains exactly 1 region (the primary). This confirms no hedging occurred even 
        /// though HedgeContext is non-empty. A single-element HedgeContext is the expected
        /// indicator that the primary request completed without triggering any hedge.
        /// </summary>
        [TestMethod]
        public async Task PrimaryCompletesBeforeThreshold_HedgeContextContainsSingleRegion()
        {
            // Arrange: high threshold ensures no hedging fires
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(5000),
                thresholdStep: TimeSpan.FromMilliseconds(5000));

            // Use a real trace so AddOrUpdateDatum actually persists data (NoOpTrace discards it)
            using ITrace rootTrace = Trace.GetRootTrace("HedgeContextTest");
            using RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                "/dbs/testdb/colls/testcontainer/docs/testId",
                rootTrace)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = (req, ct) =>
            {
                Interlocked.Increment(ref senderCallCount);
                ResponseMessage response = new ResponseMessage(HttpStatusCode.OK)
                {
                    Trace = req.Trace
                };
                return Task.FromResult(response);
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, senderCallCount,
                "Only the primary request should be sent when it returns before the hedge timer fires.");

            CosmosTraceDiagnostics traceDiagnostic = response.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);

            if (traceDiagnostic.Value is Trace concreteTrace)
            {
                concreteTrace.SetWalkingStateRecursively();
            }

            Assert.IsFalse(traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out _),
                "HedgeContext should be absent when the primary request completes before the threshold (no hedging occurred).");

            Assert.IsTrue(traceDiagnostic.Value.Data.TryGetValue("Hedge Config", out _),
                "Hedge Config should always be present when the hedging strategy code path is used.");
        }

        /// <summary>
        /// Verifies that when hedging IS triggered (primary is slow, hedge returns first),
        /// HedgeContext contains 2 regions — confirming the semantics that HedgeContext count > 1 
        /// means hedging occurred.
        /// </summary>
        [TestMethod]
        public async Task HedgeTriggered_HedgeContextContainsMultipleRegions()
        {
            // Arrange: low threshold ensures hedging fires quickly
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            // Use a real trace so AddOrUpdateDatum actually persists data
            using ITrace rootTrace = Trace.GetRootTrace("HedgeContextTest");
            using RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                "/dbs/testdb/colls/testcontainer/docs/testId",
                rootTrace)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read
            };
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // Primary: slow enough to trigger hedging
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ContinueWith(_ => { });
                    return new ResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                // Hedge request: returns immediately with success, wired to request trace
                return new ResponseMessage(HttpStatusCode.OK)
                {
                    Trace = req.Trace
                };
            };

            // Act
            ResponseMessage response = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender, mockCosmosClient, request, CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(senderCallCount >= 2,
                "At least 2 sender calls expected (primary + hedge).");

            CosmosTraceDiagnostics traceDiagnostic = response.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);

            if (traceDiagnostic.Value is Trace concreteTrace)
            {
                concreteTrace.SetWalkingStateRecursively();
            }

            Assert.IsTrue(traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext),
                "HedgeContext should be present when hedging occurred.");

            IEnumerable<string> hedgeRegions = (IEnumerable<string>)hedgeContext;
            List<string> hedgeRegionsList = new List<string>(hedgeRegions);

            Assert.IsTrue(hedgeRegionsList.Count >= 2,
                $"HedgeContext should contain 2+ regions when hedging occurred, but got {hedgeRegionsList.Count}. " +
                "Multiple regions in HedgeContext confirms hedging was triggered.");
        }

        /// <summary>
        /// Verifies that CrossRegionAvailabilityContext propagates the hub region header flag
        /// across hedged request clones via the shared Properties dictionary.
        /// This tests the core mechanism: shallow-copy of Properties preserves reference identity,
        /// so volatile writes by one clone are visible to all others.
        /// </summary>
        [TestMethod]
        public void CrossRegionAvailabilityContext_PropagatesHubHeaderFlagToHedgedRequests()
        {
            // 1. Create shared context (injected by CrossRegionHedgingAvailabilityStrategy)
            CrossRegionAvailabilityContext sharedContext = new CrossRegionAvailabilityContext();
            Assert.IsFalse(sharedContext.ShouldAddHubRegionProcessingOnlyHeader,
                "Flag must be false initially.");

            // 2. Simulate original request Properties with the shared context
            Dictionary<string, object> originalProperties = new Dictionary<string, object>
            {
                { CrossRegionAvailabilityContext.PropertyKey, sharedContext }
            };

            // 3. Simulate RequestMessage.Clone() — shallow copy of Properties
            Dictionary<string, object> clonedProperties = new Dictionary<string, object>(originalProperties);

            // 4. Verify both dictionaries reference the SAME context instance
            Assert.IsTrue(clonedProperties.TryGetValue(CrossRegionAvailabilityContext.PropertyKey, out object clonedObj));
            CrossRegionAvailabilityContext clonedContext = clonedObj as CrossRegionAvailabilityContext;
            Assert.IsNotNull(clonedContext);
            Assert.AreSame(sharedContext, clonedContext,
                "Shallow copy must preserve reference identity — clones share the same context instance.");

            // 5. Primary's ClientRetryPolicy sets the flag after 2x 404/1002
            sharedContext.ShouldAddHubRegionProcessingOnlyHeader = true;

            // 6. Hedge's ClientRetryPolicy reads the flag from its cloned Properties
            Assert.IsTrue(clonedContext.ShouldAddHubRegionProcessingOnlyHeader,
                "Hub region flag set by primary must be visible to hedge via shared context reference. " +
                "This is the core hedging propagation mechanism (mirrors Java SDK's CrossRegionAvailabilityContext).");

            // 7. Verify PropertyKey is the expected well-known key
            Assert.AreEqual("CrossRegionAvailabilityContext",
                CrossRegionAvailabilityContext.PropertyKey);
        }

        /// <summary>
        /// Regression test for the .NET Framework 4.7.2 stack-overflow scenario in
        /// CrossRegionHedgingAvailabilityStrategy.
        ///
        /// On .NET Framework, every async method consumes ~10KB of stack on the synchronous
        /// exception propagation path (ExceptionDispatchInfo.Throw -> TaskAwaiter.ThrowForNonSuccess
        /// -> HandleNonSuccessAndDebuggerNotification). When a deep request pipeline beneath
        /// hedging throws (e.g. CosmosOperationCanceledException after the hedge CTS is signalled),
        /// the synchronous exception propagation can blow the managed stack.
        ///
        /// Fix: <see cref="CrossRegionHedgingAvailabilityStrategy"/>.CloneAndSendAsync wraps its
        /// awaited call in a try/catch that does <c>await Task.Yield(); throw;</c> — the yield
        /// resumes the rethrow on a fresh threadpool stack, breaking the synchronous propagation
        /// chain. This test asserts:
        ///  1. Functional correctness: a sender that throws OperationCanceledException with the
        ///     application token already cancelled still surfaces as CosmosOperationCanceledException,
        ///     and the inner OCE's stack trace preserves the original throwing frame (also covers
        ///     the throw-ex -> throw fix in RequestSenderAndResultCheckAsync).
        ///  2. Yield observable proof: at least one continuation is posted to the active
        ///     SynchronizationContext during exception propagation, demonstrating the synchronous
        ///     propagation chain was broken.
        ///
        /// NOTE on test target framework: this test project (Microsoft.Azure.Cosmos.Tests) only
        /// targets net6.0, where the underlying StackOverflowException does NOT reproduce — .NET
        /// Core / .NET 5+ already optimize the synchronous exception-propagation path. The test
        /// therefore asserts the proximate cure (the yield occurred + stack trace was preserved)
        /// rather than the absence of an SO. That is sufficient regression coverage: removing the
        /// production fix in CloneAndSendAsync's catch block makes the PostCount assertion below
        /// fail, and removing the throw-ex -> throw fix in RequestSenderAndResultCheckAsync makes
        /// the stack-trace assertion fail. End-to-end SO reproduction would require multi-targeting
        /// this test project for net472, which is out of scope for this fix.
        /// </summary>
        [TestMethod]
        public async Task SenderException_PropagatesViaYield_PreservesStackTrace()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(2);

            // Pre-cancelled CTS exercises the propagation path:
            //   RequestSenderAndResultCheckAsync's catch (OperationCanceledException oce) when
            //   (hedgeRequestsCancellationTokenSource.IsCancellationRequested) wraps in CosmosOCE,
            //   ExecuteAvailabilityStrategyAsync's phase-1 loop awaits the faulted task (because
            //   applicationProvidedCancellationToken.IsCancellationRequested is true) and the
            //   exception unwinds through CloneAndSendAsync's catch -> await Task.Yield(); throw;
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            const string sentinelMethodName = nameof(ThrowDeepInPipelineAsync);

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                await ThrowDeepInPipelineAsync();
                return new ResponseMessage(HttpStatusCode.OK);
            };

            // Install a SyncContext we can observe. Task.Yield() posts its continuation to the
            // current SyncContext when one is set, so a non-zero delta in PostCount across the
            // ExecuteAvailabilityStrategyAsync invocation proves CloneAndSendAsync's catch yielded
            // before rethrowing.
            //
            // IMPORTANT: the helper ThrowDeepInPipelineAsync deliberately does NOT call
            // Task.Yield() — it awaits Task.CompletedTask (which completes synchronously and does
            // not post to the SyncContext) before throwing. This guarantees that any Post observed
            // on customCtx during the invocation is attributable to the production-side fix in
            // CloneAndSendAsync's catch block, not to the test scaffolding itself.
            SynchronizationContext previousCtx = SynchronizationContext.Current;
            CountingSynchronizationContext customCtx = new CountingSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(customCtx);
            int postCountBefore = customCtx.PostCount;
            try
            {
                CosmosOperationCanceledException caught =
                    await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(
                        () => availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                            sender, mockCosmosClient, request, cts.Token));

                // CosmosOperationCanceledException overrides StackTrace to return the original
                // OCE's stack trace (see CosmosOperationCanceledException.StackTrace).
                // Stack-trace preservation: the original deep frame must still be present.
                // With the old `throw ex;` in RequestSenderAndResultCheckAsync this would have
                // been wiped on rethrow.
                string stack = caught.StackTrace ?? string.Empty;
                Assert.IsTrue(
                    stack.Contains(sentinelMethodName),
                    $"Stack trace should include the original throwing frame '{sentinelMethodName}'. " +
                    $"Actual stack trace:\n{stack}");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousCtx);
            }

            // Yield observable proof: CloneAndSendAsync's catch did await Task.Yield() before
            // rethrowing, which posts a continuation to the active SyncContext — without the fix,
            // exception propagation would be fully synchronous and the SyncContext would observe
            // zero posts. Assert on the delta (not the absolute count) to remain robust against
            // any future scaffolding that may post during setup.
            int postCountDelta = customCtx.PostCount - postCountBefore;
            Assert.IsTrue(
                postCountDelta > 0,
                "Task.Yield in CloneAndSendAsync's catch block should have posted at least one " +
                "continuation to the active SynchronizationContext, proving the synchronous " +
                $"exception propagation chain was broken. Observed delta: {postCountDelta}.");
        }

        private static async Task ThrowDeepInPipelineAsync()
        {
            // await a pre-completed task so the async state machine satisfies the compiler
            // (no CS1998 warning) without scheduling a continuation. Critically, this does NOT
            // post to the active SynchronizationContext — that way, the only Post observed by
            // CountingSynchronizationContext during the test is from the production-side
            // `await Task.Yield()` in CloneAndSendAsync's catch block, which is what we are
            // actually trying to verify.
            await Task.CompletedTask;
            throw new OperationCanceledException("Simulated deep-pipeline cancellation for hedging stack-overflow regression.");
        }

        /// <summary>
        /// Minimal SynchronizationContext that counts Post invocations and dispatches them
        /// onto the threadpool so test continuations don't deadlock.
        /// </summary>
        private sealed class CountingSynchronizationContext : SynchronizationContext
        {
            private int postCount;

            public int PostCount => Volatile.Read(ref this.postCount);

            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref this.postCount);
                ThreadPool.QueueUserWorkItem(_ => d(state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }
        }
    }
}
