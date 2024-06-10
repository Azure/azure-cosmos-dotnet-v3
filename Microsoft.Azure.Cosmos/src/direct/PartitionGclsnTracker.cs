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

    internal class PartitionGclsnTracker
    {
        public PartitionKeyRange partitionKeyRangeId { get; }
        private long gclsn;
        private readonly object lockObject = new object();

        public PartitionGclsnTracker(PartitionKeyRange partitionKeyRangeId, long gclsn)
        {
            this.partitionKeyRangeId = partitionKeyRangeId;
            this.gclsn = gclsn;
        }

        public void SetGclsn(long gclsn)
        {
            if (gclsn <= this.gclsn)
            {
                return;
            }

            lock (this.lockObject)
            {
                if (gclsn <= this.gclsn)
                {
                    return;
                }

                this.gclsn = gclsn;
            }
        }

        public long GetGclsn()
        {
            lock (this.lockObject)
            {
                return this.gclsn;
            }
        }
    }
}
