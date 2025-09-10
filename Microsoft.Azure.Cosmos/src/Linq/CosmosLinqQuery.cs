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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;
    using Debug = System.Diagnostics.Debug;

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
        private readonly CosmosLinqSerializerOptionsInternal linqSerializationOptions;

        public CosmosLinqQuery(
           ContainerInternal container,
           CosmosResponseFactoryInternal responseFactory,
           CosmosQueryClientCore queryClient,
           string continuationToken,
           QueryRequestOptions cosmosQueryRequestOptions,
           Expression expression,
           bool allowSynchronousQueryExecution,
           CosmosLinqSerializerOptionsInternal linqSerializationOptions = null)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
            this.continuationToken = continuationToken;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.Expression = expression ?? Expression.Constant(this);
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.correlatedActivityId = Guid.NewGuid();
            this.linqSerializationOptions = linqSerializationOptions;

            this.queryProvider = new CosmosLinqQueryProvider(
              container,
              responseFactory,
              queryClient,
              this.continuationToken,
              cosmosQueryRequestOptions,
              this.allowSynchronousQueryExecution,
              this.queryClient.OnExecuteScalarQueryCallback,
              this.linqSerializationOptions);
        }

        public CosmosLinqQuery(
          ContainerInternal container,
          CosmosResponseFactoryInternal responseFactory,
          CosmosQueryClientCore queryClient,
          string continuationToken,
          QueryRequestOptions cosmosQueryRequestOptions,
          bool allowSynchronousQueryExecution,
          CosmosLinqSerializerOptionsInternal linqSerializerOptions = null)
            : this(
              container,
              responseFactory,
              queryClient,
              continuationToken,
              cosmosQueryRequestOptions,
              null,
              allowSynchronousQueryExecution,
              linqSerializerOptions)
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

            FeedIterator<T> localFeedIterator = this.CreateFeedIterator(false, out ScalarOperationKind scalarOperationKind);
            Debug.Assert(
                scalarOperationKind == ScalarOperationKind.None,
                "CosmosLinqQuery Assert!",
                $"Unexpected client operation. Expected 'None', Received '{scalarOperationKind}'");

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
            SqlQuerySpec querySpec = DocumentQueryEvaluator.Evaluate(this.Expression, this.linqSerializationOptions).SqlQuerySpec;
            if (querySpec != null)
            {
                return JsonConvert.SerializeObject(querySpec);
            }

            return this.container.LinkUri.ToString();
        }

        public QueryDefinition ToQueryDefinition(IDictionary<object, string> parameters = null)
        {
            LinqQueryOperation linqQueryOperation = DocumentQueryEvaluator.Evaluate(this.Expression, this.linqSerializationOptions, parameters);
            ScalarOperationKind scalarOperationKind = linqQueryOperation.ScalarOperationKind;
            Debug.Assert(
                scalarOperationKind == ScalarOperationKind.None,
                "CosmosLinqQuery Assert!",
                $"Unexpected client operation. Expected 'None', Received '{scalarOperationKind}'");

            return QueryDefinition.CreateFromQuerySpec(linqQueryOperation.SqlQuerySpec);
        }

        public FeedIterator<T> ToFeedIterator()
        {
            FeedIterator<T> iterator = this.CreateFeedIterator(true, out ScalarOperationKind scalarOperationKind);
            Debug.Assert(
                scalarOperationKind == ScalarOperationKind.None,
                "CosmosLinqQuery Assert!",
                $"Unexpected client operation. Expected 'None', Received '{scalarOperationKind}'");

            return new FeedIteratorInlineCore<T>(iterator, this.container.ClientContext);
        }

        public FeedIterator ToStreamIterator()
        {
            FeedIterator iterator = this.CreateStreamIterator(true, out ScalarOperationKind scalarOperationKind);
            Debug.Assert(
                scalarOperationKind == ScalarOperationKind.None,
                "CosmosLinqQuery Assert!",
                $"Unexpected client operation. Expected 'None', Received '{scalarOperationKind}'");

            return new FeedIteratorInlineCore(iterator, this.container.ClientContext);
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

            FeedIteratorInlineCore<T> localFeedIterator = this.CreateFeedIterator(isContinuationExpected: false, scalarOperationKind: out ScalarOperationKind scalarOperationKind);
            Debug.Assert(
                scalarOperationKind == ScalarOperationKind.None,
                "CosmosLinqQuery Assert!",
                $"Unexpected client operation. Expected 'None', Received '{scalarOperationKind}'");

            ITrace rootTrace;
            using (rootTrace = Trace.GetRootTrace("Aggregate LINQ Operation"))
            {
                while (localFeedIterator.HasMoreResults)
                {
                    FeedResponse<T> response = await localFeedIterator.ReadNextAsync(rootTrace, cancellationToken);
                    headers.RequestCharge += response.RequestCharge;

                    // IndexMetrics only show up on first round trip
                    if (response.Headers.IndexUtilizationText != null)
                    {
                        headers.IndexUtilizationText = response.Headers.IndexUtilizationText;
                    }

                    if (response.Headers.ActivityId != null && headers.ActivityId == null)
                    {
                        headers.ActivityId = response.Headers.ActivityId;
                    }

                    result.AddRange(response);
                }
            }

            return new ItemResponse<T>(
                System.Net.HttpStatusCode.OK,
                headers,
                result.FirstOrDefault(),
                new CosmosTraceDiagnostics(rootTrace),
                null);
        }

        internal T ExecuteScalar()
        {
            FeedIteratorInlineCore<T> localFeedIterator = this.CreateFeedIterator(isContinuationExpected: false, out ScalarOperationKind scalarOperationKind);
            Headers headers = new Headers();

            List<T> result = new List<T>();
            ITrace rootTrace;
            using (rootTrace = Trace.GetRootTrace("Scalar LINQ Operation"))
            {
                while (localFeedIterator.HasMoreResults)
                {
                    FeedResponse<T> response = localFeedIterator.ReadNextAsync(rootTrace, cancellationToken: default).GetAwaiter().GetResult();
                    headers.RequestCharge += response.RequestCharge;
                    result.AddRange(response);
                }
            }

            switch (scalarOperationKind)
            {
                case ScalarOperationKind.FirstOrDefault:
                    return result.FirstOrDefault();

                // ExecuteScalar gets called when (sync) aggregates such as Max, Min, Sum are invoked on the IQueryable.
                // Since query fully supprots these operations, there is no client operation involved.
                // In these cases we return FirstOrDefault which handles empty/undefined/null result set from the backend.
                case ScalarOperationKind.None:
                    return result.SingleOrDefault();

                default:
                    throw new InvalidOperationException($"Unsupported scalar operation {scalarOperationKind}");
            }
        }

        private FeedIteratorInternal CreateStreamIterator(bool isContinuationExcpected, out ScalarOperationKind scalarOperationKind)
        {
            LinqQueryOperation linqQueryOperation = DocumentQueryEvaluator.Evaluate(this.Expression, this.linqSerializationOptions);
            scalarOperationKind = linqQueryOperation.ScalarOperationKind;

            return this.container.GetItemQueryStreamIteratorInternal(
                sqlQuerySpec: linqQueryOperation.SqlQuerySpec,
                isContinuationExcpected: isContinuationExcpected,
                continuationToken: this.continuationToken,
                feedRange: null,
                requestOptions: this.cosmosQueryRequestOptions);
        }

        private FeedIteratorInlineCore<T> CreateFeedIterator(bool isContinuationExpected, out ScalarOperationKind scalarOperationKind)
        {
            FeedIteratorInternal streamIterator = this.CreateStreamIterator(
                isContinuationExpected,
                out scalarOperationKind);
            return new FeedIteratorInlineCore<T>(new FeedIteratorCore<T>(
                streamIterator,
                this.responseFactory.CreateQueryFeedUserTypeResponse<T>),
                this.container.ClientContext);
        }
    }
}
