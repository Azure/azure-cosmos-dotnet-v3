//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="Microsoft.Azure.Documents.DocumentServiceRequest"/>
    /// </summary>
    [TestClass]
    public class DocumentServiceRequestTests
    {
        [TestMethod]
        [Owner("grander")]
        public void TestIsChangeFeedRequest_False_When_A_IM_Header_Missing()
        {
            DocumentServiceRequest documentServiceRequest = this.CreateDocumentServiceRequest(headersCollections: null);
            Assert.IsFalse(documentServiceRequest.IsChangeFeedRequest);
        }

        [TestMethod]
        [Owner("grander")]
        public void TestIsChangeFeedRequest_True_When_A_IM_Header_NonEmpty()
        {
            NameValueCollectionWrapper headerCollection = new NameValueCollectionWrapper
            {
                { HttpConstants.HttpHeaders.A_IM, "Incremental" }
            };
            DocumentServiceRequest documentServiceRequest = this.CreateDocumentServiceRequest(headerCollection);

            Assert.IsTrue(documentServiceRequest.IsChangeFeedRequest);
        }

        [TestMethod]
        [Owner("grander")]
        public void TestIsChangeFeedRequest_False_When_A_IM_Header_Empty()
        {
            NameValueCollectionWrapper headerCollection = new NameValueCollectionWrapper
            {
                { HttpConstants.HttpHeaders.A_IM, "" }
            };
            DocumentServiceRequest documentServiceRequest = this.CreateDocumentServiceRequest(headerCollection);
            Assert.IsFalse(documentServiceRequest.IsChangeFeedRequest);
        }

        [TestMethod]
        [Owner("grander")]
        public void TestIsChangeFeedRequest_False_When_A_IM_Header_Whitespace()
        {
            NameValueCollectionWrapper headerCollection = new NameValueCollectionWrapper
            {
                { HttpConstants.HttpHeaders.A_IM, " " }
            };
            DocumentServiceRequest documentServiceRequest = this.CreateDocumentServiceRequest(headerCollection);
            Assert.IsFalse(documentServiceRequest.IsChangeFeedRequest);
        }

        private DocumentServiceRequest CreateDocumentServiceRequest(NameValueCollectionWrapper headersCollections)
        {
            return new DocumentServiceRequest(
                OperationType.ReadFeed,
                ResourceType.Document,
                PathsHelper.GetResourcePath(ResourceType.Document),
                body: null,
                AuthorizationTokenType.Invalid,
                headersCollections);
        }
    }
}