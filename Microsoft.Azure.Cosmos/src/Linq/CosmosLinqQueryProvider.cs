//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary> 
    /// This class serve as LINQ query provider implementing IQueryProvider.
    /// </summary> 
    internal sealed class CosmosLinqQueryProvider : IQueryProvider
    {
        private readonly ContainerCore container;
        private readonly CosmosQueryClientCore queryClient;
        private readonly CosmosResponseFactory responseFactory;
        private readonly QueryRequestOptions cosmosQueryRequestOptions;
        private readonly bool allowSynchronousQueryExecution;
        private readonly Action<IQueryable> onExecuteScalarQueryCallback;
        private readonly string continuationToken;

        public CosmosLinqQueryProvider(
           ContainerCore container,
           CosmosResponseFactory responseFactory,
           CosmosQueryClientCore queryClient,
           string continuationToken,
           QueryRequestOptions cosmosQueryRequestOptions,
           bool allowSynchronousQueryExecution,
           Action<IQueryable> onExecuteScalarQueryCallback = null)
        {
            this.container = container;
            this.responseFactory = responseFactory;
            this.queryClient = queryClient;
            this.continuationToken = continuationToken;
            this.cosmosQueryRequestOptions = cosmosQueryRequestOptions;
            this.allowSynchronousQueryExecution = allowSynchronousQueryExecution;
            this.onExecuteScalarQueryCallback = onExecuteScalarQueryCallback;
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
                this.allowSynchronousQueryExecution);
        }

        public IQueryable CreateQuery(Expression expression)
        {
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
                this.allowSynchronousQueryExecution);
        }

        public TResult Execute<TResult>(Expression expression)
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
                this.allowSynchronousQueryExecution);
            this.onExecuteScalarQueryCallback?.Invoke(cosmosLINQQuery);
            return cosmosLINQQuery.ToList().FirstOrDefault();
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
                this.allowSynchronousQueryExecution);
            this.onExecuteScalarQueryCallback?.Invoke(cosmosLINQQuery);
            return cosmosLINQQuery.ToList().FirstOrDefault();
        }

        public async Task<TResult> ExecuteAggregateAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default(CancellationToken))
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
                this.allowSynchronousQueryExecution);
            IList<TResult> result = await cosmosLINQQuery.ToListAsync();
            return result.FirstOrDefault();
        }
    }
}
