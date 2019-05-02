//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryUnitTests
    {
        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentOnFailure()
        {
            (IList<DocumentQueryExecutionComponentBase> components, CosmosQueryResponse response) setupContext = await this.GetAllExecutionComponents();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                CosmosQueryResponse response = await component.DrainAsync(1, default(CancellationToken));
                Assert.AreEqual(setupContext.response, response);
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentCancellation()
        {
            (IList<DocumentQueryExecutionComponentBase> components, CosmosQueryResponse response) setupContext = await this.GetAllExecutionComponents();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                try
                {
                    CosmosQueryResponse response = await component.DrainAsync(1, cancellationTokenSource.Token);
                    Assert.Fail("cancellation token should have thrown an exception");
                }
                catch (OperationCanceledException e)
                {
                    Assert.IsNotNull(e.Message);
                }
            }
        }

        private async Task<(IList<DocumentQueryExecutionComponentBase> components, CosmosQueryResponse response)> GetAllExecutionComponents()
        {
            (Func<string, Task<IDocumentQueryExecutionComponent>> func, CosmosQueryResponse response) setupContext = this.SetupBaseContextToVerifyFailureScenario();

            List<DocumentQueryExecutionComponentBase> components = new List<DocumentQueryExecutionComponentBase>();
            List<AggregateOperator> operators = new List<AggregateOperator>()
            {
                AggregateOperator.Average,
                AggregateOperator.Count,
                AggregateOperator.Max,
                AggregateOperator.Min,
                AggregateOperator.Sum
            };

            components.Add(await AggregateDocumentQueryExecutionComponent.CreateAsync(
                operators.ToArray(),
                null,
                setupContext.func));

            components.Add(await DistinctDocumentQueryExecutionComponent.CreateAsync(
                     null,
                     setupContext.func,
                     DistinctQueryType.Ordered));

            components.Add(await SkipDocumentQueryExecutionComponent.CreateAsync(
                       5,
                       null,
                       setupContext.func));

            components.Add(await TakeDocumentQueryExecutionComponent.CreateLimitDocumentQueryExecutionComponentAsync(
                      5,
                      null,
                      setupContext.func));

            components.Add(await TakeDocumentQueryExecutionComponent.CreateTopDocumentQueryExecutionComponentAsync(
                       5,
                       null,
                       setupContext.func));

            return (components, setupContext.response);
        }

        private (Func<string, Task<IDocumentQueryExecutionComponent>>, CosmosQueryResponse) SetupBaseContextToVerifyFailureScenario()
        {
            Mock<CosmosQueryResponse> mockResponseMessage = new Mock<CosmosQueryResponse>();
            mockResponseMessage.Setup(x => x.IsSuccessStatusCode).Returns(false);
            // Throw an exception if the context accesses the CosmosElements array
            mockResponseMessage.Setup(x => x.CosmosElements).Throws(new ArgumentException("Context tried to access the Cosmos Elements of a failed response. Context should just return failed response."));

            Mock<IDocumentQueryExecutionComponent> baseContext = new Mock<IDocumentQueryExecutionComponent>();
            baseContext.Setup(x => x.DrainAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CosmosQueryResponse>(mockResponseMessage.Object));
            Func<string, Task<IDocumentQueryExecutionComponent>> callBack = x => Task.FromResult<IDocumentQueryExecutionComponent>(baseContext.Object);
            return (callBack, mockResponseMessage.Object);
        }
    }
}
