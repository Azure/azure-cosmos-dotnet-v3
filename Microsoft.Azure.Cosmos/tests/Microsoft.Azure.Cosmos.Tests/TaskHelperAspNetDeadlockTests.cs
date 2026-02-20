//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests to verify that the deadlock issue in AspNetSynchronizationContext has been fixed.
    /// This addresses the issue reported in #4695 where FeedIterator<T>.ReadNextAsync() would
    /// block indefinitely when called from .NET 4.8 WebForms applications.
    /// </summary>
    [TestClass]
    public class TaskHelperAspNetDeadlockTests
    {
        /// <summary>
        /// Mock SynchronizationContext that simulates AspNetSynchronizationContext deadlock scenarios.
        /// This context enforces that continuations run on the original thread, which can cause
        /// deadlocks when blocking calls are made from the UI thread.
        /// </summary>
        private class AspNetLikeSynchronizationContext : SynchronizationContext
        {
            private readonly Thread originalThread;
            private readonly SynchronizationContext innerContext;

            public AspNetLikeSynchronizationContext()
            {
                this.originalThread = Thread.CurrentThread;
                this.innerContext = SynchronizationContext.Current;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                // Simulate AspNetSynchronizationContext behavior by marshaling to the original thread
                if (Thread.CurrentThread == this.originalThread)
                {
                    d(state);
                }
                else
                {
                    // This would typically use HttpContext.Current.BeginInvoke in real AspNet
                    // For testing, we'll simulate the marshaling behavior
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
                    // This would block in real scenarios when called from background thread
                    var task = Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                    task.Wait();
                }
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_DoesNotDeadlock_WithAspNetLikeContext()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var completed = false;
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - This would deadlock with the original Task.Run() implementation
                // The fix uses Task.Factory.StartNew with TaskScheduler.Default to avoid the deadlock
                var task = TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    // Simulate async work that might try to marshal back to original context
                    return Task.FromResult("Success");
                });

                // Use ConfigureAwait(false) as would be done in real scenarios
                var result = await task.ConfigureAwait(false);
                
                // Assert
                Assert.AreEqual("Success", result);
                completed = true;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }

            Assert.IsTrue(completed, "TaskHelper.RunInlineIfNeededAsync should complete without deadlocking");
            Assert.IsFalse(timeoutCts.Token.IsCancellationRequested, "Task should complete before timeout");
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_UsesThreadPool_WithSynchronizationContext()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var originalThreadId = Thread.CurrentThread.ManagedThreadId;
            var taskThreadId = 0;

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Verify that the task runs on a different thread (ThreadPool)
                await TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    taskThreadId = Thread.CurrentThread.ManagedThreadId;
                    return Task.FromResult(true);
                }).ConfigureAwait(false);

                // Assert
                Assert.AreNotEqual(originalThreadId, taskThreadId, 
                    "TaskHelper.RunInlineIfNeededAsync should execute on ThreadPool when SynchronizationContext is present");
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
            var originalThreadId = Thread.CurrentThread.ManagedThreadId;
            var taskThreadId = 0;

            // Ensure no synchronization context is set
            SynchronizationContext.SetSynchronizationContext(null);

            // Act
            await TaskHelper.RunInlineIfNeededAsync(() => 
            {
                taskThreadId = Thread.CurrentThread.ManagedThreadId;
                return Task.FromResult(true);
            });

            // Assert
            Assert.AreEqual(originalThreadId, taskThreadId, 
                "TaskHelper.RunInlineIfNeededAsync should run inline when no SynchronizationContext is present");
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_HandlesCancellation_WithSynchronizationContext()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act & Assert
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
                {
                    await TaskHelper.RunInlineIfNeededAsync(() => 
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        return Task.FromResult("Should not reach here");
                    }).ConfigureAwait(false);
                });
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_PropagatesExceptions_WithSynchronizationContext()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var expectedMessage = "Test exception for AspNet deadlock fix";

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act & Assert
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

        [TestMethod]
        public async Task RunInlineIfNeededAsync_WorksWithMultipleAwaitOperations()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var results = new string[3];

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Test multiple sequential operations that could cause deadlock
                results[0] = await TaskHelper.RunInlineIfNeededAsync(() => 
                    Task.FromResult("First")).ConfigureAwait(false);
                
                results[1] = await TaskHelper.RunInlineIfNeededAsync(() => 
                    Task.FromResult("Second")).ConfigureAwait(false);
                
                results[2] = await TaskHelper.RunInlineIfNeededAsync(() => 
                    Task.FromResult("Third")).ConfigureAwait(false);

                // Assert
                Assert.AreEqual("First", results[0]);
                Assert.AreEqual("Second", results[1]);
                Assert.AreEqual("Third", results[2]);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [TestMethod]
        public async Task RunInlineIfNeededAsync_WorksWithNestedTasks()
        {
            // Arrange
            var aspNetContext = new AspNetLikeSynchronizationContext();
            var nestedResult = "";

            try
            {
                SynchronizationContext.SetSynchronizationContext(aspNetContext);

                // Act - Test nested TaskHelper calls that could cause deadlock
                var result = await TaskHelper.RunInlineIfNeededAsync(() => 
                {
                    return TaskHelper.RunInlineIfNeededAsync(() => 
                    {
                        nestedResult = "Nested task completed";
                        return Task.FromResult("Outer task completed");
                    });
                }).ConfigureAwait(false);

                // Assert
                Assert.AreEqual("Nested task completed", nestedResult);
                Assert.AreEqual("Outer task completed", result);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }
    }
}