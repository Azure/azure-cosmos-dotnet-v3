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

        private readonly CosmosContainerCore container;
        private readonly CosmosQueryClient queryClient;
        private readonly CosmosJsonSerializer cosmosJsonSerializer;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;

        public CosmosLinqQuery(
           CosmosContainerCore container,
           CosmosJsonSerializer cosmosJsonSerializer,
           CosmosQueryClient queryClient,
           QueryRequestOptions cosmosQueryRequestOptions,
           Expression expression)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.cosmosJsonSerializer = cosmosJsonSerializer;
            this.queryClient = queryClient;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.expression = expression ?? Expression.Constant(this);
            this.queryProvider = new CosmosLinqQueryProvider(
              container,
              cosmosJsonSerializer,
              queryClient,
              cosmosQueryRequestOptions);
            this.correlatedActivityId = Guid.NewGuid();
        }

        public CosmosLinqQuery(
          CosmosContainerCore container,
          CosmosJsonSerializer cosmosJsonSerializer,
          CosmosQueryClient queryClient,
          QueryRequestOptions cosmosQueryRequestOptions)
            : this(
              container,
              cosmosJsonSerializer,
              queryClient,
              cosmosQueryRequestOptions,
              null)
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
                queryRequestOptions: this.cosmosQueryRequestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());
            return cosmosQueryExecution;
        }
    }
}
