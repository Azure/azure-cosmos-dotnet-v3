//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
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