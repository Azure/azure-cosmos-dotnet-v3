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
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    internal sealed class DocumentQueryProvider : IDocumentQueryProvider
    {
        private readonly IDocumentQueryClient client;
        private readonly ResourceType resourceTypeEnum;
        private readonly Type resourceType;
        private readonly string documentsFeedOrDatabaseLink;
        private readonly FeedOptions feedOptions;
        private readonly object partitionKey;
        private readonly Action<IQueryable> onExecuteScalarQueryCallback;

        public DocumentQueryProvider(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            string documentsFeedOrDatabaseLink,
            FeedOptions feedOptions,
            object partitionKey = null,
            Action<IQueryable> onExecuteScalarQueryCallback = null)
        {
            this.client = client;
            this.resourceTypeEnum = resourceTypeEnum;
            this.resourceType = resourceType;
            this.documentsFeedOrDatabaseLink = documentsFeedOrDatabaseLink;
            this.feedOptions = feedOptions;
            this.partitionKey = partitionKey;
            this.onExecuteScalarQueryCallback = onExecuteScalarQueryCallback;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new DocumentQuery<TElement>(
                this.client,
                this.resourceTypeEnum,
                this.resourceType,
                this.documentsFeedOrDatabaseLink,
                expression,
                this.feedOptions,
                this.partitionKey);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            Type expressionType = TypeSystem.GetElementType(expression.Type);
            Type documentQueryType = typeof(DocumentQuery<bool>).GetGenericTypeDefinition().MakeGenericType(expressionType);
            return (IQueryable)Activator.CreateInstance(
                documentQueryType,
                this.client,
                this.resourceTypeEnum,
                this.resourceType,
                this.documentsFeedOrDatabaseLink,
                expression,
                this.feedOptions,
                this.partitionKey);
        }

        //Sync execution of query via direct invoke on IQueryProvider.
        public TResult Execute<TResult>(Expression expression)
        {
            Type documentQueryType = typeof(DocumentQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(TResult));
            DocumentQuery<TResult> documentQuery = (DocumentQuery<TResult>)Activator.CreateInstance(
                documentQueryType,
                this.client,
                this.resourceTypeEnum,
                this.resourceType,
                this.documentsFeedOrDatabaseLink,
                expression,
                this.feedOptions,
                this.partitionKey);
            this.onExecuteScalarQueryCallback?.Invoke(documentQuery);

            return documentQuery.ToList().FirstOrDefault();
        }

        //Sync execution of query via direct invoke on IQueryProvider.
        public object Execute(Expression expression)
        {
            Type documentQueryType = typeof(DocumentQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(object));
            DocumentQuery<object> documentQuery = (DocumentQuery<object>)Activator.CreateInstance(
                documentQueryType,
                this.client,
                this.resourceTypeEnum,
                this.resourceType,
                this.documentsFeedOrDatabaseLink,
                expression,
                this.feedOptions,
                this.partitionKey);
            this.onExecuteScalarQueryCallback?.Invoke(documentQuery);

            return documentQuery.ToList().FirstOrDefault();
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            Type documentQueryType = typeof(DocumentQuery<bool>).GetGenericTypeDefinition().MakeGenericType(typeof(TResult));
            DocumentQuery<TResult> documentQuery = (DocumentQuery<TResult>)Activator.CreateInstance(
                documentQueryType,
                this.client,
                this.resourceTypeEnum,
                this.resourceType,
                this.documentsFeedOrDatabaseLink,
                expression,
                this.feedOptions,
                this.partitionKey);
            this.onExecuteScalarQueryCallback?.Invoke(documentQuery);

            List<TResult> result = await documentQuery.ExecuteAllAsync();
            return result.FirstOrDefault();
        }
    }

    internal interface IDocumentQueryProvider : IQueryProvider
    {
        Task<TResult> ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default);
    }
}
