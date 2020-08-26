//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TransientHttpClientRetryPolicyTests
    {
        [TestMethod]
        public async Task NoRetryOnHttpRequestException()
        {
            HttpMethod HttpMethodFunc() => HttpMethod.Get;

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                HttpMethodFunc,
                TimeSpan.FromSeconds(10));

            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new HttpRequestException(), default(CancellationToken));
            Assert.IsFalse(shouldRetryResult.ShouldRetry);            
        }

        [TestMethod]
        public async Task NoRetriesOnUserCancelledException()
        {
            HttpMethod HttpMethodFunc() => HttpMethod.Get;

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                HttpMethodFunc,
                TimeSpan.FromSeconds(10));


            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            cancellationTokenSource.Cancel();
            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new OperationCanceledException(), cancellationToken);
            Assert.IsFalse(shouldRetryResult.ShouldRetry); 
        }

        [TestMethod]
        public async Task RetriesOnWebException()
        {
            HttpMethod httpMethod = HttpMethod.Get;
            HttpMethod HttpMethodFunc() => httpMethod;

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                HttpMethodFunc,
                TimeSpan.FromSeconds(10));

            List<HttpMethod> httpMethods = new List<HttpMethod>()
            {
                HttpMethod.Get,
                HttpMethod.Delete,
                HttpMethod.Patch,
                HttpMethod.Post,
                HttpMethod.Put
            };

            foreach (HttpMethod method in httpMethods)
            {
                httpMethod = method;
                ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new WebException("Test", WebExceptionStatus.ConnectFailure), default(CancellationToken));
                Assert.IsTrue(shouldRetryResult.ShouldRetry, method.ToString()); 
            }
        }


        [TestMethod]
        public async Task RetriesOnOperationCancelException()
        {
            HttpMethod HttpMethodFunc() => HttpMethod.Get;

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                HttpMethodFunc,
                TimeSpan.FromSeconds(10));

            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new OperationCanceledException(), default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry); 
        }
    }
}
