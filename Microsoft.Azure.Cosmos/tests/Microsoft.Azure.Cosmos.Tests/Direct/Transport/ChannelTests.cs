namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public class ChannelTests
    {

        /// <summary>
        /// Validates that when a task is canceled after timeout, the exception is properly observed by HandleTaskTimeout
        /// and doesn't trigger an UnobservedTaskException.
        /// </summary>
        [TestMethod]
        [Owner("dikshibahl")]
        public async Task HandleTaskTimeout_ObservesCanceledTasks()
        {
            // Arrange
            Guid testActivityId = Guid.NewGuid();
            Guid testCorrelationId = Guid.NewGuid();

            // Use reflection to access the private HandleTaskTimeout method
            MethodInfo handleTaskTimeoutMethod = typeof(Channel).GetMethod("HandleTaskTimeout",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Create a task that will be canceled
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            CancellationTokenSource cts = new CancellationTokenSource();

            // Setup an event to be triggered by an unobserved exception
            bool unobservedExceptionOccurred = false;
            EventHandler<UnobservedTaskExceptionEventArgs> unobservedHandler = (sender, e) =>
            {
                unobservedExceptionOccurred = true;
                e.SetObserved(); // Mark as observed so the test doesn't crash
            };

            // Subscribe to the event
            TaskScheduler.UnobservedTaskException += unobservedHandler;

            try
            {
                // Start a task that will be canceled
                Task task = Task.Run(() =>
                {
                    return tcs.Task;
                });

                // Invoke HandleTaskTimeout to set up the continuation
                handleTaskTimeoutMethod.Invoke(null, new object[] { task, testActivityId, testCorrelationId });

                // Cancel the task
                tcs.SetCanceled();

                // Wait for the task to complete and for any continuations to run
                await Task.Delay(100);

                // Force garbage collection to trigger any unobserved exceptions
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Assert that no unobserved exception occurred
                Assert.IsFalse(unobservedExceptionOccurred, "An unobserved task exception was detected");
            }
            finally
            {
                // Clean up the event subscription
                TaskScheduler.UnobservedTaskException -= unobservedHandler;
            }
        }
    }
}