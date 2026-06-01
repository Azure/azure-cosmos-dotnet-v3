//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class WebExceptionRetryPolicyTests
    {
        [TestMethod]
        public async Task ShouldRetryAsync_HonorsCancellationToken()
        {
            WebExceptionRetryPolicy policy = new WebExceptionRetryPolicy();

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            // Any retriable web exception would normally yield ShouldRetry=true with a backoff.
            // With a pre-cancelled token we must short-circuit and stop retrying.
            SocketException retriableException = new SocketException((int)SocketError.ConnectionReset);

            ShouldRetryResult result = await policy.ShouldRetryAsync(retriableException, cts.Token);

            Assert.IsFalse(result.ShouldRetry, "Cancelled token must short-circuit retry");
        }
    }
}
