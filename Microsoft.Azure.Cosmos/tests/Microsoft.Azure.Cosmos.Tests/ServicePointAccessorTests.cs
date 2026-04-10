//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ServicePointAccessorTests
    {
        private static readonly Uri uri = new Uri("https://localhost");

        [TestMethod]
        public void ServicePointAccessor_SetConnectionLimit()
        {
            int limit = 10;
            ServicePointAccessor accessor = ServicePointAccessor.FindServicePoint(ServicePointAccessorTests.uri);
            Assert.IsNotNull(accessor);
            accessor.ConnectionLimit = limit;
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(ServicePointAccessorTests.uri);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            Assert.AreEqual(limit, servicePoint.ConnectionLimit);
        }

        [TestMethod]
        public void ServicePointAccessor_DisableNagle()
        {
            ServicePointAccessor accessor = ServicePointAccessor.FindServicePoint(ServicePointAccessorTests.uri);
            Assert.IsNotNull(accessor);
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(ServicePointAccessorTests.uri);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            Assert.IsFalse(servicePoint.UseNagleAlgorithm);
        }
    }
}