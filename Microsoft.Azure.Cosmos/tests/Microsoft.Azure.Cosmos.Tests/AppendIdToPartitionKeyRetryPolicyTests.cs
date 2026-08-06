//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class AppendIdToPartitionKeyRetryPolicyTests
    {
        [TestMethod]
        public async Task ExecuteWithRetryAsyncProvidesAttemptForReplayAndDisposesRetriedResponse()
        {
            ContainerProperties containerProperties = new ContainerProperties("container", "/pk");
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container
                .Setup(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<bool>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            using MemoryStream requestStream = new MemoryStream(new byte[] { 1, 2, 3 });
            TrackingStream retriedResponseContent = new TrackingStream();
            int attempts = 0;

            ResponseMessage response = await AppendIdToPartitionKeyRetryPolicy.ExecuteWithRetryAsync(
                container.Object,
                (attempt) =>
                {
                    Assert.AreEqual(attempt, attempts);
                    if (attempt > 0)
                    {
                        requestStream.Position = 0;
                    }

                    Assert.AreEqual(0, requestStream.Position);
                    requestStream.Position = requestStream.Length;
                    attempts++;

                    ResponseMessage attemptResponse = new ResponseMessage(
                        attempts == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
                    if (attempts == 1)
                    {
                        attemptResponse.Headers.SubStatusCode = (SubStatusCodes)ContainerPropertiesExtensions.AddIdToLastPartitionKeyPathSubStatusCode;
                        attemptResponse.Content = retriedResponseContent;
                    }

                    return Task.FromResult(attemptResponse);
                },
                canRetryAction: true,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(2, attempts);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(retriedResponseContent.IsDisposed);
            response.Dispose();
        }

        [TestMethod]
        public async Task ExecuteWithRetryAsyncReturnsFinalRetryableResponse()
        {
            ContainerProperties containerProperties = new ContainerProperties("container", "/pk");
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container
                .Setup(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<bool>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            TrackingStream finalResponseContent = new TrackingStream();
            int attempts = 0;

            ResponseMessage response = await AppendIdToPartitionKeyRetryPolicy.ExecuteWithRetryAsync(
                container.Object,
                (attempt) =>
                {
                    Assert.AreEqual(attempt, attempts);
                    attempts++;
                    ResponseMessage attemptResponse = new ResponseMessage(HttpStatusCode.BadRequest);
                    attemptResponse.Headers.SubStatusCode = (SubStatusCodes)ContainerPropertiesExtensions.AddIdToLastPartitionKeyPathSubStatusCode;
                    if (attempts == 2)
                    {
                        attemptResponse.Content = finalResponseContent;
                    }

                    return Task.FromResult(attemptResponse);
                },
                canRetryAction: true,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(2, attempts);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsFalse(finalResponseContent.IsDisposed);
            response.Dispose();
            Assert.IsTrue(finalResponseContent.IsDisposed);
        }

        private sealed class TrackingStream : MemoryStream
        {
            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
