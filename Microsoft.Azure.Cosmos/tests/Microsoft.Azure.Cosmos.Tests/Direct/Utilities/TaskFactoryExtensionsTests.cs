//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests that verify thunking layer for current task scheduler in <see cref="TaskFactoryExtensions"/>
    /// </summary>
    [TestClass]
    public sealed class TaskFactoryExtensionsTests
    {
        /// <summary>
        /// Test task scheduler used for verification of ambient task scheduler propagation
        /// </summary>
        private static TestTaskScheduler TestTaskSchedulerInstance = new TestTaskScheduler();

        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void Action()
        {
            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                {
                    // Expect to run in test task scheduler
                    VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                });
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action, CancellationToken cancellationToken)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void ActionAndCancellationToken()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                {
                    // Expect to run in test task scheduler
                    VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                },
                cts.Token);
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }


        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync(this TaskFactory taskFactory, Action action, TaskCreationOptions creationOptions)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void ActionAndTaskCreationOptions()
        {
            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                {
                    // Expect to run in test task scheduler
                    VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                },
                TaskCreationOptions.None);
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync{TResult}(this TaskFactory taskFactory, Func{TResult} function)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void Function()
        {
            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                Assert.AreEqual(
                    expected: 135246,
                    actual: await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                    {
                        // Expect to run in test task scheduler
                        VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                        return 135246;
                    }),
                    message: "Unexpected value returned from the function call");
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync{TResult}(this TaskFactory taskFactory, Func{TResult} function, CancellationToken cancellationToken)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void FunctionAndCancellationToken()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                Assert.AreEqual(
                    expected: 324354,
                    actual: await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                    {
                        // Expect to run in test task scheduler
                        VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                        return 324354;
                    },
                    cts.Token),
                    message: "Unexpected value returned from the function call");
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Test StartNewOnCurrentTaskSchedulerAsync{TResult}(this TaskFactory taskFactory, Func{TResult} function, TaskCreationOptions creationOptions)
        /// </summary>
        [TestMethod]
        [Owner("visridha")]
        public void FunctionAndTaskCreationOptions()
        {
            // Switch context to test task scheduler
            Task.Factory.StartNew(async () =>
            {
                // Task should have started in test task scheduler
                VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());

                // Kick off a task using thunking layer under test to see which task scheduler is passed in
                Assert.AreEqual(
                    expected: 531246,
                    actual: await Task.Factory.StartNewOnCurrentTaskSchedulerAsync(() =>
                    {
                        // Expect to run in test task scheduler
                        VerifyTaskScheduler(TaskFactoryExtensionsTests.TestTaskSchedulerInstance.GetType());
                        return 531246;
                    },
                    TaskCreationOptions.DenyChildAttach),
                    message: "Unexpected value returned from the function call");
            },
            cancellationToken: default(CancellationToken),
            creationOptions: TaskCreationOptions.DenyChildAttach,
            scheduler: TaskFactoryExtensionsTests.TestTaskSchedulerInstance).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Routine that is scheduled as a task to verify which task scheduler was used to execute it
        /// </summary>
        /// <param name="expectedTaskSchedulerType">Type of the task scheduler that is expected</param>
        /// <exception cref="Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException">Is thrown when task scheduler inside of which task is running is not expected</exception>
        private static void VerifyTaskScheduler(Type expectedTaskSchedulerType)
        {
            Assert.AreEqual(expectedTaskSchedulerType, TaskScheduler.Current.GetType(), "Task is running in unexpected task scheduler");
        }
    }
}