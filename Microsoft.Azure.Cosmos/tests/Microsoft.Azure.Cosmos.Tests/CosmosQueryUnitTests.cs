//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosQueryUnitTests
    {
        [TestMethod]
        public void VerifyNegativeCosmosQueryResponseStream()
        {
            string contianerRid = "mockContainerRid";
            string errorMessage = "TestErrorMessage";
            string activityId = "TestActivityId";
            double requestCharge = 42.42;
            CosmosDiagnosticsContext diagnostics = new CosmosDiagnosticsContextCore();
            CosmosException cosmosException = CosmosExceptionFactory.CreateBadRequestException(errorMessage, diagnosticsContext: diagnostics);

            diagnostics.GetOverallScope().Dispose();
            QueryResponse queryResponse = QueryResponse.CreateFailure(
                        statusCode: HttpStatusCode.NotFound,
                        cosmosException: cosmosException,
                        requestMessage: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            null,
                            null,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = requestCharge,
                            ActivityId = activityId
                        },
                        diagnostics: diagnostics);

            Assert.AreEqual(HttpStatusCode.NotFound, queryResponse.StatusCode);
            Assert.AreEqual(cosmosException.Message, queryResponse.ErrorMessage);
            Assert.AreEqual(requestCharge, queryResponse.Headers.RequestCharge);
            Assert.AreEqual(activityId, queryResponse.Headers.ActivityId);
            Assert.AreEqual(diagnostics, queryResponse.DiagnosticsContext);
            Assert.IsNull(queryResponse.Content);
        }

        [TestMethod]
        public void VerifyCosmosQueryResponseStream()
        {
            string contianerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: contianerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = response;

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: new CosmosDiagnosticsContextCore());

            using (Stream stream = queryResponse.Content)
            {
                using (Stream innerStream = queryResponse.Content)
                {
                    Assert.IsTrue(object.ReferenceEquals(stream, innerStream), "Content should return the same stream");
                }
            }
        }

        [TestMethod]
        public void VerifyItemQueryResponseResult()
        {
            string contianerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) factoryResponse = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: contianerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = factoryResponse.response;
            List<CosmosElement> cosmosElements = new List<CosmosElement>(responseCore.CosmosElements);

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: cosmosElements,
                        count: cosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            contianerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: new CosmosDiagnosticsContextCore());

            QueryResponse<ToDoItem> itemQueryResponse = QueryResponseMessageFactory.CreateQueryResponse<ToDoItem>(queryResponse);
            List<ToDoItem> resultItems = new List<ToDoItem>(itemQueryResponse.Resource);
            ToDoItemComparer comparer = new ToDoItemComparer();

            Assert.AreEqual(factoryResponse.items.Count, resultItems.Count);
            for (int i = 0; i < factoryResponse.items.Count; i++)
            {
                Assert.AreNotSame(factoryResponse.items[i], resultItems[i]);
                Assert.AreEqual(0, comparer.Compare(factoryResponse.items[i], resultItems[i]));
            }
        }

        [TestMethod]
        public void VerifyItemQueryResponseCosmosElements()
        {
            string containerRid = "mockContainerRid";
            (QueryResponseCore response, IList<ToDoItem> items) factoryResponse = QueryResponseMessageFactory.Create(
                       itemIdPrefix: $"TestPage",
                       continuationToken: "SomeContinuationToken",
                       collectionRid: containerRid,
                       itemCount: 100);

            QueryResponseCore responseCore = factoryResponse.response;
            List<CosmosElement> cosmosElements = new List<CosmosElement>(responseCore.CosmosElements);

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                        result: cosmosElements,
                        count: cosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        serializationOptions: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            ResourceType.Document,
                            containerRid)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        },
                        diagnostics: new CosmosDiagnosticsContextCore());

            QueryResponse<CosmosElement> itemQueryResponse = QueryResponseMessageFactory.CreateQueryResponse<CosmosElement>(queryResponse);
            List<CosmosElement> resultItems = new List<CosmosElement>(itemQueryResponse.Resource);

            Assert.AreEqual(cosmosElements.Count, resultItems.Count);
            for (int i = 0; i < cosmosElements.Count; i++)
            {
                Assert.AreSame(cosmosElements[i], resultItems[i]);
            }
        }

        [TestMethod]
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
            string exceptionMessage = "Verified that the PartitionKeyDefinition was correctly set. Cancel the rest of the query";
            client
                .Setup(x => x.GetCachedContainerQueryPropertiesAsync(It.IsAny<string>(), It.IsAny<Cosmos.PartitionKey?>(), cancellationtoken))
                .ReturnsAsync(new ContainerQueryProperties("mockContainer", null, partitionKeyDefinition));
            client
                .Setup(x => x.ByPassQueryParsing())
                .Returns(false);
            client
                .Setup(x => x.TryGetPartitionedQueryExecutionInfoAsync(
                    It.IsAny<SqlQuerySpec>(),
                    It.IsAny<PartitionKeyDefinition>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(
                    new InvalidOperationException(
                        exceptionMessage)));

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: null,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions?.MaxConcurrency,
                maxItemCount: queryRequestOptions?.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions?.MaxBufferedItemCount,
                partitionKey: queryRequestOptions?.PartitionKey,
                properties: queryRequestOptions?.Properties,
                partitionedQueryExecutionInfo: null,
                executionEnvironment: queryRequestOptions?.ExecutionEnvironment,
                returnResultsInDeterministicOrder: true,
                forcePassthrough: false,
                testInjections: queryRequestOptions?.TestSettings);

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContextCore(
                client: client.Object,
                queryRequestOptions: queryRequestOptions,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                resourceLink: "dbs/mockdb/colls/mockColl",
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                diagnosticsContext: new CosmosDiagnosticsContextCore(),
                correlatedActivityId: new Guid("221FC86C-1825-4284-B10E-A6029652CCA6"));

            IQueryPipelineStage pipelineStage = CosmosQueryExecutionContextFactory.Create(
                documentContainer: null,
                cosmosQueryContext,
                inputParameters);

            Assert.IsTrue(await pipelineStage.MoveNextAsync());
            TryCatch<QueryPage> tryGetPage = pipelineStage.Current;
            Assert.IsTrue(tryGetPage.Failed);
            Assert.AreEqual(HttpStatusCode.BadRequest, (tryGetPage.Exception as CosmosException).StatusCode);
            Assert.IsTrue(
                (tryGetPage.Exception as CosmosException).ToString().Contains(exceptionMessage),
                "response error message did not contain the proper substring.");
        }
    }
}