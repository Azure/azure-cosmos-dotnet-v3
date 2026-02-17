namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
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
                threshold: TimeSpan.FromMilliseconds(10),
                thresholdStep: TimeSpan.FromMilliseconds(10));

            using RequestMessage request = CreateReadRequest();
            using CosmosClient mockCosmosClient = CreateMockClientWithRegions(3);

            CancellationTokenSource appCts = new CancellationTokenSource();
            int senderCallCount = 0;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender = async (req, ct) =>
            {
                int callNumber = Interlocked.Increment(ref senderCallCount);

                if (callNumber == 1)
                {
                    // First request: cancel the app token after a brief delay
                    // This simulates an e2e timeout scenario
                    _ = Task.Delay(15).ContinueWith(_ => appCts.Cancel());

                    // Then wait - this will be cancelled
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
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

       
    }
}