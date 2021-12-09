//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DefaultTracingTests
    {
        [TestMethod]
        public async Task DefaultTracingDisabledByDefault()
        {
            TestTraceListener testTraceListener = new TestTraceListener();
            DefaultTrace.TraceSource.Listeners.Add(testTraceListener);

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("Test exception that won't be retried"));

            CosmosClient cosmosClient = new CosmosClient(
                "https://localhost:8081",
                Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                });

            try
            {
                await cosmosClient.GetDatabase("randomDb").ReadAsync();
                Assert.Fail("Should throw exception");
            }
            catch (InvalidOperationException ex)
            {
            }

            if (Debugger.IsAttached)
            {
                Assert.IsTrue(testTraceListener.IsTraceWritten);
            }
            else
            {
                Assert.IsFalse(testTraceListener.IsTraceWritten);
            }
        }

        [TestMethod]
        public async Task DefaultTracingEnableTest()
        {
            TestTraceListener testTraceListener = new TestTraceListener();
            DefaultTrace.TraceSource.Listeners.Add(testTraceListener);

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("Test exception that won't be retried"));

            CosmosClient.EnableDefaultTrace();

            CosmosClient cosmosClient = new CosmosClient(
                "https://localhost:8081",
                Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                });

            try
            {
                await cosmosClient.GetDatabase("randomDb").ReadAsync();
                Assert.Fail("Should throw exception");
            }
            catch (InvalidOperationException ex)
            {
            }

            Assert.IsTrue(testTraceListener.IsTraceWritten);
        }

        private class TestTraceListener : TraceListener
        {
            public bool IsTraceWritten = false;

            public override bool IsThreadSafe => true;
            public override void Write(string message)
            {
                this.IsTraceWritten = true;
            }

            public override void WriteLine(string message)
            {
                this.IsTraceWritten = true;
            }
        }
    }
}
