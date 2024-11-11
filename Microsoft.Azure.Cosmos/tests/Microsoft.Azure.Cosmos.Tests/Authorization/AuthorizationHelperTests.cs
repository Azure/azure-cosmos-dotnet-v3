//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.AuthorizationHelper;

    [TestClass]
    public class AuthorizationHelperTests
    {
        [TestMethod]
        public void TestGenerateAuthorizationTokenWithHashCoreDoesNotEncodeUrl()
        {
            Mock<INameValueCollection> mockHeaders = new Mock<INameValueCollection>();
            mockHeaders.SetupGet(h => h["x-ms-date"]).Returns(Rfc1123DateTimeCache.UtcNow());
            Mock<IComputeHash> hashHelperMock = new Mock<IComputeHash>();
            hashHelperMock.Setup(
                ch => ch.ComputeHash(It.IsAny<ArraySegment<byte>>()))
                .Returns(new byte[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 });

            string token = AuthorizationHelper.GenerateAuthorizationTokenWithHashCore(
                verb: "testVerb",
                resourceId: "dbs/V4lVAA==/colls/V4lVAMl0wuQ=/",
                resourceType: "colls",
                headers: mockHeaders.Object,
                stringHMACSHA256Helper: hashHelperMock.Object,
                out _);

            // Encoding.UTF8 string representation of the key byte array. (if it were URL encoded, it would end with "3d%3d" instead of "==").
            Assert.AreEqual("AgQGCAoMDhASFA==", token);
        }
    }
}