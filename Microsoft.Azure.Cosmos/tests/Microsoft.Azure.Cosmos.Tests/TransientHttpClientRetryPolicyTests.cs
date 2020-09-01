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
            HttpRequestMessage httpRequestMessageFunc() => new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081");

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                httpRequestMessageFunc,
                TimeSpan.FromSeconds(10),
                new CosmosDiagnosticsContextCore());

            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new HttpRequestException(), default(CancellationToken));
            Assert.IsFalse(shouldRetryResult.ShouldRetry);            
        }

        [TestMethod]
        public async Task NoRetriesOnUserCancelledException()
        {
            HttpRequestMessage httpRequestMessageFunc() => new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081");

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                httpRequestMessageFunc,
                TimeSpan.FromSeconds(10),
                new CosmosDiagnosticsContextCore());

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
            HttpRequestMessage httpRequestMessageFunc() => new HttpRequestMessage(httpMethod, "https://localhost:8081");

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                httpRequestMessageFunc,
                TimeSpan.FromSeconds(10),
                new CosmosDiagnosticsContextCore());

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
            HttpRequestMessage httpRequestMessageFunc() => new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081");

            IRetryPolicy retryPolicy = new TransientHttpClientRetryPolicy(
                httpRequestMessageFunc,
                TimeSpan.FromSeconds(10),
                new CosmosDiagnosticsContextCore());

            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(new OperationCanceledException(), default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry); 
        }
    }
}
