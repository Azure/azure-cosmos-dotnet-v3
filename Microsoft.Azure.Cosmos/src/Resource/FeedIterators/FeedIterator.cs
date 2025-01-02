//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <example>
    /// Example on how to fully drain the query results.
    /// <code language="c#">
    /// <![CDATA[
    /// QueryDefinition queryDefinition = new QueryDefinition("select c.id From c where c.status = @status")
    ///               .WithParameter("@status", "Failure");
    /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
    ///     queryDefinition))
    /// {
    ///     while (feedIterator.HasMoreResults)
    ///     {
    ///         // Stream iterator returns a response with status code
    ///         using(ResponseMessage response = await feedIterator.ReadNextAsync())
    ///         {
    ///             // Handle failure scenario
    ///             if(!response.IsSuccessStatusCode)
    ///             {
    ///                 // Log the response.Diagnostics and handle the error
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public abstract class FeedIterator : IDisposable
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
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     queryDefinition))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         // Stream iterator returns a response with status code
        ///         using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             // Handle failure scenario
        ///             if(!response.IsSuccessStatusCode)
        ///             {
        ///                 // Log the response.Diagnostics and handle the error
        ///             }
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
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     queryDefinition))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         // Stream iterator returns a response with status code
        ///         using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             // Handle failure scenario
        ///             if(!response.IsSuccessStatusCode)
        ///             {
        ///                 // Log the response.Diagnostics and handle the error
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default);

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
        /// Operation Type used for open telemetry traces, it will set optionally, otherwise default operation type will be used in traces
        /// </summary>
        internal Documents.OperationType? operationType;

        /// <summary>
        /// collect SQL query Specs for tracing
        /// </summary>
        internal SqlQuerySpec querySpec;

        /// <summary>
        /// Collect operation metrics options for open telemetry metrics
        /// </summary>
        internal OperationMetricsOptions operationMetricsOptions;

        /// <summary>
        /// Collect network metrics options for open telemetry metrics
        /// </summary>
        internal NetworkMetricsOptions networkMetricsOptions;

        /// <summary>
        /// Setup the information required for telemetry
        /// </summary>
        /// <param name="feedIteratorInternal"></param>
        internal void SetupInfoForTelemetry(FeedIterator feedIteratorInternal)
        {
            this.SetupInfoForTelemetry(
                feedIteratorInternal.databaseName,
                feedIteratorInternal.operationName,
                feedIteratorInternal.operationType,
                feedIteratorInternal.querySpec,
                feedIteratorInternal.operationMetricsOptions,
                feedIteratorInternal.networkMetricsOptions);
        }

        /// <summary>
        /// Setup the information required for telemetry
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="operationName"></param>
        /// <param name="operationType"></param>
        /// <param name="querySpec"></param>
        /// <param name="operationMetricsOptions"></param>
        /// <param name="networkMetricOptions"></param>
        internal void SetupInfoForTelemetry(string databaseName, 
            string operationName, 
            OperationType? operationType, 
            SqlQuerySpec querySpec, 
            OperationMetricsOptions operationMetricsOptions, 
            NetworkMetricsOptions networkMetricOptions)
        {
            this.databaseName = databaseName;

            this.operationName = operationName;
            this.operationType = operationType;

            this.querySpec = querySpec;
            this.operationMetricsOptions = operationMetricsOptions;
            this.networkMetricsOptions = networkMetricOptions;
        }
    }
}