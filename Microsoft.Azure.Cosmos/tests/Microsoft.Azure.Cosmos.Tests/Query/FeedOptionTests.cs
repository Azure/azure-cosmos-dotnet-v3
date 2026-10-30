//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Moq;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FeedOptionTests
    {
        [TestMethod]
        public async Task CheckConsistencyLevel()
        {
            FeedOptions fo = new FeedOptions();
            Mock<IDocumentQueryClient> dcClient = new Mock<IDocumentQueryClient>();
            Expression<Func<int, int>> randomFunc = x => x * 2;

            TestQueryExecutionContext cxt = new TestQueryExecutionContext(
                dcClient.Object,
                ResourceType.Document,
                typeof(TestQueryExecutionContext),
                randomFunc,
                fo,
                string.Empty,
                true, Guid.NewGuid());

            dcClient.Setup(e => e.GetDefaultConsistencyLevelAsync()).Returns(Task.FromResult(ConsistencyLevel.BoundedStaleness));

            INameValueCollection headers = await cxt.CreateCommonHeadersAsync(fo);
            Assert.AreEqual(null, headers[HttpConstants.HttpHeaders.ConsistencyLevel]);

            fo.ConsistencyLevel = Cosmos.ConsistencyLevel.Eventual;
            headers = await cxt.CreateCommonHeadersAsync(fo);
            Assert.AreEqual(ConsistencyLevel.Eventual.ToString(), headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow((int)SupportedSerializationFeatures.None)]
        [DataRow((int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.UInt64))]
        public void TestCopyConstructor(int? features)
        {
            FeedOptions options = new FeedOptions
            {
                SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };

            FeedOptions copy = new FeedOptions(options);

            Assert.AreEqual(options.SupportedSerializationFormats, copy.SupportedSerializationFormats);
            Assert.AreEqual(options.SupportedSerializationFeatures, copy.SupportedSerializationFeatures);
        }

        [TestMethod]
        [DataRow(null, null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), null, "CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, null, "JsonText")]
        [DataRow(null, (int)SupportedSerializationFeatures.None, "JsonText,CosmosBinary")]
        [DataRow(null, (int)SupportedSerializationFeatures.NumberArrays, "JsonText,CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.None, "CosmosBinary")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.UInt64, "CosmosBinary+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays), "CosmosBinary+Base64Strings+NumberArrays, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, (int)SupportedSerializationFeatures.UInt64, "JsonText")]
        public async Task TestSupportedSerializationFormats(int? formats, int? features, string expected)
        {
            FeedOptions feedOptions = new FeedOptions
            {
                SupportedSerializationFormats = (SupportedSerializationFormats?)formats,
                SupportedSerializationFeatures = (SupportedSerializationFeatures?)features,
            };
            Mock<IDocumentQueryClient> client = new Mock<IDocumentQueryClient>(MockBehavior.Strict);
            client.Setup(x => x.GetDefaultConsistencyLevelAsync()).Returns(Task.FromResult(ConsistencyLevel.BoundedStaleness));
            client.Setup(x => x.GetDesiredConsistencyLevelAsync()).Returns(Task.FromResult<ConsistencyLevel?>(ConsistencyLevel.BoundedStaleness));
            Expression<Func<int, int>> randomFunc = x => x * 2;

            TestQueryExecutionContext testQueryExecutionContext = new TestQueryExecutionContext(
                client.Object,
                ResourceType.Document,
                typeof(TestQueryExecutionContext),
                randomFunc,
                feedOptions,
                string.Empty,
                true, Guid.NewGuid());
            INameValueCollection headers = await testQueryExecutionContext.CreateCommonHeadersAsync(feedOptions);
            Assert.AreEqual(expected, headers[HttpConstants.HttpHeaders.SupportedSerializationFormats]);
        }

        internal class TestQueryExecutionContext : DocumentQueryExecutionContextBase
        {
            public TestQueryExecutionContext(
                IDocumentQueryClient client,
                ResourceType resourceTypeEnum,
                Type resourceType,
                Expression expression,
                FeedOptions feedOptions,
                string resourceLink,
                bool getLazyFeedResponse,
                Guid correlatedActivityId)
                : base(new DocumentQueryExecutionContextBase.InitParams(
                    client,
                    resourceTypeEnum,
                    resourceType,
                    expression,
                    feedOptions,
                    resourceLink,
                    getLazyFeedResponse,
                    correlatedActivityId))
            {
            }

            public override void Dispose()
            {
                throw new NotImplementedException();
            }

            protected override Task<DocumentFeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }
    }
}