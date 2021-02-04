//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

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
        ///         using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             // Handle failure scenario
        ///             if (!response.IsSuccessStatusCode)
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
        /// Gets the entire result set from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns an Asynchronous enumerator around the content of the container</returns>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("select c.id From c where c.status = @status")
        ///               .WithParameter("@status", "Failure");
        /// using (FeedIterator<MyItem> feedIterator = this.Container.GetItemQueryIterator<MyItem>(
        ///     queryDefinition))
        /// {
        ///     foreach async (var response in feedIterator.ReadAsync())
        ///     {
        ///         using (response)
        ///         {
        ///             // Handle failure scenario
        ///             if (!response.IsSuccessStatusCode)
        ///             {
        ///                 // Log the response.Diagnostics and handle the error
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
        public async IAsyncEnumerable<ResponseMessage> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore VSTHRD200 // Because this rule doesn't know about IAsyncEnumerable<T> (yet)
        {
            while (this.HasMoreResults && !cancellationToken.IsCancellationRequested)
            {
                yield return await this.ReadNextAsync();
            }
        }

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
    }
}