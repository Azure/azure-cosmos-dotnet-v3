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
        public void DefaultTracingEnableTest()
        {
            // Access cosmos client to cause the static constructor to get called
            Assert.IsTrue(CosmosClient.numberOfClientsCreated >= 0);
            Assert.IsTrue(CosmosClient.NumberOfActiveClients >= 0);

            if (!Debugger.IsAttached)
            {
                Assert.IsFalse(this.DefaultTraceHasDefaultTraceListener());
                DefaultTrace.TraceSource.Listeners.Add(new DefaultTraceListener());
            }

            Assert.IsTrue(this.DefaultTraceHasDefaultTraceListener());
            typeof(CosmosClient).GetMethod("RemoveDefaultTraceListener", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            //CosmosClient.RemoveDefaultTraceListener();
            Assert.IsFalse(this.DefaultTraceHasDefaultTraceListener());
        }

        private bool DefaultTraceHasDefaultTraceListener()
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