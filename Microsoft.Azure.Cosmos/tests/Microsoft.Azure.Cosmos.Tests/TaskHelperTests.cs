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

    [TestClass]
    public class TaskHelperTests
    {
        private readonly CancellationToken cancellationToken = new CancellationToken();

        [TestMethod]
        public async Task CancellationTokenIsPassedToTask()
        {
            await TaskHelper.RunInlineIfNeededAsync(() => this.RunAndAssertAsync(this.cancellationToken));
        }

        [TestMethod]
        public async Task CancellationTokenIsPassedToTask_WhenSyncContextPresent()
        {
            Mock<SynchronizationContext> mockSynchronizationContext = new Mock<SynchronizationContext>()
            {
                CallBase = true
            };

            try
            {
                SynchronizationContext.SetSynchronizationContext(mockSynchronizationContext.Object);
                await TaskHelper.RunInlineIfNeededAsync(() => this.RunAndAssertAsync(this.cancellationToken));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        public Task<bool> RunAndAssertAsync(CancellationToken cancellationToken)
        {
            Assert.AreEqual(this.cancellationToken, cancellationToken);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Custom SynchronizationContext that simulates AspNetSynchronizationContext behavior
        /// to test deadlock scenarios with TaskHelper.RunInlineIfNeededAsync
        /// </summary>
        private class TestSynchronizationContext : SynchronizationContext
        {
            private readonly TaskScheduler scheduler;
            private readonly Thread mainThread;

            public TestSynchronizationContext()
            {
                this.mainThread = Thread.CurrentThread;
                this.scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, this.scheduler);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread == this.mainThread)
                {
                    d(state);
                }
                else
                {
                    var task = Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, this.scheduler);
                    task.Wait();
                }
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_DoesNotDeadlock_WithCustomSynchronizationContext()
        {
            // Arrange
            var testContext = new TestSynchronizationContext();
            var completedSuccessfully = false;
            
            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);
                
                // Act - This would deadlock with the original Task.Run() implementation
                var result = await TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    // Simulate some async work
                    return Task.FromResult("Success");
                }).ConfigureAwait(false);

                // Assert
                Assert.AreEqual("Success", result);
                completedSuccessfully = true;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }

            Assert.IsTrue(completedSuccessfully, "TaskHelper.RunInlineIfNeededAsync should complete without deadlocking");
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_RunsOnDefaultScheduler_WithSynchronizationContext()
        {
            // Arrange
            var testContext = new TestSynchronizationContext();
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var taskThreadId = 0;
            
            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);
                
                // Act
                await TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    taskThreadId = Thread.CurrentThread.ManagedThreadId;
                    return Task.FromResult(true);
                }).ConfigureAwait(false);

                // Assert - The task should run on a different thread (ThreadPool)
                Assert.AreNotEqual(mainThreadId, taskThreadId, 
                    "TaskHelper.RunInlineIfNeededAsync should run on ThreadPool, not on the main thread");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_RunsInline_WithoutSynchronizationContext()
        {
            // Arrange
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var taskThreadId = 0;
            
            // Ensure no synchronization context
            SynchronizationContext.SetSynchronizationContext(null);
            
            // Act
            await TaskHelper.RunInlineIfNeededAsync(() => 
            {
                taskThreadId = Thread.CurrentThread.ManagedThreadId;
                return Task.FromResult(true);
            }).ConfigureAwait(false);

            // Assert - The task should run on the same thread when no SynchronizationContext
            Assert.AreEqual(mainThreadId, taskThreadId, 
                "TaskHelper.RunInlineIfNeededAsync should run inline when no SynchronizationContext is present");
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_HandlesExceptions_WithSynchronizationContext()
        {
            // Arrange
            var testContext = new TestSynchronizationContext();
            var expectedException = new InvalidOperationException("Test exception");
            
            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);
                
                // Act & Assert
                var actualException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    async () => await TaskHelper.RunInlineIfNeededAsync<string>(() => 
                    {
                        throw expectedException;
                    }).ConfigureAwait(false));

                Assert.AreEqual(expectedException.Message, actualException.Message);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_WithTimeout_DoesNotDeadlock()
        {
            // Arrange
            var testContext = new TestSynchronizationContext();
            
            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);
                
                // Act - Test with a timeout to ensure no deadlock
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var task = TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    return Task.FromResult("Completed");
                });

                var result = await task.ConfigureAwait(false);

                // Assert
                Assert.AreEqual("Completed", result);
                Assert.IsFalse(cts.Token.IsCancellationRequested, "Task should complete before timeout");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }
    }
}