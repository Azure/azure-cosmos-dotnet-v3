//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Reflection;
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
            FieldInfo enabledField = typeof(CosmosClient).GetField("enableDefaultTrace", BindingFlags.Static | BindingFlags.NonPublic);

            bool ccosmosClientAlreadyEnabledTrace = (bool)enabledField.GetValue(null);
            if (ccosmosClientAlreadyEnabledTrace)
            {
                // A previous test enabled the traces. Reset back to the default state.
                enabledField.SetValue(null, false);
                DefaultTrace.TraceSource.Switch.Level = SourceLevels.Off;
                DefaultTrace.TraceSource.Listeners.Clear();
                FieldInfo defaultTraceIsListenerAdded = typeof(DefaultTrace).GetField("IsListenerAdded", BindingFlags.Static | BindingFlags.NonPublic);
                defaultTraceIsListenerAdded.SetValue(null, false);
            }

            Assert.AreEqual(SourceLevels.Off, DefaultTrace.TraceSource.Switch.Level, $"The trace is already enabled.");

            await this.ValidateTraceAsync(false);

            Assert.AreEqual(SourceLevels.Off, DefaultTrace.TraceSource.Switch.Level, $"The trace got enabled.");
        }

        [TestMethod]
        public async Task DefaultTracingEnableTest()
        {
            CosmosClient.EnableDefaultTrace();
            await this.ValidateTraceAsync(true);
        }

        private async Task ValidateTraceAsync(
            bool isTraceEnabled)
        {
            TestTraceListener testTraceListener = new TestTraceListener();
            DefaultTrace.TraceSource.Listeners.Add(testTraceListener);

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("Test exception that won't be retried"));

            using CosmosClient cosmosClient = new CosmosClient(
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

            if (isTraceEnabled)
            {
                Assert.IsTrue(testTraceListener.IsTraceWritten);
            }
            else
            {
                Assert.IsFalse(testTraceListener.IsTraceWritten);
            }
        }

        private class TestTraceListener : TraceListener
        {
            public bool IsTraceWritten = false;
            public bool WriteTraceToConsole = false;

            public override bool IsThreadSafe => true;
            public override void Write(string message)
            {
                this.IsTraceWritten = true;
                if (this.WriteTraceToConsole)
                {
                    Logger.LogLine("Trace.Write:" + message);
                }
            }

            public override void WriteLine(string message)
            {
                this.IsTraceWritten = true;
                if (this.WriteTraceToConsole)
                {
                    Logger.LogLine("Trace.WriteLine:" + message);
                }
            }
        }
    }
}
