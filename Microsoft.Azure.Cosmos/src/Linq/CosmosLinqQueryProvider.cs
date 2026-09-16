//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serializer;

    /// <summary> 
    /// This class serve as LINQ query provider implementing IQueryProvider.
    /// </summary> 
    internal sealed class CosmosLinqQueryProvider : IQueryProvider
    {
        private readonly ContainerInternal container;
        private readonly CosmosQueryClientCore queryClient;
        private readonly CosmosResponseFactoryInternal responseFactory;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;
        private readonly bool allowSynchronousQueryExecution;
        private readonly Action<IQueryable> onExecuteScalarQueryCallback;
        private readonly string continuationToken;
        private readonly CosmosLinqSerializerOptionsInternal linqSerializerOptions;

        public CosmosLinqQueryProvider(
           ContainerInternal container,
           CosmosResponseFactoryInternal responseFactory,
           CosmosQueryClientCore queryClient,
           string continuationToken,
           QueryRequestOptions cosmosQueryRequestOptions,
           bool allowSynchronousQueryExecution,
           Action<IQueryable> onExecuteScalarQueryCallback = null,
           CosmosLinqSerializerOptionsInternal linqSerializerOptions = null)
        {
            this.container = container;
            this.responseFactory = responseFactory;
            this.queryClient = queryClient;
            this.continuationToken = continuationToken;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.onExecuteScalarQueryCallback = onExecuteScalarQueryCallback;
            this.linqSerializerOptions = linqSerializerOptions;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new CosmosLinqQuery<TElement>(
                this.container,
                this.responseFactory,
                this.queryClient,
                this.continuationToken,
                this.cosmosQueryRequestOptions,
                expression,
                this.allowSynchronousQueryExecution,
                this.linqSerializerOptions);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            // ISSUE-TODO-adityasa-2024/1/26 - Investigate if reflection usage can be removed.
            Type expressionType = TypeSystem.GetElementType(expression.Type);
            Type documentQueryType = typeof(CosmosLinqQuery<bool>).GetGenericTypeDefinition().MakeGenericType(expressionType);
            return (IQueryable)Activator.CreateInstance(
                documentQueryType,
                this.container,
                this.responseFactory,
                this.queryClient,
                this.continuationToken,
                this.cosmosQueryRequestOptions,
                expression,
                this.allowSynchronousQueryExecution,
                this.linqSerializerOptions);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            // ISSUE-TODO-adityasa-2024/1/26 - We should be able to delegate the implementation to ExecuteAggregateAsync method below by providing an Async implementation of ExecuteScalar.
            Type cosmosQueryType = typeof(CosmosLinqQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(TResult));
            CosmosLinqQuery<TResult> cosmosLINQQuery = (CosmosLinqQuery<TResult>)Activator.CreateInstance(
                cosmosQueryType,
                this.container,
                this.responseFactory,
                this.queryClient,
                this.continuationToken,
                this.cosmosQueryRequestOptions,
                expression,
                this.allowSynchronousQueryExecution,
                this.linqSerializerOptions);
            this.onExecuteScalarQueryCallback?.Invoke(cosmosLINQQuery);
            return cosmosLINQQuery.ExecuteScalar();
        }

        //Sync execution of query via direct invoke on IQueryProvider.
        public object Execute(Expression expression)
        {
            Type cosmosQueryType = typeof(CosmosLinqQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(object));
            CosmosLinqQuery<object> cosmosLINQQuery = (CosmosLinqQuery<object>)Activator.CreateInstance(
                cosmosQueryType,
                this.container,
                this.responseFactory,
                this.queryClient,
                this.continuationToken,
                this.cosmosQueryRequestOptions,
                this.allowSynchronousQueryExecution,
                this.linqSerializerOptions);
            this.onExecuteScalarQueryCallback?.Invoke(cosmosLINQQuery);
            return cosmosLINQQuery.ToList().FirstOrDefault();
        }

        public Task<Response<TResult>> ExecuteAggregateAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            Type cosmosQueryType = typeof(CosmosLinqQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(TResult));
            CosmosLinqQuery<TResult> cosmosLINQQuery = (CosmosLinqQuery<TResult>)Activator.CreateInstance(
                cosmosQueryType,
                this.container,
                this.responseFactory,
                this.queryClient,
                this.continuationToken,
                this.cosmosQueryRequestOptions,
                expression,
                this.allowSynchronousQueryExecution,
                this.linqSerializerOptions);
            return TaskHelper.RunInlineIfNeededAsync(() => cosmosLINQQuery.AggregateResultAsync(cancellationToken));
        }
    }
}
