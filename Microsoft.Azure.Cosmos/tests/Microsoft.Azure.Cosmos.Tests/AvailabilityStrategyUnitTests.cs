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
        /// Reproduces the ArgumentNullException reported by customers in 
        /// CrossRegionHedgingAvailabilityStrategy.RequestSenderAndResultCheckAsync.
        /// 
        /// Root cause: When a hedge request completes successfully before the primary,
        /// ExecuteAvailabilityStrategyAsync returns immediately. The primary request task
        /// is still running because the sender receives the ORIGINAL cancellation token 
        /// (not the linked CTS token), so it is NOT cancelled. After the method returns,
        /// the using blocks dispose the CancellationTokenSource and CloneableStream, and
        /// the using block in CloneAndSendAsync disposes the cloned RequestMessage.
        /// The abandoned task eventually faults and is never observed, producing the 
        /// TaskScheduler_UnobservedTaskException with ArgumentNullException that customers see.
        ///
        /// This test directly verifies that when a hedge succeeds and the method returns,
        /// the abandoned primary task is properly cancelled and does not leave the cloned 
        /// request in a disposed state while the sender is still using it.
        /// </summary>
        [TestMethod]
        public async Task CloneAndSendAsync_DisposesClonedRequest_CausesNullReferenceOnAbandonedTask()
        {
            // Arrange
            CrossRegionHedgingAvailabilityStrategy availabilityStrategy =
                new CrossRegionHedgingAvailabilityStrategy(
                    threshold: TimeSpan.FromMilliseconds(100),
                    thresholdStep: TimeSpan.FromMilliseconds(50));

            RequestMessage request = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
            };

            AccountProperties databaseAccount = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                {
                    new AccountRegion() { Name = "US East", Endpoint = new Uri("https://location1.documents.azure.com").ToString() },
                    new AccountRegion() { Name = "US West", Endpoint = new Uri("https://location2.documents.azure.com").ToString() },
                    new AccountRegion() { Name = "US Central", Endpoint = new Uri("https://location3.documents.azure.com").ToString() },
                }
            };

            using CosmosClient mockCosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            mockCosmosClient.DocumentClient.GlobalEndpointManager.InitializeAccountPropertiesAndStartBackgroundRefresh(databaseAccount);

            int callCount = 0;
            TaskCompletionSource<bool> primarySenderEntered = new TaskCompletionSource<bool>();
            bool primaryTokenWasCancelled = false;

            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender =
                async (req, token) =>
                {
                    int currentCall = Interlocked.Increment(ref callCount);

                    if (currentCall == 1)
                    {
                        // Signal that the primary sender has started
                        primarySenderEntered.TrySetResult(true);

                        // Simulate a slow primary request. Use a polling loop so we
                        // can cleanly detect cancellation without throwing, to isolate
                        // the test assertion from cancellation exception handling.
                        int elapsed = 0;
                        while (elapsed < 10000 && !token.IsCancellationRequested)
                        {
                            await Task.Delay(50);
                            elapsed += 50;
                        }

                        if (token.IsCancellationRequested)
                        {
                            primaryTokenWasCancelled = true;
                            // Return a transient response instead of throwing,
                            // so the task completes cleanly and we can assert
                            // just on the cancellation behavior.
                            return new ResponseMessage(HttpStatusCode.ServiceUnavailable, requestMessage: req);
                        }

                        return new ResponseMessage(HttpStatusCode.OK, requestMessage: req);
                    }

                    // Hedge request: return success immediately
                    return new ResponseMessage(HttpStatusCode.OK, requestMessage: req);
                };

            // Act
            ResponseMessage result = await availabilityStrategy.ExecuteAvailabilityStrategyAsync(
                sender,
                mockCosmosClient,
                request,
                CancellationToken.None);

            // The hedge succeeded, so we should get a 200 OK
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

            // Verify the primary sender was invoked
            await primarySenderEntered.Task;

            // Wait a bit for things to settle
            await Task.Delay(500);

            // CRITICAL ASSERTION: The primary request's sender should have received 
            // a cancellation token that gets cancelled when the hedge succeeds.
            // In the current buggy code, it receives the ORIGINAL CancellationToken.None
            // which is NEVER cancelled, so the primary task keeps running indefinitely.
            // This is what leads to the abandoned task and the unobserved exception.
            Assert.IsTrue(
                primaryTokenWasCancelled,
                "The primary request's sender was invoked with a cancellation token that " +
                "was NOT cancelled when the hedge succeeded. This means the primary task " +
                "continues running after ExecuteAvailabilityStrategyAsync returns, and when " +
                "it eventually completes, the using block in CloneAndSendAsync disposes the " +
                "cloned request, causing the ArgumentNullException that customers reported. " +
                "The sender should receive the linked CancellationToken so it gets cancelled " +
                "when a hedge succeeds.");
        }
    }
}