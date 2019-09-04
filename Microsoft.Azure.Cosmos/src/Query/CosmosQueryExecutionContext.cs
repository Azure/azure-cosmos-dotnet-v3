//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Interface for all document query execution contexts
    /// </summary>
    internal abstract class CosmosQueryExecutionContext : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether or not the context is done serving documents.
        /// </summary>
        public abstract bool IsDone
        {
            get;
        }

        public abstract void Dispose();

        /// <summary>
        /// Executes the context to feed the next page of results.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on, which in return provides a DoucmentFeedResponse of documents.</returns>
        public abstract Task<QueryResponseCore> ExecuteNextAsync(CancellationToken token);
    }
}
