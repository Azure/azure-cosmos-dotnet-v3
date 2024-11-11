//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TryCatchTests
    {
        [TestMethod]
        public void TestStackTrace()
        {
            TryCatch<object> tryCatch = this.MethodWhereExceptionWasCaught();
            Assert.IsFalse(tryCatch.Succeeded);
            Exception exception = tryCatch.Exception;
            Assert.IsNotNull(exception.StackTrace);
            // exception.ToString() >>
            //Microsoft.Azure.Cosmos.Query.Core.Monads.ExceptionWithStackTraceException: TryCatch resulted in an exception. --->System.Exception: Exception of type 'System.Exception' was thrown.
            //   at Microsoft.Azure.Cosmos.Tests.Query.TryCatchTests.MethodWhereExceptionWasThrown() in C:\azure - cosmos - dotnet - v3\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Query\TryCatchTests.cs:line 43
            //   at Microsoft.Azure.Cosmos.Tests.Query.TryCatchTests.MethodWhereExceptionWasCaught() in C:\azure - cosmos - dotnet - v3\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Tests\Query\TryCatchTests.cs:line 30
            //         -- - End of inner exception stack trace-- -
            //          at Microsoft.Azure.Cosmos.Tests.Query.TryCatchTests.MethodWhereExceptionWasCaught()
            //   at Microsoft.Azure.Cosmos.Tests.Query.TryCatchTests.TestStackTrace()
            //   at System.RuntimeMethodHandle.InvokeMethod(Object target, Object[] arguments, Signature sig, Boolean constructor, Boolean wrapExceptions)
            //   at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions.MethodInfoExtensions.InvokeAsSynchronousTask(MethodInfo methodInfo, Object classInstance, Object[] parameters)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo.ExecuteInternal(Object[] arguments)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo.Invoke(Object[] arguments)
            //   at Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute.Execute(ITestMethod testMethod)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner.RunTestMethod()
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner.Execute()
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest(TestMethod testMethod, IDictionary`2 testRunParameters)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.ExecuteTestsWithTestRunner(IEnumerable`1 tests, IRunContext runContext, ITestExecutionRecorder testExecutionRecorder, String source, UnitTestRunner testRunner)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.ExecuteTestsInSource(IEnumerable`1 tests, IRunContext runContext, IFrameworkHandle frameworkHandle, String source, Boolean isDeploymentDone)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.ExecuteTests(IEnumerable`1 tests, IRunContext runContext, IFrameworkHandle frameworkHandle, Boolean isDeploymentDone)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.RunTests(IEnumerable`1 tests, IRunContext runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken runCancellationToken)
            //   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestExecutor.RunTests(IEnumerable`1 tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
            //   at Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.BaseRunTests.RunTestInternalWithExecutors(IEnumerable`1 executorUriExtensionMap, Int64 totalTests)
            //   at Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.BaseRunTests.RunTestsInternal()
            //   at Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.BaseRunTests.RunTests()
            //   at Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.ExecutionManager.StartTestRun(IEnumerable`1 tests, String package, String runSettings, TestExecutionContext testExecutionContext, ITestCaseEventsHandler testCaseEventsHandler, ITestRunEventsHandler runEventsHandler)
            //   at Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.TestRequestHandler.<> c__DisplayClass30_4.< OnMessageReceived > b__4()
            //   at Microsoft.VisualStudio.TestPlatform.Utilities.JobQueue`1.SafeProcessJob(T job)
            //   at Microsoft.VisualStudio.TestPlatform.Utilities.JobQueue`1.BackgroundJobProcessor()
            //   at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
            //   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task & currentTaskSlot)
            //   at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
        }

        private TryCatch<object> MethodWhereExceptionWasCaught()
        {
            TryCatch<object> tryCatch;
            try
            {
                this.MethodWhereExceptionWasThrown();
                tryCatch = TryCatch<object>.FromResult(null);
            }
            catch (Exception ex)
            {
                tryCatch = TryCatch<object>.FromException(ex);
            }

            return tryCatch;
        }

        private void MethodWhereExceptionWasThrown()
        {
            throw new Exception();
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestRetryExceptionBubblingUp()
        {
            // This test demos how exceptions will bubble up using the try pattern instead of throwing and catching.
            TryCatch<int> tryMonand = await FunctionThatTriesToDoWorkButBubblesUpException2();

            tryMonand
                .Try((result) => Console.WriteLine($"Got a result: {result}"))
                .Catch((requestRateTooLargeException) => Console.WriteLine($"Got a 429: {requestRateTooLargeException}"));
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestRetryMonad429()
        {
            // This test demos how we can handle / retry exceptions using the try pattern.
            TryCatch<int> tryResult = await RetryHandler.Retry429<int>(
                FunctionThatThrows429,
                maxRetryCount: 100,
                maxDelayInMilliseconds: 1000000);

            tryResult.Match(
                onSuccess: (result) => Console.WriteLine($"Got a result: {result}"),
                onError: (requestRateTooLargeException) => Console.WriteLine($"Got a 429: {requestRateTooLargeException}"));
        }

        private static async Task<TryCatch<int>> FunctionThatThrows429()
        {
            Random random = new Random();
            TryCatch<int> tryResult = random.Next() % 2 == 0
                ? TryCatch<int>.FromException(
                    new RequestRateTooLargeException(
                        new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 0, milliseconds: 1000)))
                : TryCatch<int>.FromResult(random.Next());
            return await Task.FromResult(tryResult);
        }

        private static async Task<TryCatch<int>> FunctionThatTriesToDoWorkButBubblesUpException1()
        {
            TryCatch<int> tryResult = await FunctionThatThrows429();

            // Just try to do your work. If it fails just bubble it up.
            return tryResult.Try((result) => result + 1);
        }

        private static async Task<TryCatch<int>> FunctionThatTriesToDoWorkButBubblesUpException2()
        {
            TryCatch<int> tryResult = await FunctionThatTriesToDoWorkButBubblesUpException1();

            // Just try to do your work. If it fails just bubble it up.
            return tryResult.Try((result) => result + 2);
        }

        private static class RetryHandler
        {
            public static async Task<TryCatch<T>> Retry429<T>(
                Func<Task<TryCatch<T>>> function,
                int maxRetryCount = 10,
                int maxDelayInMilliseconds = 10000)
            {
                TryCatch<T> tryMonad = await function();
                return await tryMonad.CatchAsync(
                    onError: async (exception) =>
                    {
                        if (!(exception is RequestRateTooLargeException requestRateTooLargeExecption))
                        {
                            return TryCatch<T>.FromException(exception);
                        }

                        if (maxRetryCount <= 0)
                        {
                            return TryCatch<T>.FromException(requestRateTooLargeExecption);
                        }

                        if (requestRateTooLargeExecption.RetryAfter.TotalMilliseconds > maxDelayInMilliseconds)
                        {
                            return TryCatch<T>.FromException(requestRateTooLargeExecption);
                        }

                        await Task.Delay(requestRateTooLargeExecption.RetryAfter);

                        return await Retry429(
                            function,
                            maxRetryCount - 1,
                            maxDelayInMilliseconds);
                    });
            }
        }

        private sealed class RequestRateTooLargeException : Exception
        {
            public RequestRateTooLargeException(TimeSpan retryAfter)
            {
                this.RetryAfter = retryAfter;
            }

            public TimeSpan RetryAfter { get; }
        }
    }
}