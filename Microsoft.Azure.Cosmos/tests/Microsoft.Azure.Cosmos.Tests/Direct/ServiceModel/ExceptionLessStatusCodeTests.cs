//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExceptionLessStatusCodeTests
    {
        [TestMethod]
        [Owner("kirankk")]
        [DataRow(403, 0, true, true)]
        [DataRow(403, 3, true, true)]
        [DataRow(403, 1000, true, true)] // Any sub-status code
        [DataRow(403, 0, false, false)] // Non-V3 flows dsrUseStatusCodeFor403 = false
        [DataRow(403, 3, false, false)]
        [DataRow(403, 1000, false, false)]
        public void ForbiddenStatusCodeForFailuresTest(
            int statusCode,
            int subStatusCode,
            bool dsrUseStatusCodeFor403,
            bool isExceptionLessExppected)
        {
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                "/dbs",
                null,
                AuthorizationTokenType.AadToken,
                new NameValueCollectionWrapper());
            documentServiceRequest.UseStatusCodeFor403 = dsrUseStatusCodeFor403;

            bool isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);

            Assert.AreEqual(isExceptionLessExppected, isValidForExceptionLess);
        }

        [TestMethod]
        [Owner("kirankk")]
        [DataRow(403, 0)]
        [DataRow(403, 3)]
        [DataRow(403, 1000)]
        public void ForbiddenStatusCodeForFailuresIndependentTest(
            int statusCode,
            int subStatusCode)
        {
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                "/dbs",
                null,
                AuthorizationTokenType.AadToken,
                new NameValueCollectionWrapper());
            documentServiceRequest.UseStatusCodeFor403 = true; // 403 handling should be independent of others
            documentServiceRequest.UseStatusCodeForFailures = false;
            documentServiceRequest.UseStatusCodeFor429 = false;
            documentServiceRequest.UseStatusCodeForBadRequest = false;

            bool isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);
            Assert.IsTrue(isValidForExceptionLess);

            documentServiceRequest.UseStatusCodeFor403 = false; // 403 handling should be independent of others
            documentServiceRequest.UseStatusCodeForFailures = true;
            documentServiceRequest.UseStatusCodeFor429 = true;
            documentServiceRequest.UseStatusCodeForBadRequest = true;

            isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);
            Assert.IsFalse(isValidForExceptionLess);
        }

        [TestMethod]
        [Owner("kirankk")]
        [DataRow(404, 1002)]
        public void SessionNotFoundStatusCodeForFailuresIndependentTest(
            int statusCode,
            int subStatusCode)
        {
            {
                DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                    OperationType.Read,
                    ResourceType.Document,
                    "/dbs",
                    null,
                    AuthorizationTokenType.AadToken,
                    new NameValueCollectionWrapper());
                documentServiceRequest.UseStatusCodeFor4041002 = true; // 404-1002 handling should be independent of others
                documentServiceRequest.UseStatusCodeFor403 = false;
                documentServiceRequest.UseStatusCodeForFailures = false;
                documentServiceRequest.UseStatusCodeFor429 = false;
                documentServiceRequest.UseStatusCodeForBadRequest = false;

                bool isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);
                Assert.IsTrue(isValidForExceptionLess);

                documentServiceRequest.UseStatusCodeFor4041002 = false; // 404-1002 handling should be independent of others
                documentServiceRequest.UseStatusCodeFor403 = true;
                documentServiceRequest.UseStatusCodeForFailures = true;
                documentServiceRequest.UseStatusCodeFor429 = true;
                documentServiceRequest.UseStatusCodeForBadRequest = true;

                isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);
                Assert.IsFalse(isValidForExceptionLess);
            }

            {
                DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                    OperationType.Read,
                    ResourceType.Database,
                    "/dbs",
                    null,
                    AuthorizationTokenType.AadToken,
                    new NameValueCollectionWrapper());
                documentServiceRequest.UseStatusCodeFor4041002 = true; // 404-1002 handling should be independent of others
                documentServiceRequest.UseStatusCodeFor403 = true;
                documentServiceRequest.UseStatusCodeForFailures = true;
                documentServiceRequest.UseStatusCodeFor429 = true;
                documentServiceRequest.UseStatusCodeForBadRequest = true;

                bool isValidForExceptionLess = DocumentServiceRequestExtensions.IsValidStatusCodeForExceptionlessRetry(documentServiceRequest, statusCode, (SubStatusCodes)subStatusCode);
                Assert.IsFalse(isValidForExceptionLess);
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void AnyExceptionLessTest()
        {
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                "/dbs",
                null,
                AuthorizationTokenType.AadToken,
                new NameValueCollectionWrapper());

            documentServiceRequest.UseStatusCodeFor403 = false;
            documentServiceRequest.UseStatusCodeForFailures = false;
            documentServiceRequest.UseStatusCodeFor429 = false;
            documentServiceRequest.UseStatusCodeFor4041002 = false;
            documentServiceRequest.UseStatusCodeForBadRequest = false;

            Assert.IsFalse(documentServiceRequest.IsAnyExceptionLessEnabled());

            documentServiceRequest.UseStatusCodeFor403 = true;
            Assert.IsTrue(documentServiceRequest.IsAnyExceptionLessEnabled());
            documentServiceRequest.UseStatusCodeFor403 = false;

            documentServiceRequest.UseStatusCodeForFailures = true;
            Assert.IsTrue(documentServiceRequest.IsAnyExceptionLessEnabled());
            documentServiceRequest.UseStatusCodeForFailures = false;

            documentServiceRequest.UseStatusCodeFor429 = true;
            Assert.IsTrue(documentServiceRequest.IsAnyExceptionLessEnabled());
            documentServiceRequest.UseStatusCodeFor429 = false;

            documentServiceRequest.UseStatusCodeFor4041002 = true;
            Assert.IsTrue(documentServiceRequest.IsAnyExceptionLessEnabled());
            Assert.IsTrue(documentServiceRequest.IsValidRequestFor4041002());
            documentServiceRequest.UseStatusCodeFor4041002 = false;
        }

        [TestMethod]
        [Owner("kirankk")]
        [DataRow(false, false, false, false)]
        [DataRow(true, false, false, false)]
        [DataRow(false, true, false, false)]
        [DataRow(false, false, true, false)]
        [DataRow(false, false, false, true)]
        [DataRow(true, true, true, true)]
        public void ExceptionLessClonePropertiesTest(
            bool useStatusCodeFor403,
            bool useStatusCodeForFailures,
            bool useStatusCodeFor429,
            bool useStatusCodeFor4041002)
        {
            DocumentServiceRequest documentServiceRequest = new DocumentServiceRequest(
                OperationType.Read,
                ResourceType.Document,
                "/dbs",
                null,
                AuthorizationTokenType.AadToken,
                new NameValueCollectionWrapper());

            documentServiceRequest.UseStatusCodeFor403 = useStatusCodeFor403;
            documentServiceRequest.UseStatusCodeForFailures = useStatusCodeForFailures;
            documentServiceRequest.UseStatusCodeFor429 = useStatusCodeFor429;
            documentServiceRequest.UseStatusCodeFor4041002 = useStatusCodeFor4041002;

            DocumentServiceRequest clonedRequest = documentServiceRequest.Clone();

            Assert.AreEqual(documentServiceRequest.UseStatusCodeFor403, clonedRequest.UseStatusCodeFor403);
            Assert.AreEqual(documentServiceRequest.UseStatusCodeForFailures, clonedRequest.UseStatusCodeForFailures);
            Assert.AreEqual(documentServiceRequest.UseStatusCodeFor429, clonedRequest.UseStatusCodeFor429);
            Assert.AreEqual(documentServiceRequest.UseStatusCodeFor4041002, clonedRequest.UseStatusCodeFor4041002);
        }
    }
}