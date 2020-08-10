//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the context for the current changes.
    /// </summary>
    public abstract class ChangeFeedProcessorContext
    {
        /// <summary>
        /// ets the token representative of the current lease from which the changes come from.
        /// </summary>
        public abstract string LeaseToken { get; }

        /// <summary>
        /// Gets the session token returned as part of the Change Feed request.
        /// </summary>
        /// <remarks>
        /// Useful if the current application passes item information to another application and the account is on Session consistency.
        /// </remarks>
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/consistency-levels"/>
        public abstract string SessionToken { get; }

        /// <summary>
        /// Checkpoints progress of a stream. This method is valid only if manual checkpoint was configured.
        /// Client may accept multiple change feed batches to process in parallel.
        /// Once first N document processing was finished the client can call checkpoint on the last completed batches in the row.
        /// In case of automatic checkpointing this is method throws.
        /// </summary>
        /// <exception cref="Exceptions.LeaseLostException">Thrown if other host acquired the lease or the lease was deleted</exception>
        /// <returns>A <see cref="Task"/> representing an operation that updates the lease with the current continuation.</returns>
        public abstract Task CheckpointAsync();
    }
}