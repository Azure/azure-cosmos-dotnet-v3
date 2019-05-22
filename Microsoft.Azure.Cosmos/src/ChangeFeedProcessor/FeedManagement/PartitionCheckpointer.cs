//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
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