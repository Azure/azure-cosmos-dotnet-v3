//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BulkExecutionRetryPolicyTests
    {
        [TestMethod]
        public async Task ShouldRetryAsync_Exception_HonorsCancellationToken()
        {
            ContainerInternal container = BulkExecutionRetryPolicyTests.CreateMockContainer();
            BulkExecutionRetryPolicy policy = new BulkExecutionRetryPolicy(
                container,
                OperationType.Create,
                nextRetryPolicy: null);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            CosmosException splitException = new CosmosException(
                "split",
                HttpStatusCode.Gone,
                subStatusCode: (int)SubStatusCodes.CompletingSplit,
                activityId: Guid.NewGuid().ToString(),
                requestCharge: 0);

            ShouldRetryResult result = await policy.ShouldRetryAsync(splitException, cts.Token);

            Assert.IsFalse(result.ShouldRetry, "Cancelled token must short-circuit retry");
        }

        [TestMethod]
        public async Task ShouldRetryAsync_ResponseMessage_HonorsCancellationToken()
        {
            ContainerInternal container = BulkExecutionRetryPolicyTests.CreateMockContainer();
            BulkExecutionRetryPolicy policy = new BulkExecutionRetryPolicy(
                container,
                OperationType.Create,
                nextRetryPolicy: null);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            ResponseMessage splitResponse = new ResponseMessage(HttpStatusCode.Gone);
            splitResponse.Headers.SubStatusCode = SubStatusCodes.CompletingSplit;

            ShouldRetryResult result = await policy.ShouldRetryAsync(splitResponse, cts.Token);

            Assert.IsFalse(result.ShouldRetry, "Cancelled token must short-circuit retry");
        }

        private static ContainerInternal CreateMockContainer()
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            return container.Object;
        }
    }
}
