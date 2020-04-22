//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class DatabaseInlineCore : Database
    {
        private readonly DatabaseCore database;

        public override string Id => this.database.Id;

        internal DatabaseInlineCore(DatabaseCore database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            this.database = database;
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerAsync(containerProperties, throughput, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerAsync(string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerAsync(id, partitionKeyPath, throughput, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerIfNotExistsAsync(id, partitionKeyPath, throughput, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, cancellationToken));
        }

        public override Task<UserResponse> CreateUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateUserAsync(id, requestOptions, cancellationToken));
        }

        public override ContainerBuilder DefineContainer(
            string name,
            string partitionKeyPath)
        {
            return this.database.DefineContainer(name, partitionKeyPath);
        }

        public override Task<DatabaseResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.DeleteAsync(requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.DeleteStreamAsync(requestOptions, cancellationToken));
        }

        public override Container GetContainer(string id)
        {
            return this.database.GetContainer(id);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.database.GetContainerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.database.GetContainerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.database.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.database.GetContainerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override User GetUser(string id)
        {
            return this.database.GetUser(id);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.database.GetUserQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.database.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override Task<DatabaseResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReadAsync(requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReadStreamAsync(requestOptions, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReadThroughputAsync(cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReadThroughputAsync(requestOptions, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReplaceThroughputAsync(throughput, requestOptions, cancellationToken));
        }

#if PREVIEW
        public override
#else
        internal
#endif
        Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.ReplaceThroughputAsync(throughputProperties, requestOptions, cancellationToken));
        }

#if PREVIEW
        public override
#else
        internal
#endif
        Task<ContainerResponse> CreateContainerAsync(ContainerProperties containerProperties, ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerAsync(containerProperties, throughputProperties, requestOptions, cancellationToken));
        }

#if PREVIEW
        public override
#else
        internal
#endif
        Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties, requestOptions, cancellationToken));
        }

#if PREVIEW
        public override
#else
        internal
#endif
        Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.CreateContainerStreamAsync(containerProperties, throughputProperties, requestOptions, cancellationToken));
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.database.UpsertUserAsync(id, requestOptions, cancellationToken));
        }

        public static implicit operator DatabaseCore(DatabaseInlineCore databaseInlineCore) => databaseInlineCore.database;
    }
}
