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
        public void TestCopyConstructor()
        {
            FeedOptions fo = new FeedOptions();
            _ = new FeedOptions(fo);
        }

        [TestMethod]
        public async Task TestSupportedSerializationFormats()
        {
            FeedOptions feedOptions = new FeedOptions();
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
            Assert.AreEqual("JsonText,CosmosBinary", headers[HttpConstants.HttpHeaders.SupportedSerializationFormats]);

            feedOptions.SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow;
            headers = await testQueryExecutionContext.CreateCommonHeadersAsync(feedOptions);
            Assert.AreEqual("CosmosBinary, HybridRow", headers[HttpConstants.HttpHeaders.SupportedSerializationFormats]);
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