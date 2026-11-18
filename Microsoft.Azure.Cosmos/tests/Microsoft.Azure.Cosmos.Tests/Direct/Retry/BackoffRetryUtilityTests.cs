//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class BackoffRetryUtilityTests
    {
        private const int MaxRetries = 3;
        private RetriableRetryPolicy policy = new RetriableRetryPolicy();

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithException()
        {
            int counter = 0;
            bool calledPreRetry = false;
            Action<Exception> preRetryCallback = (Exception ex) =>
            {
                Assert.IsTrue(ex is NotFoundException);
                calledPreRetry = true;
            };

            await BackoffRetryUtility<bool>.ExecuteAsync(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(true);
                 },
                 this.policy,
                 CancellationToken.None,
                 preRetryCallback: preRetryCallback
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.IsTrue(calledPreRetry);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task Should_UseAlternateMethod()
        {
            int counter = 0;

            bool result = await BackoffRetryUtility<bool>.ExecuteAsync(
                 callbackMethod: () =>
                 {
                     counter++;
                     throw new NotFoundException();
                 },
                 retryPolicy: this.policy,
                 cancellationToken: CancellationToken.None,
                 minBackoffForInBackoffCallback: TimeSpan.Zero,
                 inBackoffAlternateCallbackMethod: () =>
                 {
                     return Task.FromResult(true);
                 }
                );

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.IsTrue(result);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldStopRetrying_WhenSignaledByPolicy_AndThrow()
        {
            int counter = 0;
            bool exceptionThrown = false;
            try
            {
                await BackoffRetryUtility<bool>.ExecuteAsync(
                         () =>
                         {
                             counter++;
                             throw new NotFoundException();
                         },
                         this.policy,
                         CancellationToken.None
                        );
            }
            catch (NotFoundException)
            {
                exceptionThrown = true;
            }

            Assert.AreEqual(MaxRetries, counter);
            Assert.IsTrue(exceptionThrown, "Exception not thrown.");
            Assert.AreEqual(MaxRetries, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldThrow_IfSignaledByInternalException()
        {
            int counter = 0;
            bool exceptionThrown = false;
            try
            {
                await BackoffRetryUtility<bool>.ExecuteAsync(
                         () =>
                         {
                             counter++;
                             throw new ServiceUnavailableException();
                         },
                         this.policy,
                         CancellationToken.None
                        );
            }
            catch (ServiceUnavailableException)
            {
                exceptionThrown = true;
            }

            Assert.AreEqual(1, counter);
            Assert.IsTrue(exceptionThrown, "Exception not thrown.");
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldNotRetry_OnValidResponse()
        {
            await BackoffRetryUtility<bool>.ExecuteAsync(
                 () =>
                 {
                     return Task.FromResult(true);
                 },
                 this.policy,
                 CancellationToken.None
                );

            Assert.AreEqual(0, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldCancel_OnTokenCancellation_ButStillCallPolicy()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            int counter = 0;
            try
            {
                await BackoffRetryUtility<bool>.ExecuteAsync(
                         () =>
                         {
                             counter++;
                             if (counter == 1)
                             {
                                 cancellationTokenSource.Cancel();
                                 throw new NotFoundException();
                             }

                             return Task.FromResult(true);
                         },
                         this.policy,
                         cancellationTokenSource.Token
                        );

                Assert.Fail("Failed to detect token cancellation.");
            }
            catch (OperationCanceledException)
            {
                // Expected exception
            }

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithAlternateMethod()
        {
            int counter = 0;
            TimeSpan timespan = TimeSpan.Zero;
            await BackoffRetryUtility<bool>.ExecuteAsync(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(true);
                 },
                 this.policy,
                 inBackoffAlternateCallbackMethod: () =>
                 {
                     return Task.FromResult(true);
                 },
                 timespan,
                 CancellationToken.None
                );

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithAlternateMethod_ThatThrows()
        {
            int counter = 0;
            TimeSpan timespan = TimeSpan.Zero;
            await BackoffRetryUtility<bool>.ExecuteAsync(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(true);
                 },
                 this.policy,
                 () =>
                 {
                     throw new NotFoundException();
                 },
                 timespan,
                 CancellationToken.None
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        [ExpectedException(typeof(WebException))]
        public async Task AvoidStackOverflowOnException()
        {
            await this.AvoidStackOverflowOnExceptionInternalAsync(2000);
        }

        private async Task<bool> AvoidStackOverflowOnExceptionInternalAsync(int count)
        {
            Func<Task<bool>> sendFunc = async () =>
            {
                if (count > 0)
                {
                    // Avoid StackOverflow for forward path, this test is just for Exception path
                    await Task.Yield();
                    return await this.AvoidStackOverflowOnExceptionInternalAsync(count - 1);
                }

                throw new WebException("", WebExceptionStatus.ConnectFailure);
            };

            Mock<IRetriableResponse> retriableResponse = new Mock<IRetriableResponse>();

            Mock<IRetryPolicy> retryPolicy = new Mock<IRetryPolicy>();

            retryPolicy
                .Setup(r => r.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns((Exception exception, CancellationToken cancellationToken) =>
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                });


            return await BackoffRetryUtility<bool>.ExecuteAsync(
                sendFunc,
                retryPolicy: retryPolicy.Object,
                cancellationToken: default(CancellationToken));
        }

        private class RetriableRetryPolicy : IRetryPolicy
        {
            public int ShouldRetryAsyncCalls { get; private set; }

            /// <summary>
            /// Should retry on 404 unless it hits the max retries.
            /// </summary>
            public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
            {
                this.ShouldRetryAsyncCalls++;
                if (exception is NotFoundException)
                {
                    if (this.ShouldRetryAsyncCalls == MaxRetries)
                    {
                        return Task.FromResult(ShouldRetryResult.NoRetry());
                    }

                    return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.Zero));
                }

                if (exception is ServiceUnavailableException)
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry(exception));
                }

                return Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }
    }
}
