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
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    /// <summary>
    /// This is the entry point for LINQ query creation/execution, it generate query provider, implements IOrderedQueryable.
    /// </summary>
    /// <seealso cref="CosmosLinqQueryProvider"/>
    internal sealed class CosmosLinqQuery<T> : IDocumentQuery<T>, IOrderedQueryable<T>
    {
        private readonly CosmosLinqQueryProvider queryProvider;
        private readonly Guid correlatedActivityId;

        private readonly ContainerInternal container;
        private readonly CosmosQueryClientCore queryClient;
        private readonly CosmosResponseFactoryInternal responseFactory;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;
        private readonly bool allowSynchronousQueryExecution = false;
        private readonly string continuationToken;
        private readonly CosmosSerializationOptions serializationOptions;

        public CosmosLinqQuery(
           ContainerInternal container,
           CosmosResponseFactoryInternal responseFactory,
           CosmosQueryClientCore queryClient,
           string continuationToken,
           QueryRequestOptions cosmosQueryRequestOptions,
           Expression expression,
           bool allowSynchronousQueryExecution,
           CosmosSerializationOptions serializationOptions = null)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
            this.continuationToken = continuationToken;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.Expression = expression ?? Expression.Constant(this);
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.correlatedActivityId = Guid.NewGuid();
            this.serializationOptions = serializationOptions;

            this.queryProvider = new CosmosLinqQueryProvider(
              container,
              responseFactory,
              queryClient,
              this.continuationToken,
              cosmosQueryRequestOptions,
              this.allowSynchronousQueryExecution,
              this.queryClient.OnExecuteScalarQueryCallback,
              this.serializationOptions);
        }

        public CosmosLinqQuery(
          ContainerInternal container,
          CosmosResponseFactoryInternal responseFactory,
          CosmosQueryClientCore queryClient,
          string continuationToken,
          QueryRequestOptions cosmosQueryRequestOptions,
          bool allowSynchronousQueryExecution,
          CosmosSerializationOptions serializationOptions = null)
            : this(
              container,
              responseFactory,
              queryClient,
              continuationToken,
              cosmosQueryRequestOptions,
              null,
              allowSynchronousQueryExecution,
              serializationOptions)
        {
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

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
                    " use GetItemQueryIterator to execute asynchronously");
            }

            FeedIterator<T> localFeedIterator = this.CreateFeedIterator(false);
            while (localFeedIterator.HasMoreResults)
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                FeedResponse<T> items = TaskHelper.InlineIfPossible(() => localFeedIterator.ReadNextAsync(CancellationToken.None), null).GetAwaiter().GetResult();
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
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.Expression, this.serializationOptions);
            if (querySpec != null)
            {
                return JsonConvert.SerializeObject(querySpec);
            }

            return this.container.LinkUri.ToString();
        }

        public QueryDefinition ToQueryDefinition(IDictionary<object, string> parameters = null)
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.Expression, this.serializationOptions, parameters);
            return QueryDefinition.CreateFromQuerySpec(querySpec);
        }

        public FeedIterator<T> ToFeedIterator()
        {
            return new FeedIteratorInlineCore<T>(this.CreateFeedIterator(true));
        }

        public FeedIterator ToStreamIterator()
        {
            return new FeedIteratorInlineCore(this.CreateStreamIterator(true));
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

        internal async Task<Response<T>> AggregateResultAsync(CancellationToken cancellationToken = default)
        {
            List<T> result = new List<T>();
            Headers headers = new Headers();

            FeedIterator<T> localFeedIterator = this.CreateFeedIterator(isContinuationExpected: false);
            FeedIteratorInternal<T> localFeedIteratorInternal = (FeedIteratorInternal<T>)localFeedIterator;

            ITrace rootTrace;
            using (rootTrace = Trace.GetRootTrace("Aggregate LINQ Operation"))
            {
                while (localFeedIterator.HasMoreResults)
                {
                    FeedResponse<T> response = await localFeedIteratorInternal.ReadNextAsync(rootTrace, cancellationToken);
                    headers.RequestCharge += response.RequestCharge;
                    result.AddRange(response);
                }
            }

            return new ItemResponse<T>(
                System.Net.HttpStatusCode.OK,
                headers,
                result.FirstOrDefault(),
                rootTrace);
        }

        private FeedIteratorInternal CreateStreamIterator(bool isContinuationExcpected)
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.Expression, this.serializationOptions);

            return this.container.GetItemQueryStreamIteratorInternal(
                sqlQuerySpec: querySpec,
                isContinuationExcpected: isContinuationExcpected,
                continuationToken: this.continuationToken,
                feedRange: null,
                requestOptions: this.cosmosQueryRequestOptions);
        }

        private FeedIterator<T> CreateFeedIterator(bool isContinuationExpected)
        {
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.Expression, this.serializationOptions);

            FeedIteratorInternal streamIterator = this.CreateStreamIterator(isContinuationExpected);
            return new FeedIteratorInlineCore<T>(new FeedIteratorCore<T>(
                streamIterator,
                this.responseFactory.CreateQueryFeedUserTypeResponse<T>));
        }
    }
}
