//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryUnitTests
    {
        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentOnFailure()
        {
            (IList<DocumentQueryExecutionComponentBase> components, QueryResponse response) setupContext = await this.GetAllExecutionComponents();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                QueryResponse response = await component.DrainAsync(1, default(CancellationToken));
                Assert.AreEqual(setupContext.response, response);
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentCancellation()
        {
            (IList<DocumentQueryExecutionComponentBase> components, QueryResponse response) setupContext = await this.GetAllExecutionComponents();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                try
                {
                    QueryResponse response = await component.DrainAsync(1, cancellationTokenSource.Token);
                    Assert.Fail("cancellation token should have thrown an exception");
                }
                catch (OperationCanceledException e)
                {
                    Assert.IsNotNull(e.Message);
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task TestCosmosQueryPartitionKeyDefinition()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            queryRequestOptions.Properties = new Dictionary<string, object>()
            {
                {"x-ms-query-partitionkey-definition", partitionKeyDefinition }
            };

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(@"select * from t where t.something = 42 ");
            bool allowNonValueAggregateQuery = true;
            bool isContinuationExpected = true;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationtoken = cancellationTokenSource.Token;

            Mock<CosmosQueryClient> client = new Mock<CosmosQueryClient>();
            client.Setup(x => x.GetCachedContainerPropertiesAsync(cancellationtoken)).Returns(Task.FromResult(new ContainerProperties("mockContainer", "/pk")));
            client.Setup(x => x.ByPassQueryParsing()).Returns(false);
            client.Setup(x => x.GetPartitionedQueryExecutionInfoAsync(
                sqlQuerySpec,
                partitionKeyDefinition,
                true,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                false, // has logical partition key
                cancellationtoken)).Throws(new InvalidOperationException("Verified that the PartitionKeyDefinition was correctly set. Cancel the rest of the query"));

            CosmosQueryExecutionContextFactory factory = new CosmosQueryExecutionContextFactory(
                client: client.Object,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: null,
                queryRequestOptions: queryRequestOptions,
                resourceLink: new Uri("dbs/mockdb/colls/mockColl", UriKind.Relative),
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: new Guid("221FC86C-1825-4284-B10E-A6029652CCA6"));

            await factory.ReadNextAsync(cancellationtoken);
        }

        private async Task<(IList<DocumentQueryExecutionComponentBase> components, QueryResponse response)> GetAllExecutionComponents()
        {
            (Func<string, Task<IDocumentQueryExecutionComponent>> func, QueryResponse response) setupContext = this.SetupBaseContextToVerifyFailureScenario();

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
                new Dictionary<string, AggregateOperator?>()
                {
                    { "test", AggregateOperator.Count }
                },
                false,
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

        private (Func<string, Task<IDocumentQueryExecutionComponent>>, QueryResponse) SetupBaseContextToVerifyFailureScenario()
        {
            Mock<QueryResponse> mockResponseMessage = new Mock<QueryResponse>();
            mockResponseMessage.Setup(x => x.IsSuccessStatusCode).Returns(false);
            // Throw an exception if the context accesses the CosmosElements array
            mockResponseMessage.Setup(x => x.CosmosElements).Throws(new ArgumentException("Context tried to access the Cosmos Elements of a failed response. Context should just return failed response."));

            Mock<IDocumentQueryExecutionComponent> baseContext = new Mock<IDocumentQueryExecutionComponent>();
            baseContext.Setup(x => x.DrainAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<QueryResponse>(mockResponseMessage.Object));
            Func<string, Task<IDocumentQueryExecutionComponent>> callBack = x => Task.FromResult<IDocumentQueryExecutionComponent>(baseContext.Object);
            return (callBack, mockResponseMessage.Object);
        }
    }
}
