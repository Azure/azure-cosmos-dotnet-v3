//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System.Linq.Expressions;
    using System.Threading;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Cosmos.CosmosElements;

    [TestClass]
    public class FeedOptionTests
    {
        // Devnote: Query should be fixed
        //[TestMethod]
        //public async Task CheckConsistencyLevel()
        //{
        //    FeedOptions fo = new FeedOptions();
        //    var dcClient = new Mock<IDocumentQueryClient>();
        //    Expression<Func<int, int>> randomFunc = x => x * 2;

        //    var cxt = new TestQueryExecutionContext(
        //        dcClient.Object,
        //        ResourceType.Document,
        //        typeof(TestQueryExecutionContext),
        //        randomFunc,
        //        fo,
        //        string.Empty,
        //        true, Guid.NewGuid());

        //    dcClient.Setup(e => e.GetDefaultConsistencyLevelAsync()).Returns(Task.FromResult(ConsistencyLevel.BoundedStaleness));

        //    INameValueCollection headers = await cxt.CreateCommonHeadersAsync(fo);
        //    Assert.AreEqual(null, headers[HttpConstants.HttpHeaders.ConsistencyLevel]);

        //    fo.ConsistencyLevel = Cosmos.ConsistencyLevel.Eventual;
        //    headers = await cxt.CreateCommonHeadersAsync(fo);
        //    Assert.AreEqual(ConsistencyLevel.Eventual.ToString(), headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
        //}

        [TestMethod]
        public void TestCopyConstructor()
        {
            FeedOptions fo = new FeedOptions();
            FeedOptions f01 = new FeedOptions(fo);
        }
    }
}
