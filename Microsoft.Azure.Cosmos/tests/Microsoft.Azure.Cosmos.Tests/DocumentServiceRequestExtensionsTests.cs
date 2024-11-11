//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="DocumentServiceRequestExtensions"/>.
    /// </summary>
    [TestClass]
    public class DocumentServiceRequestExtensionsTests
    {
        /// <summary>
        /// Tests to make sure IsValidStatusCodeForExceptionlessRetry works with UseStatusCodeForFailures
        /// </summary>
        [TestMethod]
        public void TestUseStatusCodeForFailures()
        {
            using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        OperationType.Query,
                        ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
            {
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.PreconditionFailed));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.Conflict));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.NotFound));

                request.UseStatusCodeForFailures = true;

                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)StatusCodes.TooManyRequests));
                Assert.IsTrue(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.PreconditionFailed));
                Assert.IsTrue(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.Conflict));
                Assert.IsTrue(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.NotFound));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.NotFound, SubStatusCodes.ReadSessionNotAvailable));
            }
        }

        /// <summary>
        /// Tests to make sure IsValidStatusCodeForExceptionlessRetry works with UseStatusCodeFor429
        /// </summary>
        [TestMethod]
        public void TestUseStatusCodeFor429()
        {
            using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        OperationType.Query,
                        ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
            {
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)StatusCodes.TooManyRequests));

                request.UseStatusCodeFor429 = true;

                Assert.IsTrue(request.IsValidStatusCodeForExceptionlessRetry((int)StatusCodes.TooManyRequests));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.PreconditionFailed));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.Conflict));
                Assert.IsFalse(request.IsValidStatusCodeForExceptionlessRetry((int)HttpStatusCode.NotFound));
            }
        }
    }
}