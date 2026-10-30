//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        [DataRow(null, null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)SupportedSerializationFormats.JsonText, null, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, null, "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), null, "CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        [DataRow(null, (int)SupportedSerializationFeatures.None, "JsonText,CosmosBinary")]
        [DataRow(null, (int)SupportedSerializationFeatures.NumberArrays, "JsonText,CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.None, "CosmosBinary")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.UInt64, "CosmosBinary+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays), "CosmosBinary+Base64Strings+NumberArrays, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, (int)SupportedSerializationFeatures.UInt64, "JsonText")]
        public void QueryAdvertisesSupportedSerializationFeatures(
            int? formats,
            int? features,
            string expected)
        {
            QueryRequestOptions options = new QueryRequestOptions
            {
                SupportedSerializationFormats = (SupportedSerializationFormats?)formats,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };
            using RequestMessage request = new RequestMessage { OperationType = OperationType.Query };

            options.PopulateRequestOptions(request);

            Assert.AreEqual(expected, request.Headers.CosmosMessageHeaders.SupportedSerializationFormats);
        }

        [TestMethod]
        [DataRow(null, null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow(null, (int)SupportedSerializationFeatures.None, "JsonText,CosmosBinary")]
        [DataRow(null, (int)SupportedSerializationFeatures.NumberArrays, "JsonText,CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.None, "CosmosBinary")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.UInt64), "CosmosBinary+Base64Strings+UInt64, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, (int)SupportedSerializationFeatures.UInt64, "JsonText")]
        public void DistributedQueryAdvertisesSupportedSerializationFeatures(
            int? formats,
            int? features,
            string expected)
        {
            QueryRequestOptions options = new QueryRequestOptions
            {
                SupportedSerializationFormats = (SupportedSerializationFormats?)formats,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            CosmosDistributedQueryClient queryClient = new CosmosDistributedQueryClient(
                client.ClientContext,
                "dbs/database/colls/container",
                Guid.NewGuid(),
                options);
            using DocumentServiceRequest request = queryClient.CreateRequest(
                new SqlQuerySpec("SELECT * FROM c"),
                null,
                FeedRangeEpk.FullRange,
                null,
                QueryExecutionOptions.Default,
                NoOpTrace.Singleton);

            Assert.AreEqual(expected, request.Headers[HttpConstants.HttpHeaders.SupportedSerializationFormats]);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow((int)SupportedSerializationFeatures.None)]
        [DataRow((int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.UInt64))]
        public void ShallowCopyPreservesSupportedSerializationFeatures(int? features)
        {
            QueryRequestOptions options = new QueryRequestOptions
            {
                SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };

            QueryRequestOptions copy = (QueryRequestOptions)options.ShallowCopy();

            Assert.AreEqual(options.SupportedSerializationFormats, copy.SupportedSerializationFormats);
            Assert.AreEqual(options.SupportedSerializationFeatures, copy.SupportedSerializationFeatures);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow((int)SupportedSerializationFeatures.None)]
        [DataRow((int)SupportedSerializationFeatures.NumberArrays)]
        public void ReadFeedDoesNotAdvertiseBinaryFeatures(int? features)
        {
            QueryRequestOptions options = new QueryRequestOptions
            {
                SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };
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