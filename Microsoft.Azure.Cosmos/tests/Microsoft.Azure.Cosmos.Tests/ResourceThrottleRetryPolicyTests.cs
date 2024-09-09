//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ResourceThrottleRetryPolicyTests
    {
        private readonly List<TraceListener> existingListener = new List<TraceListener>();
        private SourceSwitch existingSourceSwitch;

        [TestInitialize]
        public void CaptureCurrentTraceConfiguration()
        {
            foreach (TraceListener listener in DefaultTrace.TraceSource.Listeners)
            {
                this.existingListener.Add(listener);
            }

            DefaultTrace.TraceSource.Listeners.Clear();
            this.existingSourceSwitch = DefaultTrace.TraceSource.Switch;
        }

        [TestCleanup]
        public void ResetTraceConfiguration()
        {
            DefaultTrace.TraceSource.Listeners.Clear();
            foreach (TraceListener listener in this.existingListener)
            {
                DefaultTrace.TraceSource.Listeners.Add(listener);
            }

            DefaultTrace.TraceSource.Switch = this.existingSourceSwitch;
        }

        [TestMethod]
        public async Task DoesNotSerializeExceptionOnTracingDisabled()
        {
            Mock<IDocumentClientInternal> mockedClient = new ();
            GlobalEndpointManager endpointManager = new(mockedClient.Object, new ConnectionPolicy());

            // No listeners
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0, endpointManager);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(0, exception.ToStringCount, "Exception was serialized");
        }

        [TestMethod]
        public async Task DoesSerializeExceptionOnTracingEnabled()
        {
            Mock<IDocumentClientInternal> mockedClient = new ();
            GlobalEndpointManager endpointManager = new(mockedClient.Object, new ConnectionPolicy());

            // Let the default trace listener
            DefaultTrace.TraceSource.Switch = new SourceSwitch("ClientSwitch", "Error");
            DefaultTrace.TraceSource.Listeners.Add(new DefaultTraceListener());
            ResourceThrottleRetryPolicy policy = new ResourceThrottleRetryPolicy(0, endpointManager);
            CustomException exception = new CustomException();
            await policy.ShouldRetryAsync(exception, default);
            Assert.AreEqual(1, exception.ToStringCount, "Exception was not serialized");
        }

        private class CustomException : Exception
        {
            public int ToStringCount { get; private set; } = 0;

            public override string ToString()
            {
                ++this.ToStringCount;
                return string.Empty;
            }
        }
    }
}
