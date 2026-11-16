//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Management.Tests
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="RequestRetryUtility".
    /// </summary>
    [TestClass]
    public class RequestRetryUtilityTests
    {
        private const int MaxRetries = 3;
        private RetriableRetryPolicy policy = new RetriableRetryPolicy();
        private GoneAndRetryWithRequestRetryPolicy<StoreResponse> goneAndRetryPolicy = new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(false, waitTimeInSecondsOverride: 1);
        private RetriableRetryPolicyWithCustomContext policyWithCustomContext = new RetriableRetryPolicyWithCustomContext();
        private RetriableResponse notFoundResponse = new RetriableResponse(HttpStatusCode.NotFound);
        private RetriableResponse foundResponse = new RetriableResponse(HttpStatusCode.OK);
        private RetriableRequest request = new RetriableRequest();

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithFailedStatusCode()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         return Task.FromResult(this.notFoundResponse);
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithFailedStatusCode_AndCustomContext()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<CustomContext, RetriableRequest, RetriableResponse>(
                 (CustomContext customContext) =>
                 {
                     counter++;
                     if (!customContext.HasChanged)
                     {
                         return Task.FromResult(this.notFoundResponse);
                     }

                     Assert.IsTrue(customContext.HasChanged);

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policyWithCustomContext,
                 CancellationToken.None
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policyWithCustomContext.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policyWithCustomContext.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithFailedStatusCode_AndCustomContext_AndInBackoffMethod()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<CustomContext, RetriableRequest, RetriableResponse>(
                 (CustomContext customContext) =>
                 {
                     counter++;
                     if (!customContext.HasChanged)
                     {
                         return Task.FromResult(this.notFoundResponse);
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policyWithCustomContext,
                 (CustomContext customContext) =>
                 {
                     Assert.IsTrue(customContext.HasChanged);

                     return Task.FromResult(this.foundResponse);
                 },
                 TimeSpan.Zero,
                 CancellationToken.None
                );

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policyWithCustomContext.ShouldRetryAsyncCalls);
            Assert.AreEqual(1, policyWithCustomContext.OnBeforeSendRequestCalls);
            Assert.AreEqual(this.foundResponse, response);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithException()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithException_AndCustomContext()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<CustomContext, RetriableRequest, RetriableResponse>(
                 (CustomContext customContext) =>
                 {
                     counter++;
                     if (!customContext.HasChanged)
                     {
                         throw new NotFoundException();
                     }

                     Assert.IsTrue(customContext.HasChanged);

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policyWithCustomContext,
                 CancellationToken.None
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policyWithCustomContext.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policyWithCustomContext.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldStopRetrying_WhenSignaledByPolicy()
        {
            int counter = 0;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     return Task.FromResult(this.notFoundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None
                );

            Assert.AreEqual(MaxRetries, counter);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(MaxRetries, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(MaxRetries, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldStopRetrying_WhenSignaledByPolicy_AndThrow()
        {
            int counter = 0;
            bool exceptionThrown = false;
            try
            {
                RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                         () =>
                         {
                             counter++;
                             throw new NotFoundException();
                         },
                         () => request,
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
            Assert.AreEqual(MaxRetries, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldThrow_IfSignaledByInternalException()
        {
            int counter = 0;
            bool exceptionThrown = false;
            try
            {
                RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                         () =>
                         {
                             counter++;
                             throw new ServiceUnavailableException();
                         },
                         () => request,
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
            Assert.AreEqual(1, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldNotRetry_OnValidResponse()
        {
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None
                );

            Assert.AreEqual(0, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(1, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldCancel_OnTokenCancellation()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            int counter = 0;
            try
            {
                RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                         () =>
                         {
                             counter++;
                             if (counter == 1)
                             {
                                 cancellationTokenSource.Cancel();
                                 throw new NotFoundException();
                             }

                             return Task.FromResult(this.foundResponse);
                         },
                         () => request,
                         this.policy,
                         cancellationTokenSource.Token
                        );

                Assert.Fail("BackoffRetryUtilityForServiceResponse failed to detect token cancellation.");
            }
            catch (OperationCanceledException)
            {
                // Expected exception
            }

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(1, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithAlternateMethod()
        {
            int counter = 0;
            TimeSpan timespan = TimeSpan.Zero;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None,
                 () =>
                 {
                     return Task.FromResult(this.foundResponse);
                 },
                 timespan
                );

            Assert.AreEqual(1, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(1, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithAlternateMethod_ThatThrows()
        {
            int counter = 0;
            TimeSpan timespan = TimeSpan.Zero;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         throw new NotFoundException();
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None,
                 () =>
                 {
                     throw new NotFoundException();
                 },
                 timespan
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(1, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        public async Task ShouldRetry_WithAlternateMethod_ThatReturnsFailedResponse()
        {
            int counter = 0;
            TimeSpan timespan = TimeSpan.Zero;
            RetriableResponse response = await RequestRetryUtility.ProcessRequestAsync<RetriableRequest, RetriableResponse>(
                 () =>
                 {
                     counter++;
                     if (counter == 1)
                     {
                         return Task.FromResult(this.notFoundResponse);
                     }

                     return Task.FromResult(this.foundResponse);
                 },
                 () => request,
                 this.policy,
                 CancellationToken.None,
                 () =>
                 {
                     return Task.FromResult(this.notFoundResponse);
                 },
                 timespan
                );

            Assert.AreEqual(2, counter);
            Assert.AreEqual(2, policy.ShouldRetryAsyncCalls);
            Assert.AreEqual(2, policy.OnBeforeSendRequestCalls);
        }

        [TestMethod]
        [Owner("kirankk")]
        [ExpectedException(typeof(WebException))]
        public async Task AvoidStackOverflowOnException()
        {
            await this.AvoidStackOverflowOnExceptionInternal(2000);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ShouldThrowServiceUnavailableException_IfLeaseNotFoundException()
        {
            int counter = 0;
            bool exceptionThrown = false;

            INameValueCollection headers = new DictionaryNameValueCollection();
            DocumentServiceRequest request = new (
                OperationType.Create,
                ResourceType.Document,
                "dbs/db/colls/coll1/docs/doc1",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);

            try
            {
                INameValueCollection responseHeaders = new NameValueCollectionWrapper();
                responseHeaders.Add(WFConstants.BackendHeaders.Status, ((int)StatusCodes.Gone).ToString());
                responseHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.LeaseNotFound).ToString());

                StoreResponse storeResponse = new()
                {
                    Headers = responseHeaders,
                    Status = (int)StatusCodes.Gone,
                };

                Func<GoneAndRetryRequestRetryPolicyContext, Task<StoreResponse>> funcDelegate = async (GoneAndRetryRequestRetryPolicyContext contextArguments) =>
                {
                    counter++;
                    request.Headers[HttpConstants.HttpHeaders.ClientRetryAttemptCount] = contextArguments.ClientRetryCount.ToString(CultureInfo.InvariantCulture);
                    request.Headers[HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest] = contextArguments.RemainingTimeInMsOnClientRequest.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
                    
                    return await Task.FromResult(storeResponse);
                };

                StoreResponse response = await RequestRetryUtility.ProcessRequestAsync<GoneAndRetryRequestRetryPolicyContext, DocumentServiceRequest, StoreResponse>(
                         funcDelegate,
                         () => request,
                         this.goneAndRetryPolicy,
                         CancellationToken.None
                        );
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex is ServiceUnavailableException);
            }

            Assert.AreEqual(1, counter);
            Assert.IsTrue(exceptionThrown, "Exception not thrown.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task ShouldThrowServiceUnavailableException_IfArchivalPartitionNotPresentException()
        {
            int counter = 0;
            bool exceptionThrown = false;

            INameValueCollection headers = new DictionaryNameValueCollection();
            DocumentServiceRequest request = new(
                OperationType.ReadFeed,
                ResourceType.Document,
                "dbs/db/colls/coll1/docs/doc1",
                null,
                AuthorizationTokenType.PrimaryMasterKey,
                headers);
            request.DisableArchivalPartitionNotFoundRetry = true;

            try
            {
                INameValueCollection responseHeaders = new NameValueCollectionWrapper();
                responseHeaders.Add(WFConstants.BackendHeaders.Status, ((int)StatusCodes.Gone).ToString());
                responseHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ArchivalPartitionNotPresent).ToString());

                StoreResponse storeResponse = new()
                {
                    Headers = responseHeaders,
                    Status = (int)StatusCodes.Gone,
                };

                GoneAndRetryWithRequestRetryPolicy<StoreResponse> goneAndRetryPolicy = new(
                    disableRetryWithPolicy: true,
                    waitTimeInSecondsOverride: 1);

                Func<GoneAndRetryRequestRetryPolicyContext, Task<StoreResponse>> funcDelegate = async (GoneAndRetryRequestRetryPolicyContext contextArguments) =>
                {
                    counter++;
                    request.Headers[HttpConstants.HttpHeaders.ClientRetryAttemptCount] = contextArguments.ClientRetryCount.ToString(CultureInfo.InvariantCulture);
                    request.Headers[HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest] = contextArguments.RemainingTimeInMsOnClientRequest.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

                    return await Task.FromResult(storeResponse);
                };

                StoreResponse response = await RequestRetryUtility.ProcessRequestAsync<GoneAndRetryRequestRetryPolicyContext, DocumentServiceRequest, StoreResponse>(
                         funcDelegate,
                         () => request,
                         goneAndRetryPolicy,
                         CancellationToken.None
                        );
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex is ServiceUnavailableException);
            }

            Assert.AreEqual(1, counter);
            Assert.IsTrue(exceptionThrown, "Exception not thrown.");
        }

        private async Task<IRetriableResponse> AvoidStackOverflowOnExceptionInternal(int count)
        {
            Func<Task<IRetriableResponse>> sendFunc = async () =>
            {
                if (count > 0)
                {
                    // Avoid StackOverflow for forward path, this test is just for Exception path
                    await Task.Yield();
                    return await this.AvoidStackOverflowOnExceptionInternal(count - 1);
                }

                throw new WebException("", WebExceptionStatus.ConnectFailure);
            };

            Mock<IRetriableResponse> retriableResponse = new Mock<IRetriableResponse>();

            Mock<IRequestRetryPolicy<int, IRetriableResponse>> retryPolicy = new Mock<IRequestRetryPolicy<int, IRetriableResponse>>();

            retryPolicy
                .Setup(r => r.ShouldRetryAsync(It.IsAny<int>(), It.IsAny<IRetriableResponse>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns((int request, IRetriableResponse response, Exception exception, CancellationToken cancellationToken) =>
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                });

            ShouldRetryResult shouldRetryResult;
            retryPolicy
                .Setup(r => r.TryHandleResponseSynchronously(It.IsAny<int>(), It.IsAny<IRetriableResponse>(), It.IsAny<Exception>(), out shouldRetryResult))
                .Returns(false);

            return await RequestRetryUtility.ProcessRequestAsync<int, IRetriableResponse>(
                executeAsync: sendFunc,
                prepareRequest: () => count,
                policy: retryPolicy.Object,
                cancellationToken: default(CancellationToken));
        }

        private class RetriableResponse : IRetriableResponse
        {
            public RetriableResponse(HttpStatusCode statusCode)
            {
                this.StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; private set; }

            public SubStatusCodes SubStatusCode => throw new NotImplementedException();
        }

        private class CustomContext
        {
            public bool HasChanged { get; set; }
        }

        private class RetriableRequest
        {
        }

        private class RetriableRetryPolicy : IRequestRetryPolicy<RetriableRequest, RetriableResponse>
        {
            public int OnBeforeSendRequestCalls { get; private set; }

            public int ShouldRetryAsyncCalls { get; private set; }

            public void OnBeforeSendRequest(RetriableRequest request)
            {
                this.OnBeforeSendRequestCalls++;
            }

            public bool TryHandleResponseSynchronously(RetriableRequest request, RetriableResponse response, Exception exception, out ShouldRetryResult shouldRetryResult)
            {
                shouldRetryResult = ShouldRetryResult.NoRetry(null);

                return response != null && (int)response.StatusCode < 400;
            }

            /// <summary>
            /// Should retry on 404 unless it hits the max retries.
            /// </summary>
            public Task<ShouldRetryResult> ShouldRetryAsync(RetriableRequest request, RetriableResponse response, Exception exception, CancellationToken cancellationToken)
            {
                this.ShouldRetryAsyncCalls++;
                if ((response != null && response.StatusCode == HttpStatusCode.NotFound)
                    || exception is NotFoundException)
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

        private class RetriableRetryPolicyWithCustomContext : IRequestRetryPolicy<CustomContext, RetriableRequest, RetriableResponse>
        {
            public int OnBeforeSendRequestCalls { get; private set; }

            public int ShouldRetryAsyncCalls { get; private set; }

            public CustomContext ExecuteContext { get; } = new CustomContext();

            public void OnBeforeSendRequest(RetriableRequest request)
            {
                this.OnBeforeSendRequestCalls++;
            }

            public bool TryHandleResponseSynchronously(RetriableRequest request, RetriableResponse response, Exception exception, out ShouldRetryResult shouldRetryResult)
            {
                shouldRetryResult = ShouldRetryResult.NoRetry(null);

                return response != null && (int)response.StatusCode < 400;
            }

            /// <summary>
            /// Should retry on 404 unless it hits the max retries.
            /// </summary>
            public Task<ShouldRetryResult> ShouldRetryAsync(RetriableRequest request, RetriableResponse response, Exception exception, CancellationToken cancellationToken)
            {
                this.ShouldRetryAsyncCalls++;
                this.ExecuteContext.HasChanged = true;
                if ((response != null && response.StatusCode == HttpStatusCode.NotFound)
                    || exception is NotFoundException)
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
