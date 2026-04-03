//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DnsDotSuffixHelperTests
    {
        [TestMethod]
        public void ToFqdnHostName_AppendsTrailingDot()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("myaccount.documents.azure.com");
            Assert.AreEqual("myaccount.documents.azure.com.", result);
        }

        [TestMethod]
        public void ToFqdnHostName_IdempotentWhenAlreadyDotSuffixed()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("myaccount.documents.azure.com.");
            Assert.AreEqual("myaccount.documents.azure.com.", result);
        }

        [TestMethod]
        public void ToFqdnHostName_SkipsIPv4Address()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("10.0.0.1");
            Assert.AreEqual("10.0.0.1", result);
        }

        [TestMethod]
        public void ToFqdnHostName_SkipsIPv6Address()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("::1");
            Assert.AreEqual("::1", result);
        }

        [TestMethod]
        public void ToFqdnHostName_SkipsIPv6FullAddress()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("2001:0db8:85a3:0000:0000:8a2e:0370:7334");
            Assert.AreEqual("2001:0db8:85a3:0000:0000:8a2e:0370:7334", result);
        }

        [TestMethod]
        public void ToFqdnHostName_ReturnsNullForNull()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ToFqdnHostName_ReturnsEmptyForEmpty()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName(string.Empty);
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ToFqdnHostName_AppendsTrailingDotToLocalhost()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("localhost");
            Assert.AreEqual("localhost.", result);
        }

        [TestMethod]
        public void ToFqdnHostName_AppendsTrailingDotToSingleLabel()
        {
            string result = DnsDotSuffixHelper.ToFqdnHostName("cosmosdb");
            Assert.AreEqual("cosmosdb.", result);
        }

        [TestMethod]
        public void CreateDnsResolutionFunction_ReturnsNonNullFunction()
        {
            Func<string, Task<IPAddress>> resolver = DnsDotSuffixHelper.CreateDnsResolutionFunction();
            Assert.IsNotNull(resolver);
        }
    }
}
