//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary> 
    /// This is the entry point for LINQ query creation/execution, it generate query provider, implements IOrderedQueryable.
    /// </summary> 
    /// <seealso cref="CosmosLinqQueryProvider"/>  
    internal sealed class CosmosLinqQuery<T> : IDocumentQuery<T>, IOrderedQueryable<T>
    {
        private readonly Expression expression;
        private readonly CosmosLinqQueryProvider queryProvider;
        private readonly Guid correlatedActivityId;

        private readonly ContainerCore container;
        private readonly CosmosQueryClientCore queryClient;
        private readonly CosmosSerializer cosmosJsonSerializer;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;
        private readonly bool allowSynchronousQueryExecution = false;

        public CosmosLinqQuery(
           ContainerCore container,
           CosmosSerializer cosmosJsonSerializer,
           CosmosQueryClientCore queryClient,
           QueryRequestOptions cosmosQueryRequestOptions,
           Expression expression,
           bool allowSynchronousQueryExecution)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosJsonSerializer = cosmosJsonSerializer ?? throw new ArgumentNullException(nameof(cosmosJsonSerializer));
            this.queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.expression = expression ?? Expression.Constant(this);
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.queryProvider = new CosmosLinqQueryProvider(
              container,
              cosmosJsonSerializer,
              queryClient,
              cosmosQueryRequestOptions,
              this.allowSynchronousQueryExecution,
              this.queryClient.DocumentQueryClient.OnExecuteScalarQueryCallback);
            this.correlatedActivityId = Guid.NewGuid();
        }

        public CosmosLinqQuery(
          ContainerCore container,
          CosmosSerializer cosmosJsonSerializer,
          CosmosQueryClientCore queryClient,
          QueryRequestOptions cosmosQueryRequestOptions,
          bool allowSynchronousQueryExecution)
            : this(
              container,
              cosmosJsonSerializer,
              queryClient,
              cosmosQueryRequestOptions,
              null,
              allowSynchronousQueryExecution)
        {
        }

        public Type ElementType
        {
            get { return typeof(T); }
        }

        public Expression Expression
        {
            get { return this.expression; }
        }

        public IQueryProvider Provider
        {
            get { return this.queryProvider; }
        }

        public bool HasMoreResults => throw new NotImplementedException();

        /// <summary>
        /// Retrieves an object that can iterate through the individual results of the query.
        /// </summary>
        /// <remarks>
        /// This triggers a synchronous multi-page load.
        /// </remarks>
        /// <returns>IEnumerator</returns>
        public IEnumerator<T> GetEnumerator()
        {
            if (!this.allowSynchronousQueryExecution)
            {
                throw new NotSupportedException("To execute LINQ query please set " + nameof(allowSynchronousQueryExecution) + " true or" +
                    " use GetItemsQueryIterator to execute asynchronously");
            }

            using (CosmosQueryExecutionContext localQueryExecutionContext = CreateCosmosQueryExecutionContext())
            {
                while (!localQueryExecutionContext.IsDone)
                {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    QueryResponse cosmosQueryResponse = TaskHelper.InlineIfPossible(() => localQueryExecutionContext.ExecuteNextAsync(CancellationToken.None), null).GetAwaiter().GetResult();
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    QueryResponse<T> responseIterator = QueryResponse<T>.CreateResponse<T>(
                        cosmosQueryResponse: cosmosQueryResponse,
                        jsonSerializer: cosmosJsonSerializer,
                        hasMoreResults: !localQueryExecutionContext.IsDone);

                    foreach (T item in responseIterator)
                    {
                        yield return item;
                    }
                }
            }
        }

        /// <summary>
        /// Synchronous Multi-Page load
        /// </summary>
        /// <returns>IEnumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public override string ToString()
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.expression);
            if (querySpec != null)
            {
                return JsonConvert.SerializeObject(querySpec);
            }

            return container.LinkUri.ToString();
        }

        public string ToSqlQueryText()
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.expression);
            if (querySpec != null)
            {
                return (querySpec.QueryText);
            }

            return container.LinkUri.ToString();
        }

        public FeedIterator<T> ToFeedIterator()
        {
            return this.container.GetItemQueryIterator<T>(
                sqlQueryDefinition: new QueryDefinition(ToSqlQueryText()),
                continuationToken: null,
                requestOptions: this.cosmosQueryRequestOptions);
        }

        public void Dispose()
        {
            //NOTHING TO DISPOSE HERE
        }

        Task<DocumentFeedResponse<TResult>> IDocumentQuery<T>.ExecuteNextAsync<TResult>(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        Task<DocumentFeedResponse<dynamic>> IDocumentQuery<T>.ExecuteNextAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private CosmosQueryExecutionContext CreateCosmosQueryExecutionContext()
        {
            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: DocumentQueryEvaluator.Evaluate(expression),
                continuationToken: null,
                queryRequestOptions: this.cosmosQueryRequestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());
            return cosmosQueryExecution;
        }
    }
}
