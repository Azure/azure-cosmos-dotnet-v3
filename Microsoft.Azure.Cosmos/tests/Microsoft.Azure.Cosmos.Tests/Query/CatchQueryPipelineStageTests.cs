// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CatchQueryPipelineStageTests
    {
        [TestMethod]
        [Owner("flnarenj")]
        public async Task TestAggregateExceptionTimeoutAsync()
        {
            Mock<IQueryPipelineStage> throwingPipelineStage = new Mock<IQueryPipelineStage>();
            CancellationTokenSource cts = new CancellationTokenSource();

            throwingPipelineStage
                .Setup(p => p.MoveNextAsync())
                .Callback(() => cts.Cancel())
                .ThrowsAsync(new AggregateException(new OperationCanceledException()));

            CatchAllQueryPipelineStage catchAllQueryPipelineStage = new CatchAllQueryPipelineStage(throwingPipelineStage.Object, cts.Token);

            try
            {
                await catchAllQueryPipelineStage.MoveNextAsync();
                Assert.Fail("Expected exception.");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}