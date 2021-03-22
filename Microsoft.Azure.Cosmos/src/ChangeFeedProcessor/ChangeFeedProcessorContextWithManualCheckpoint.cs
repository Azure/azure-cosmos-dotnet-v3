//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;

    /// <summary>
    /// Context that is related to the set of delivered changes when manual checkpointing is enabled.
    /// </summary>
    public abstract class ChangeFeedProcessorContextWithManualCheckpoint : ChangeFeedProcessorContext
    {
        /// <summary>
        /// Checkpoints progress of a stream. This method is valid only if manual checkpoint was configured.
        /// Client may accept multiple change feed batches to process in parallel.
        /// Once first N document processing was finished the client can call checkpoint on the last completed batches in the row.
        /// </summary>
        /// <returns>An asynchronous <see cref="Task"/> representing an attempt to checkpoint the current context.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (bool isSuccess, CosmosException error) checkpointResult = await context.TryCheckpointAsync();
        /// if (!isSuccess)
        /// {
        ///     // log error, could not checkpoint
        ///     throw error;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<(bool isSuccess, CosmosException error)> TryCheckpointAsync();
    }
}