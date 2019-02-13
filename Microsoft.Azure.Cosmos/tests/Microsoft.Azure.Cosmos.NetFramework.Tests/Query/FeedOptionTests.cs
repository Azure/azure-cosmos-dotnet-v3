//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Collections;
    using System.Collections.Specialized;
    using System.Linq.Expressions;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Internal;

    [TestClass]
    public class FeedOptionTests
    {
        [TestMethod]
        public async Task CheckConsistencyLevel()
        {
            FeedOptions fo = new FeedOptions();
            var dcClient = new Mock<IDocumentQueryClient>();
            Expression<Func<int, int>> randomFunc = x => x * 2;

            var cxt = new TestQueryExecutionContext(
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

            fo.ConsistencyLevel = ConsistencyLevel.Eventual;
            headers = await cxt.CreateCommonHeadersAsync(fo);
            Assert.AreEqual(ConsistencyLevel.Eventual.ToString(), headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
        }

        [TestMethod]
        public void TestCopyConstructor()
        {
            FeedOptions fo = new FeedOptions();
            FeedOptions f01 = new FeedOptions(fo);
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
                : base(client, resourceTypeEnum, resourceType, expression, feedOptions, resourceLink, getLazyFeedResponse, correlatedActivityId)
            {
            }

            public override void Dispose()
            {
                throw new NotImplementedException();
            }

            protected override Task<FeedResponse<dynamic>> ExecuteInternalAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }
    }
}
