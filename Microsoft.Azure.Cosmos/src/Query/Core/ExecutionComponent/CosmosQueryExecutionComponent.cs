//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// Interface for all DocumentQueryExecutionComponents
    /// </summary>
    internal abstract class CosmosQueryExecutionComponent : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this component is done draining documents.
        /// </summary>
        public abstract bool IsDone { get; }

        /// <summary>
        /// Drains documents from this component.
        /// </summary>
        /// <param name="maxElements">The maximum number of documents to drain.</param>
        /// <param name="token">The cancellation token to cancel tasks.</param>
        /// <returns>A task that when awaited on returns a feed response.</returns>
        public abstract Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token);

        /// <summary>
        /// Stops this document query execution component.
        /// </summary>
        public abstract void Stop();

        public abstract void Dispose();
    }
}
