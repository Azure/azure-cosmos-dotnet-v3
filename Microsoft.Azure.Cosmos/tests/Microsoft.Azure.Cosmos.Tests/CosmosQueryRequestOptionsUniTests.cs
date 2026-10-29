//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        [DataRow(null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)SupportedSerializationFormats.JsonText, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), "CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        public void QueryAdvertisesSupportedSerializationFeatures(
            int? formats,
            string expected)
        {
            QueryRequestOptions options = new QueryRequestOptions { SupportedSerializationFormats = (SupportedSerializationFormats?)formats };
            using RequestMessage request = new RequestMessage { OperationType = OperationType.Query };

            options.PopulateRequestOptions(request);

            Assert.AreEqual(expected, request.Headers.CosmosMessageHeaders.SupportedSerializationFormats);
        }

        [TestMethod]
        public void ReadFeedDoesNotAdvertiseBinaryFeatures()
        {
            QueryRequestOptions options = new QueryRequestOptions();
            using RequestMessage request = new RequestMessage { OperationType = OperationType.ReadFeed };

            options.PopulateRequestOptions(request);

            Assert.IsNull(request.Headers.CosmosMessageHeaders.SupportedSerializationFormats);
        }

        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();

            RequestMessage testMessage = new RequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.ContinuationToken);
        }
    }
}