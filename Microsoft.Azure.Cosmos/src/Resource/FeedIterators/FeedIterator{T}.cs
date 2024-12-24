//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <example>
    /// Example on how to fully drain the query results.
    /// <code language="c#">
    /// <![CDATA[
    /// QueryDefinition queryDefinition = new QueryDefinition("select c.id From c where c.status = @status")
    ///               .WithParameter("@status", "Failure");
    /// using (FeedIterator<MyItem> feedIterator = this.Container.GetItemQueryIterator<MyItem>(
    ///     queryDefinition))
    /// {
    ///     while (feedIterator.HasMoreResults)
    ///     {
    ///         FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
    ///         foreach (var item in response)
    ///         {
    ///             Console.WriteLine(item);
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public abstract class FeedIterator<T> : IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// Tells if there is more results that need to be retrieved from the service
        /// </summary>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("select c.id From c where c.status = @status")
        ///               .WithParameter("@status", "Failure");
        /// using (FeedIterator<MyItem> feedIterator = this.Container.GetItemQueryIterator<MyItem>(
        ///     queryDefinition))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///         foreach (var item in response)
        ///         {
        ///             Console.WriteLine(item);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract bool HasMoreResults { get; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("select c.id From c where c.status = @status")
        ///               .WithParameter("@status", "Failure");
        /// using (FeedIterator<MyItem> feedIterator = this.Container.GetItemQueryIterator<MyItem>(
        ///     queryDefinition))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///         foreach (var item in response)
        ///         {
        ///             Console.WriteLine(item);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the unmanaged resources used by the FeedIterator and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Default implementation does not need to clean anything up
            if (!this.disposedValue)
            {
                this.disposedValue = true;
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the FeedIterator and optionally
        /// releases the managed resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
        }

        /// <summary>
        /// Making Container instance available in order to collect container related information in open telemetry attributes
        /// </summary>
        internal ContainerInternal container;

        /// <summary>
        /// Collect database name if container information not available in open telemetry attributes
        /// </summary>
        internal string databaseName;

        /// <summary>
        /// Operation Name used for open telemetry traces
        /// </summary>
        internal string operationName;

        /// <summary>
        /// Operation Type used for open telemetry traces
        /// </summary>
        internal Documents.OperationType? operationType;

        /// <summary>
        /// collect SQL query Specs for tracing
        /// </summary>
        internal SqlQuerySpec querySpec;

        internal OperationMetricsOptions operationMetricsOptions;

        internal NetworkMetricsOptions networkMetricsOptions;

        internal void SetupInfoForTelemetry(FeedIterator<T> feedIteratorInternal)
        {
            this.container = feedIteratorInternal.container;
            this.databaseName = feedIteratorInternal.databaseName;

            this.operationName = feedIteratorInternal.operationName;
            this.operationType = feedIteratorInternal.operationType;

            this.querySpec = feedIteratorInternal.querySpec;
            this.operationMetricsOptions = feedIteratorInternal.operationMetricsOptions;
            this.networkMetricsOptions = feedIteratorInternal.networkMetricsOptions;
        }
    }
}