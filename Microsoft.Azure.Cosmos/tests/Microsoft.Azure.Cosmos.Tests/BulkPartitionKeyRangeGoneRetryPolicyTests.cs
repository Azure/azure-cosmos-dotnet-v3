//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BulkPartitionKeyRangeGoneRetryPolicyTests
    {
        [TestMethod]
        public async Task NotRetryOnSuccess()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));

            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.OK, new CosmosDiagnosticsCore());
            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(result.ToResponseMessage(), default(CancellationToken));
            Assert.IsFalse(shouldRetryResult.ShouldRetry);            
        }

        [TestMethod]
        public async Task RetriesOn429()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));

            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult((HttpStatusCode)StatusCodes.TooManyRequests, new CosmosDiagnosticsCore());
            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(result.ToResponseMessage(), default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }

        [TestMethod]
        public async Task RetriesOnSplits()
        {
            IDocumentClientRetryPolicy retryPolicy = new BulkPartitionKeyRangeGoneRetryPolicy(
                new ResourceThrottleRetryPolicy(1));

            TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(HttpStatusCode.Gone, new CosmosDiagnosticsCore()) { SubStatusCode = SubStatusCodes.PartitionKeyRangeGone };
            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(result.ToResponseMessage(), default(CancellationToken));
            Assert.IsTrue(shouldRetryResult.ShouldRetry);
        }
    }
}
