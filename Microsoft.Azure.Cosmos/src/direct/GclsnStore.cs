//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal class GclsnStore
    {
       private ConcurrentDictionary<PartitionKeyRange, PartitionGclsnTracker> partitionKeyRangeToGlobalCommittedlsnCache = new();

        public void SetGclsn(PartitionKeyRange partitionKeyRangeId, long gclsn)
        {
            if(partitionKeyRangeId == null)
            {
                return;
            }

            if (!this.partitionKeyRangeToGlobalCommittedlsnCache.TryGetValue(partitionKeyRangeId, out PartitionGclsnTracker partitionGclsn))
            {
                partitionGclsn = new PartitionGclsnTracker(partitionKeyRangeId, gclsn);
                if (!this.partitionKeyRangeToGlobalCommittedlsnCache.TryAdd(partitionKeyRangeId, partitionGclsn))
                {
                    throw new InvalidOperationException($"Failed to add partitionKeyRangeId: {partitionKeyRangeId} to the GclsnStore.");
                }
            }

            partitionGclsn.SetGclsn(gclsn);
        }

        public bool TryGetGclsn(PartitionKeyRange partitionKeyRangeId, out long gclsn)
        {
            if(this.partitionKeyRangeToGlobalCommittedlsnCache.TryGetValue(partitionKeyRangeId, out PartitionGclsnTracker gclsnTracker))
            {
                gclsn = gclsnTracker.GetGclsn();
                return true;
            }

            gclsn = -1;
            return false;
        }

        public PartitionGclsnTracker GetPartitionGclsnTracker(PartitionKeyRange partitionKeyRangeId)
        {
            if (this.partitionKeyRangeToGlobalCommittedlsnCache.TryGetValue(partitionKeyRangeId, out PartitionGclsnTracker gclsnTracker))
            {
                return gclsnTracker;
            }

            PartitionGclsnTracker partitionGclsnTracker = new PartitionGclsnTracker(partitionKeyRangeId, -1);
            if (this.partitionKeyRangeToGlobalCommittedlsnCache.TryAdd(partitionKeyRangeId, partitionGclsnTracker))
            {
                return partitionGclsnTracker;
            }

            if (this.partitionKeyRangeToGlobalCommittedlsnCache.TryGetValue(partitionKeyRangeId, out gclsnTracker))
            {
                return gclsnTracker;
            }

            throw new InvalidOperationException($"Failed to add partitionKeyRangeId: {partitionKeyRangeId} to the GclsnStore.");
        }
    }
}
