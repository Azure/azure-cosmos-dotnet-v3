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
            TryCatch<object> tryCatch = this.SomeNestedMethod();
            Assert.IsFalse(tryCatch.Succeeded);
            Exception exception = tryCatch.Exception;
            Assert.IsNotNull(exception.StackTrace);
        }

        private TryCatch<object> SomeNestedMethod()
        {
            TryCatch<object> tryCatch;
            try
            {
                this.SomeNestedMethod2();
                tryCatch = TryCatch<object>.FromResult(null);
            }
            catch (Exception ex)
            {
                tryCatch = TryCatch<object>.FromException(ex);
            }

            return tryCatch;
        }

        private void SomeNestedMethod2()
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
                .Try((result) =>
                {
                    Console.WriteLine($"Got a result: {result}");
                })
                .Catch((requestRateTooLargeException) =>
                {
                    Console.WriteLine($"Got a 429: {requestRateTooLargeException}");
                });
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
                onSuccess: (result) =>
                {
                    Console.WriteLine($"Got a result: {result}");
                },
                onError: (requestRateTooLargeException) =>
                {
                    Console.WriteLine($"Got a 429: {requestRateTooLargeException}");
                });
        }

        private static async Task<TryCatch<int>> FunctionThatThrows429()
        {
            Random random = new Random();
            TryCatch<int> tryResult;
            if (random.Next() % 2 == 0)
            {
                tryResult = TryCatch<int>.FromException(
                    new RequestRateTooLargeException(
                        new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 0, milliseconds: 1000)));
            }
            else
            {
                tryResult = TryCatch<int>.FromResult(random.Next());
            }

            return await Task.FromResult(tryResult);
        }

        private static async Task<TryCatch<int>> FunctionThatTriesToDoWorkButBubblesUpException1()
        {
            TryCatch<int> tryResult = await FunctionThatThrows429();

            // Just try to do your work. If it fails just bubble it up.
            return tryResult.Try((result) => { return result + 1; });
        }

        private static async Task<TryCatch<int>> FunctionThatTriesToDoWorkButBubblesUpException2()
        {
            TryCatch<int> tryResult = await FunctionThatTriesToDoWorkButBubblesUpException1();

            // Just try to do your work. If it fails just bubble it up.
            return tryResult.Try((result) => { return result + 2; });
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
                        if(!(exception is RequestRateTooLargeException requestRateTooLargeExecption))
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
