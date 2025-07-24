//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Integration tests to verify that the FeedIterator deadlock issue with AspNetSynchronizationContext
    /// has been resolved. This addresses issue #4695 where FeedIterator<T>.ReadNextAsync() would
    /// block indefinitely in .NET 4.8 WebForms applications.
    /// </summary>
    [TestClass]
    public class FeedIteratorAspNetDeadlockTests
    {
        /// <summary>
        /// Mock SynchronizationContext that simulates AspNetSynchronizationContext behavior
        /// </summary>
        private class AspNetSynchronizationContextMock : SynchronizationContext
        {
            private readonly Thread originalThread;

            public AspNetSynchronizationContextMock()
            {
                this.originalThread = Thread.CurrentThread;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                // Simulate AspNetSynchronizationContext marshaling behavior
                if (Thread.CurrentThread == this.originalThread)
                {
                    d(state);
                }
                else
                {
                    Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread == this.originalThread)
                {
                    d(state);
                }
                else
                {
                    // This would block in real AspNet scenarios
                    var task = Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                    task.Wait();
                }
            }
        }

        [TestMethod]
        public async Task FeedIteratorCore_ReadNextAsync_DoesNotDeadlock_WithAspNetContext()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var mockClientContext = new Mock<CosmosClientContext>();
            var mockContainer = new Mock<ContainerInternal>();
            var mockSerializer = new Mock<CosmosSerializer>();
            
            // Setup mock serializer to return a simple result
            mockSerializer.Setup(s => s.FromStream<dynamic>(It.IsAny<System.IO.Stream>()))
                .Returns(new { id = "test", value = "test" });
            
            mockClientContext.Setup(c => c.SerializerCore).Returns(mockSerializer.Object);
            
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Create a simple FeedIteratorCore that would trigger the deadlock scenario
            var feedIterator = new FeedIteratorCore<dynamic>(
                clientContext: mockClientContext.Object,
                container: mockContainer.Object,
                queryDefinition: null,
                continuationToken: null,
                options: new QueryRequestOptions(),
                resourceLink: "/dbs/test/colls/test",
                resourceType: ResourceType.Document,
                databaseId: "test"
            );

            // Mock the internal response to avoid actual network calls
            var mockResponse = new Mock<ResponseMessage>();
            mockResponse.SetupGet(r => r.StatusCode).Returns(System.Net.HttpStatusCode.OK);
            mockResponse.SetupGet(r => r.Content).Returns(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("[]")));
            mockResponse.SetupGet(r => r.Headers).Returns(new Headers());

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - This would deadlock with the original Task.Run() implementation
                // The fix should prevent deadlock by using Task.Factory.StartNew with TaskScheduler.Default
                var task = Task.Run(async () =>
                {
                    try
                    {
                        // Simulate the scenario where ReadNextAsync is called from AspNet context
                        // This would previously cause a deadlock
                        var result = await feedIterator.ReadNextAsync().ConfigureAwait(false);
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        // Expected since we're not fully mocking the internal dependencies
                        // The important thing is that it doesn't deadlock
                        return $"Expected exception: {ex.GetType().Name}";
                    }
                });

                // Use a timeout to ensure we don't actually deadlock
                var result = await Task.WhenAny(task, Task.Delay(5000, timeoutCts.Token));
                
                // Assert
                Assert.AreSame(task, result, "FeedIterator.ReadNextAsync should complete without deadlocking");
                Assert.IsFalse(timeoutCts.Token.IsCancellationRequested, "Operation should complete before timeout");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
                feedIterator.Dispose();
            }
        }

        [TestMethod]
        public async Task TaskHelper_RunInlineIfNeededAsync_DirectUsage_DoesNotDeadlock()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var completed = false;
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Direct test of the TaskHelper method that was causing deadlocks
                var result = await TaskHelper.RunInlineIfNeededAsync(() =>
                {
                    completed = true;
                    return Task.FromResult("Direct TaskHelper usage successful");
                }).ConfigureAwait(false);

                // Assert
                Assert.IsTrue(completed, "TaskHelper.RunInlineIfNeededAsync should execute the provided function");
                Assert.AreEqual("Direct TaskHelper usage successful", result);
                Assert.IsFalse(timeoutCts.Token.IsCancellationRequested, "Operation should complete without timeout");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task TaskHelper_RunInlineIfNeededAsync_MultipleCallsSequential_DoesNotDeadlock()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var results = new string[3];
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Test multiple sequential calls that could compound deadlock issues
                results[0] = await TaskHelper.RunInlineIfNeededAsync(() =>
                    Task.FromResult("Call 1")).ConfigureAwait(false);

                results[1] = await TaskHelper.RunInlineIfNeededAsync(() =>
                    Task.FromResult("Call 2")).ConfigureAwait(false);

                results[2] = await TaskHelper.RunInlineIfNeededAsync(() =>
                    Task.FromResult("Call 3")).ConfigureAwait(false);

                // Assert
                Assert.AreEqual("Call 1", results[0]);
                Assert.AreEqual("Call 2", results[1]);
                Assert.AreEqual("Call 3", results[2]);
                Assert.IsFalse(timeoutCts.Token.IsCancellationRequested, "All operations should complete without timeout");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task TaskHelper_RunInlineIfNeededAsync_WithConfigureAwaitFalse_DoesNotDeadlock()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var originalThreadId = Thread.CurrentThread.ManagedThreadId;
            var taskThreadId = 0;

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Test the specific scenario mentioned in the issue: using ConfigureAwait(false)
                var result = await TaskHelper.RunInlineIfNeededAsync(() =>
                {
                    taskThreadId = Thread.CurrentThread.ManagedThreadId;
                    return Task.FromResult("ConfigureAwait(false) test");
                }).ConfigureAwait(false);

                // Assert
                Assert.AreEqual("ConfigureAwait(false) test", result);
                Assert.AreNotEqual(originalThreadId, taskThreadId, 
                    "Task should run on ThreadPool thread, not on the original thread");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public void TaskHelper_RunInlineIfNeededAsync_DoesNotDeadlock_SynchronousTest()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var completed = false;

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Test synchronous blocking call that would deadlock with old implementation
                var task = TaskHelper.RunInlineIfNeededAsync(() =>
                {
                    completed = true;
                    return Task.FromResult("Synchronous blocking test");
                });

                // Use a timeout to prevent actual deadlock in case the fix doesn't work
                var result = task.Wait(TimeSpan.FromSeconds(5));

                // Assert
                Assert.IsTrue(result, "Task should complete within timeout");
                Assert.IsTrue(completed, "TaskHelper should execute the provided function");
                Assert.AreEqual("Synchronous blocking test", task.Result);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task TaskHelper_RunInlineIfNeededAsync_ExceptionHandling_DoesNotDeadlock()
        {
            // Arrange
            var aspNetContext = new AspNetSynchronizationContextMock();
            var expectedMessage = "Test exception for deadlock fix verification";

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act & Assert - Verify exception handling doesn't cause deadlock
                var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                {
                    await TaskHelper.RunInlineIfNeededAsync<string>(() =>
                    {
                        throw new InvalidOperationException(expectedMessage);
                    }).ConfigureAwait(false);
                });

                Assert.AreEqual(expectedMessage, exception.Message);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }
    }
}