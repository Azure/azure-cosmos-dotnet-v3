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
        public void DefaultTracingDisabledByDefault()
        {
            // Used to just force the CosmosClient static ctor to get called
            Assert.IsTrue(CosmosClient.numberOfClientsCreated >= 0);

            if (Debugger.IsAttached)
            {
                Assert.IsTrue(this.DefaultTraceHasDefaulTraceListener());
            }
            else
            {
                Assert.IsFalse(this.DefaultTraceHasDefaulTraceListener());
            }
        }

        [TestMethod]
        public void DefaultTracingEnableTest()
        {
            Assert.IsFalse(this.DefaultTraceHasDefaulTraceListener());
            CosmosClient.AddDefaultTraceListener();
            Assert.IsTrue(this.DefaultTraceHasDefaulTraceListener());
            CosmosClient.RemoveDefaultTraceListener();
            Assert.IsFalse(this.DefaultTraceHasDefaulTraceListener());
        }

        private bool DefaultTraceHasDefaulTraceListener()
        {
            if (DefaultTrace.TraceSource.Listeners.Count == 0)
            {
                return false;
            }

            foreach (TraceListener listener in DefaultTrace.TraceSource.Listeners)
            {
                if (listener is DefaultTraceListener)
                {
                    return true;
                }
            }

            DefaultTrace.TraceSource.Listeners.Clear();
            return false;
        }
    }
}
