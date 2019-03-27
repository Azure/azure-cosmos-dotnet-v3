//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;

    internal abstract class PartitionCheckpointer
    {
        /// <summary>
        /// Checkpoint the given partition up to the given continuation token.
        /// </summary>
        /// <param name="сontinuationToken">Continuation token</param>
        public abstract Task CheckpointPartitionAsync(string сontinuationToken);
    }
}