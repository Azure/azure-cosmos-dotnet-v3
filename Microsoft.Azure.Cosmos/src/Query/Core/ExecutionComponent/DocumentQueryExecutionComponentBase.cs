//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// Base class for all DocumentQueryExecutionComponents that implements and IDocumentQueryExecutionComponent
    /// </summary>
    internal abstract class DocumentQueryExecutionComponentBase : IDocumentQueryExecutionComponent
    {
        public static readonly string UseCosmosElementContinuationTokenInstead = $"Use Cosmos Element Continuation Token instead.";

        /// <summary>
        /// Source DocumentQueryExecutionComponent that this component will drain from.
        /// </summary>
        protected readonly IDocumentQueryExecutionComponent Source;

        /// <summary>
        /// Initializes a new instance of the DocumentQueryExecutionComponentBase class.
        /// </summary>
        /// <param name="source">The source to drain documents from.</param>
        protected DocumentQueryExecutionComponentBase(IDocumentQueryExecutionComponent source)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Gets a value indicating whether or not this component is done draining documents.
        /// </summary>
        public virtual bool IsDone => this.Source.IsDone;

        /// <summary>
        /// Disposes this context.
        /// </summary>
        public virtual void Dispose()
        {
            this.Source.Dispose();
        }

        /// <summary>
        /// Drains documents from this execution context.
        /// </summary>
        /// <param name="maxElements">Upper bound for the number of documents you wish to receive.</param>
        /// <param name="token">The cancellation token to use.</param>
        /// <returns>A DoucmentFeedResponse of documents.</returns>
        public virtual Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return this.Source.DrainAsync(maxElements, token);
        }

        /// <summary>
        /// Stops the execution component.
        /// </summary>
        public void Stop()
        {
            this.Source.Stop();
        }

        public abstract CosmosElement GetCosmosElementContinuationToken();
    }
}
