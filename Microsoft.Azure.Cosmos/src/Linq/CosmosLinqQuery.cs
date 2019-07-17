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
        private readonly CosmosResponseFactory responseFactory;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;
        private readonly bool allowSynchronousQueryExecution = false;
        private readonly string continuationToken;

        public CosmosLinqQuery(
           ContainerCore container,
           CosmosResponseFactory responseFactory,
           CosmosQueryClientCore queryClient,
           string continuationToken,
           QueryRequestOptions cosmosQueryRequestOptions,
           Expression expression,
           bool allowSynchronousQueryExecution)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
            this.continuationToken = continuationToken;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.expression = expression ?? Expression.Constant(this);
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.queryProvider = new CosmosLinqQueryProvider(
              container,
              responseFactory,
              queryClient,
              this.continuationToken,
              cosmosQueryRequestOptions,
              this.allowSynchronousQueryExecution,
              this.queryClient.OnExecuteScalarQueryCallback);
            this.correlatedActivityId = Guid.NewGuid();
        }

        public CosmosLinqQuery(
          ContainerCore container,
          CosmosResponseFactory responseFactory,
          CosmosQueryClientCore queryClient,
          string continuationToken,
          QueryRequestOptions cosmosQueryRequestOptions,
          bool allowSynchronousQueryExecution)
            : this(
              container,
              responseFactory,
              queryClient,
              continuationToken,
              cosmosQueryRequestOptions,
              null,
              allowSynchronousQueryExecution)
        {
        }

        public Type ElementType => typeof(T);

        public Expression Expression => this.expression;

        public IQueryProvider Provider => this.queryProvider;

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
                throw new NotSupportedException("To execute LINQ query please set " + nameof(this.allowSynchronousQueryExecution) + " true or" +
                    " use GetItemsQueryIterator to execute asynchronously");
            }

            FeedIterator<T> localQueryExecutionContext = this.CreateFeedIterator(false);
            while (localQueryExecutionContext.HasMoreResults)
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                FeedResponse<T> items = TaskHelper.InlineIfPossible(() => localQueryExecutionContext.ReadNextAsync(CancellationToken.None), null).GetAwaiter().GetResult();
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

                foreach (T item in items)
                {
                    yield return item;
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

            return this.container.LinkUri.ToString();
        }

        public string ToSqlQueryText()
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.expression);
            return querySpec?.QueryText;
        }

        public FeedIterator<T> ToFeedIterator()
        {
            return CreateFeedIterator(true);
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

        private FeedIterator<T> CreateFeedIterator(bool isContinuationExcpected)
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.expression);

            FeedIterator streamIterator = this.container.GetItemQueryStreamIteratorInternal(
                sqlQuerySpec: querySpec,
                isContinuationExcpected: isContinuationExcpected,
                continuationToken: this.continuationToken,
                requestOptions: this.cosmosQueryRequestOptions);

            return new FeedIteratorCore<T>(
                streamIterator,
                this.responseFactory.CreateQueryFeedResponse<T>);
        }
    }
}
