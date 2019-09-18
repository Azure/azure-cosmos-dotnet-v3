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
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryUnitTests
    {
        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentOnFailure()
        {
            (IList<DocumentQueryExecutionComponentBase> components, QueryResponseCore response) setupContext = await this.GetAllExecutionComponents();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                QueryResponseCore response = await component.DrainAsync(1, default(CancellationToken));
                Assert.AreEqual(setupContext.response, response);
            }
        }

        [TestMethod]
        public async Task TestCosmosQueryExecutionComponentCancellation()
        {
            (IList<DocumentQueryExecutionComponentBase> components, QueryResponseCore response) setupContext = await this.GetAllExecutionComponents();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            foreach (DocumentQueryExecutionComponentBase component in setupContext.components)
            {
                try
                {
                    QueryResponseCore response = await component.DrainAsync(1, cancellationTokenSource.Token);
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
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                Properties = new Dictionary<string, object>()
            {
                {"x-ms-query-partitionkey-definition", partitionKeyDefinition }
            }
            };

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(@"select * from t where t.something = 42 ");
            bool allowNonValueAggregateQuery = true;
            bool isContinuationExpected = true;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationtoken = cancellationTokenSource.Token;

            Mock<CosmosQueryClient> client = new Mock<CosmosQueryClient>();
            client.Setup(x => x.GetCachedContainerQueryPropertiesAsync(It.IsAny<Uri>(), It.IsAny<Cosmos.PartitionKey?>(), cancellationtoken)).Returns(Task.FromResult(new ContainerQueryProperties("mockContainer", null, partitionKeyDefinition)));
            client.Setup(x => x.ByPassQueryParsing()).Returns(false);
            client.Setup(x => x.GetPartitionedQueryExecutionInfoAsync(
                sqlQuerySpec,
                partitionKeyDefinition,
                true,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                false, // has logical partition key
                cancellationtoken)).Throws(new InvalidOperationException("Verified that the PartitionKeyDefinition was correctly set. Cancel the rest of the query"));

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters()
            {
                SqlQuerySpec = sqlQuerySpec,
                InitialUserContinuationToken = null,
                MaxBufferedItemCount = queryRequestOptions?.MaxBufferedItemCount,
                MaxConcurrency = queryRequestOptions?.MaxConcurrency,
                MaxItemCount = queryRequestOptions?.MaxItemCount,
                PartitionKey = queryRequestOptions?.PartitionKey,
                Properties = queryRequestOptions?.Properties
            };

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContextCore(
                client: client.Object,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                resourceLink: new Uri("dbs/mockdb/colls/mockColl", UriKind.Relative),
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                enableGroupBy: true,
                correlatedActivityId: new Guid("221FC86C-1825-4284-B10E-A6029652CCA6"));

            CosmosQueryExecutionContextFactory factory = new CosmosQueryExecutionContextFactory(
                cosmosQueryContext: cosmosQueryContext,
                inputParameters: inputParameters);

            await factory.ExecuteNextAsync(cancellationtoken);
        }

        private async Task<(IList<DocumentQueryExecutionComponentBase> components, QueryResponseCore response)> GetAllExecutionComponents()
        {
            (Func<string, Task<IDocumentQueryExecutionComponent>> func, QueryResponseCore response) setupContext = this.SetupBaseContextToVerifyFailureScenario();

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

        private (Func<string, Task<IDocumentQueryExecutionComponent>>, QueryResponseCore) SetupBaseContextToVerifyFailureScenario()
        {
            QueryResponseCore failure = QueryResponseCore.CreateFailure(
                System.Net.HttpStatusCode.Unauthorized,
                SubStatusCodes.PartitionKeyMismatch,
                "Random error message",
                42.89,
                "TestActivityId",
                null,
                null);

            Mock<IDocumentQueryExecutionComponent> baseContext = new Mock<IDocumentQueryExecutionComponent>();
            baseContext.Setup(x => x.DrainAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<QueryResponseCore>(failure));
            Func<string, Task<IDocumentQueryExecutionComponent>> callBack = x => Task.FromResult<IDocumentQueryExecutionComponent>(baseContext.Object);
            return (callBack, failure);
        }
    }
}