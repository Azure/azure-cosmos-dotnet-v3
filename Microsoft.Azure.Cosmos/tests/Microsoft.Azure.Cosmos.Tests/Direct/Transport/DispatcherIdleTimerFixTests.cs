//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // Regression guards for the RNTBD idle-timer thread-pool starvation fix
    // (port of public-repo PR Azure/azure-cosmos-dotnet-v3#5817):
    //   1. Dispatcher.OnIdleTimer (sync, void) was renamed to OnIdleTimerAsync
    //      (async, Task) so the timer continuation no longer parks a thread-pool
    //      worker on Task.Wait() inside the close-connection branch.
    //   2. A sibling private helper Dispatcher.WaitTaskAsync was added next to
    //      the existing sync Dispatcher.WaitTask. The async helper awaits the
    //      passed Task instead of blocking on it.
    //   3. Dispatcher.ScheduleIdleTimer's ContinueWith chain is now terminated
    //      with .Unwrap() so the idle-timer Task completes when OnIdleTimerAsync
    //      finishes, not when it returns its inner Task.
    //
    // These tests are deliberately scoped to the private-helper surface (signature
    // shape + non-blocking await behavior). The end-to-end close-connection flow
    // is covered by the existing RntbdTests.IdleChannelClosure* integration tests.
    [TestClass]
    public class DispatcherIdleTimerFixTests
    {
        [TestMethod]
        [Owner("harshitdirect")]
        public void WaitTaskAsync_HasExpectedSignature()
        {
            MethodInfo m = typeof(Dispatcher).GetMethod(
                "WaitTaskAsync",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Task), typeof(string) },
                modifiers: null);

            Assert.IsNotNull(m, "Dispatcher.WaitTaskAsync(Task, string) must exist as a private instance method.");
            Assert.IsTrue(m.IsPrivate, "Dispatcher.WaitTaskAsync must be private.");
            Assert.AreEqual(typeof(Task), m.ReturnType, "Dispatcher.WaitTaskAsync must return Task.");

            ParameterInfo[] parameters = m.GetParameters();
            Assert.AreEqual(2, parameters.Length, "Dispatcher.WaitTaskAsync must take exactly two parameters.");
            Assert.AreEqual(typeof(Task), parameters[0].ParameterType, "First parameter must be Task.");
            Assert.AreEqual(typeof(string), parameters[1].ParameterType, "Second parameter must be string.");

            AsyncStateMachineAttribute asyncMarker = m.GetCustomAttribute<AsyncStateMachineAttribute>();
            Assert.IsNotNull(asyncMarker, "Dispatcher.WaitTaskAsync must be compiled with the async modifier (AsyncStateMachineAttribute missing).");
        }

        [TestMethod]
        [Owner("harshitdirect")]
        [Timeout(5000)]
        public async Task WaitTaskAsync_DoesNotBlockCallingThread()
        {
            Dispatcher dispatcher = CreateBareDispatcherForHelperTest();

            MethodInfo waitTaskAsync = typeof(Dispatcher).GetMethod(
                "WaitTaskAsync",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Task), typeof(string) },
                modifiers: null);
            Assert.IsNotNull(waitTaskAsync, "Dispatcher.WaitTaskAsync(Task, string) must exist.");

            // Pre-fix (t.Wait() inside WaitTask): the invocation would block the
            // current thread until tcs is resolved. Post-fix (await t): the
            // invocation suspends and immediately returns a non-completed Task.
            // We deliberately use SetResult only (never SetException). The catch
            // handler inside WaitTaskAsync dereferences this.ConnectionCorrelationId
            // -> this.connection.ConnectionCorrelationId, and the bare-Dispatcher
            // harness leaves this.connection null. Faulting the task would NRE
            // inside the catch handler rather than exercise the property under test.
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            Task returned = (Task)waitTaskAsync.Invoke(dispatcher, new object[] { tcs.Task, "test-description" });

            Assert.IsNotNull(returned, "WaitTaskAsync must return a non-null Task.");
            Assert.IsFalse(returned.IsCompleted,
                "WaitTaskAsync must suspend on the awaited task; a completed Task here proves the helper blocked the calling thread (sync regression).");

            tcs.SetResult(true);

            await returned;

            Assert.AreEqual(TaskStatus.RanToCompletion, returned.Status,
                "After the awaited task completes, the returned Task must also complete successfully.");
        }

        [TestMethod]
        [Owner("harshitdirect")]
        public void OnIdleTimerAsync_HasExpectedSignature()
        {
            MethodInfo m = typeof(Dispatcher).GetMethod(
                "OnIdleTimerAsync",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Task) },
                modifiers: null);

            Assert.IsNotNull(m, "Dispatcher.OnIdleTimerAsync(Task) must exist as a private instance method (rename from OnIdleTimer).");
            Assert.IsTrue(m.IsPrivate, "Dispatcher.OnIdleTimerAsync must be private.");
            Assert.AreEqual(typeof(Task), m.ReturnType, "Dispatcher.OnIdleTimerAsync must return Task (not void).");

            ParameterInfo[] parameters = m.GetParameters();
            Assert.AreEqual(1, parameters.Length, "Dispatcher.OnIdleTimerAsync must take exactly one parameter.");
            Assert.AreEqual(typeof(Task), parameters[0].ParameterType, "Parameter must be Task (the precedent timer task).");

            AsyncStateMachineAttribute asyncMarker = m.GetCustomAttribute<AsyncStateMachineAttribute>();
            Assert.IsNotNull(asyncMarker, "Dispatcher.OnIdleTimerAsync must be compiled with the async modifier (AsyncStateMachineAttribute missing).");

            MethodInfo oldSync = typeof(Dispatcher).GetMethod(
                "OnIdleTimer",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Task) },
                modifiers: null);
            Assert.IsNull(oldSync, "The old synchronous Dispatcher.OnIdleTimer(Task) must no longer exist (renamed to OnIdleTimerAsync).");
        }

        // Builds a Dispatcher instance without running the constructor so the
        // WaitTaskAsync non-blocking property can be exercised in isolation.
        // GetUninitializedObject skips inline initializers, so we reflect-set
        // the two lock fields that WaitTaskAsync reads via Monitor.IsEntered.
        // this.connection is intentionally left null; the recommended test
        // path uses TaskCompletionSource.SetResult only, so WaitTaskAsync's
        // catch handler (which dereferences this.connection.ConnectionCorrelationId)
        // is unreachable.
        private static Dispatcher CreateBareDispatcherForHelperTest()
        {
            Dispatcher d = (Dispatcher)FormatterServices.GetUninitializedObject(typeof(Dispatcher));

            FieldInfo callLock = typeof(Dispatcher).GetField(
                "callLock", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(callLock, "Dispatcher.callLock private field must exist for the test harness.");
            callLock.SetValue(d, new object());

            FieldInfo connectionLock = typeof(Dispatcher).GetField(
                "connectionLock", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(connectionLock, "Dispatcher.connectionLock private field must exist for the test harness.");
            connectionLock.SetValue(d, new object());

            return d;
        }
    }
}